using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using AgentsDemoSK.Agents;
using AgentsDemoSK.Tracing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AgentsDemoSK.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly OrchestratorAgent _orchestratorAgent;
    // Single dictionary to store chat histories by chat ID
    private readonly ConcurrentDictionary<string, ChatHistory> _chatHistories = new ConcurrentDictionary<string, ChatHistory>();
    private readonly GenAITracer _genAITracer;
    private readonly ILogger<ChatController> _logger;
    
    public ChatController(
        OrchestratorAgent orchestratorAgent,
        GenAITracer genAITracer,
        ILogger<ChatController> logger)
    {
        _orchestratorAgent = orchestratorAgent ?? throw new ArgumentNullException(nameof(orchestratorAgent));
        _genAITracer = genAITracer ?? throw new ArgumentNullException(nameof(genAITracer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public class ChatRequest
    {

        public string Message { get; set; } = string.Empty;
        public string ChatId { get; set; } = string.Empty;
        public string UserId { get; set; } = "anonymous";
    }

    public class ChatResponse
    {
        public string Message { get; set; } = string.Empty;
        public List<string> AgentsUsed { get; set; } = new List<string>();
        public List<string> References { get; set; } = new List<string>();
        public string ChatId { get; set; } = string.Empty;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> PostAsync([FromBody] ChatRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        string chatId = request.ChatId ?? Guid.NewGuid().ToString();
        string responseId = $"{chatId}/{Guid.NewGuid().ToString()}";
        
        try
        {

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["chatId"] = chatId,
                ["userId"] = request.UserId
            }))
            {
                _logger.LogInformation("Conversation started: {chatId}", chatId);
            }
            
            // Trace user message for Foundry
            _genAITracer.TraceUserMessage(chatId, request.Message, request.UserId);
            
            // Get or create chat history for this session
            var chatHistory = _chatHistories.GetOrAdd(chatId, _ => {
                // Create new chat history with system message for first-time conversations
                var newHistory = new ChatHistory();
                string systemMessage = "You are a helpful healthcare insurance assistant.";
                newHistory.AddSystemMessage(systemMessage);
                _genAITracer.TraceSystemMessage(chatId, systemMessage);
                return newHistory;
            });
            
            // Add user message to chat history
            chatHistory.AddUserMessage(request.Message);
            
            // Set up tracing context for tools
            AgentsDemoSK.Tools.SearchTool.SetContext(chatId, "FAQAgent");
            AgentsDemoSK.Tools.IntentTool.SetContext(chatId);
            
            // Clear any previous intent information for this chat
            IntentTracker.Clear(chatId);
            
            // Register known agent-tool relationships dynamically
            _genAITracer.RegisterAgentTool("FAQAgent", "SearchTool");
            _genAITracer.RegisterAgentTool("AdminAgent", "EmailTool");
            _genAITracer.RegisterAgentTool("OrchestratorAgent", "IntentTool");
            
            // Get AgentPlugin directly from dependency injection
            var serviceProvider = HttpContext.RequestServices;
            var agentPlugin = serviceProvider.GetService(typeof(AgentPlugin)) as AgentPlugin;
            
            if (agentPlugin != null)
            {
                agentPlugin.SetChatId(chatId);
            }
            
            // Trace the complete conversation history
            _genAITracer.TraceConversationHistory(chatId, FormatChatHistoryForTracing(chatHistory));
            
            // Create activity for agent invocation
            var agentActivity = _genAITracer.StartAgentInvocation(
                chatId,
                _orchestratorAgent.Name,
                request.Message);
            
            // Get response from orchestrator with full chat history for context
            var response = await _orchestratorAgent.InvokeAsync(chatHistory);
            
            // Complete the agent activity
            if (agentActivity != null)
            {
                _genAITracer.CompleteAgentInvocation(
                    agentActivity,
                    response.Content ?? string.Empty,
                    true);
            }
            
            // Add assistant response to chat history
            chatHistory.AddAssistantMessage(response.Content ?? string.Empty);
            
            // Get the list of agents that were used
            var agentsUsed = _orchestratorAgent.GetAndClearUsedAgents();
            
            // Get document references that were used
            List<string> references = _orchestratorAgent.GetAndClearDocumentReferences();
            
            // If no references were found through tracking, try to extract them from the response
            if (references.Count == 0)
            {
                references = ExtractReferences(response.Content ?? string.Empty);
            }
            
            // Create response object
            var chatResponse = new ChatResponse
            {
                Message = response.Content ?? string.Empty,
                AgentsUsed = agentsUsed,
                References = references,
                ChatId = chatId
            };
            
            // Log the chat turn
            stopwatch.Stop();
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["chatId"] = chatId,
                ["agentsUsed"] = string.Join(",", agentsUsed),
                ["durationMs"] = stopwatch.ElapsedMilliseconds
            }))
            {
                _logger.LogInformation(
                    "Chat turn: User message length {userMsgLen}, response length {responseMsgLen}, duration {durationMs}ms", 
                    request.Message.Length,
                    chatResponse.Message.Length,
                    stopwatch.ElapsedMilliseconds);
            }
            
            // Trace assistant message for Foundry
            // Estimate tokens based on content length
            double inputTokens = request.Message.Length / 4.0;  // rough estimate
            double outputTokens = chatResponse.Message.Length / 4.0;  // rough estimate
            _genAITracer.TraceAssistantMessage(chatId, responseId, chatResponse.Message, inputTokens, outputTokens);
            
            // If any agents were used, add evaluations
            if (agentsUsed.Count > 0)
            {
                _genAITracer.TraceEvaluation(chatId, responseId, "AgentUsage", agentsUsed.Count, 
                    $"Used {agentsUsed.Count} agents: {string.Join(", ", agentsUsed)}");
            }
            
            return Ok(chatResponse);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Log the error
            _logger.LogError(ex, "Error processing chat request: {ErrorMessage}", ex.Message);
            
            return StatusCode(500, new ChatResponse
            {
                Message = $"An error occurred: {ex.Message}",
                ChatId = chatId
            });
        }
    }


    private List<string> ExtractReferences(string content)
    {
        var references = new List<string>();
        
        if (string.IsNullOrEmpty(content))
        {
            return references;
        }
        
        // Look for "References: doc1.pdf, doc2.pdf, ..."
        var regex = new Regex(@"References:\s*(.*?)(?:\n|$)", RegexOptions.IgnoreCase);
        var match = regex.Match(content);
        
        if (match.Success && match.Groups.Count > 1)
        {
            string refsText = match.Groups[1].Value;
            string[] refs = refsText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var reference in refs)
            {
                references.Add(reference.Trim());
            }
        }
        
        return references;
    }
    
    private string FormatChatHistoryForTracing(ChatHistory history)
    {
        // If there's no history, return empty string
        if (history == null || !history.Any())
        {
            return string.Empty;
        }

        // Format conversation history
        var formattedHistory = new List<string>();
        foreach (var message in history)
        {
            formattedHistory.Add($"{message.Role}: {message.Content}");
        }

        // Return formatted history
        return string.Join("\n", formattedHistory);
    }
}