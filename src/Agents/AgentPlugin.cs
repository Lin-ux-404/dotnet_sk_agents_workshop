using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using AgentsDemoSK.Tracing;

namespace AgentsDemoSK.Agents;


public class AgentPlugin
{

    private readonly Dictionary<string, IAgent> _agents = new();
    private readonly Dictionary<string, Dictionary<string, ChatHistory>> _agentChatHistories = new();
    private readonly GenAITracer? _genAITracer;
    private string _currentChatId = string.Empty;
    

    public AgentPlugin(GenAITracer? genAITracer = null)
    {
        _genAITracer = genAITracer;
    }
    
    public void SetChatId(string chatId)
    {
        _currentChatId = chatId;
    }
    

    public void RegisterAgent(IAgent agent)
    {
        if (agent == null) throw new ArgumentNullException(nameof(agent));
        _agents[agent.Name] = agent;
    }

    [KernelFunction, Description("Ask the FAQ agent about healthcare insurance coverage, reimbursements, eligibility, and general healthcare queries")]
    public async Task<string> AskFAQAgent(string question)
    {
        return await InvokeAgentAsync("FAQAgent", question);
    }
    
    [KernelFunction, Description("Ask the admin agent about appointment scheduling, rescheduling, cancellations, and processing complaints")]
    public async Task<string> AskAdminAgent(string question)
    {
        return await InvokeAgentAsync("AdminAgent", question);
    }
    
    private async Task<string> InvokeAgentAsync(string agentName, string question)
    {
        if (!_agents.ContainsKey(agentName))
            return $"Agent '{agentName}' not found.";
        
        var agent = _agents[agentName];
        
        // Track agent usage
        OrchestratorAgent.AddUsedAgent(agentName);
        
        // Set up tracing if available
        var agentActivity = _genAITracer?.StartAgentInvocation(_currentChatId, agentName, question);
        
        try
        {
            // Get or create a chat history for this agent and chat session
            if (!_agentChatHistories.TryGetValue(_currentChatId, out var agentHistories))
            {
                agentHistories = new Dictionary<string, ChatHistory>();
                _agentChatHistories[_currentChatId] = agentHistories;
            }
            
            if (!agentHistories.TryGetValue(agentName, out var chatHistory))
            {
                chatHistory = new ChatHistory();
                agentHistories[agentName] = chatHistory;
            }
            
            // Add the user's question to the chat history
            chatHistory.AddUserMessage(question);
            
            // Invoke the agent and collect the response
            var responseBuilder = new StringBuilder();
            await foreach (var item in agent.Agent.InvokeAsync(chatHistory))
            {
                if (item.Message is ChatMessageContent content)
                {
                    responseBuilder.Append(content.Content);
                }
            }
            
            var response = responseBuilder.ToString();
            
            // Add the assistant's response to the chat history
            chatHistory.AddAssistantMessage(response);
            
            // Complete tracing with success
            _genAITracer?.CompleteAgentInvocation(agentActivity, response, true);
            
            return response;
        }
        catch (Exception ex)
        {
            // Complete tracing with failure
            _genAITracer?.CompleteAgentInvocation(agentActivity, ex.Message, false);
            return $"Error: {ex.Message}";
        }
    }
}