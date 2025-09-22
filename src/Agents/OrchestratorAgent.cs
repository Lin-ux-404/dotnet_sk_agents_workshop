using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using AgentsDemoSK.Tools;
using AgentsDemoSK.Tracing;

namespace AgentsDemoSK.Agents;


public class OrchestratorAgent : IAgent
{
    public string Name => "OrchestratorAgent";

    public ChatCompletionAgent Agent { get; }
    
    private static readonly ConcurrentBag<string> _usedAgents = new ConcurrentBag<string>();
    private static readonly ConcurrentBag<string> _documentReferences = new ConcurrentBag<string>();


    public OrchestratorAgent(
        Kernel kernel, 
        IntentTool intentTool, 
        FAQAgent faqAgent, 
        AdminAgent adminAgent,
        GenAITracer? genAITracer = null)
    {
        // Create agent plugin
        var agentPlugin = new AgentPlugin(genAITracer);
        agentPlugin.RegisterAgent(faqAgent);
        agentPlugin.RegisterAgent(adminAgent);
        
        Agent = CreateChatAgent(kernel, intentTool, agentPlugin);
    }

    public OrchestratorAgent(Kernel kernel, IntentTool intentTool, AgentPlugin agentPlugin)
    {
        Agent = CreateChatAgent(kernel, intentTool, agentPlugin);
    }
    
    public static void AddUsedAgent(string agentName)
    {
        _usedAgents.Add(agentName);
    }
    
    public List<string> GetAndClearUsedAgents()
    {
        var usedAgents = new List<string>();
        while (_usedAgents.TryTake(out var agentName))
        {
            usedAgents.Add(agentName);
        }
        return usedAgents;
    }

    public static void AddDocumentReference(string documentName)
    {
        if (!string.IsNullOrWhiteSpace(documentName))
        {
            _documentReferences.Add(documentName.Trim());
        }
    }
    
    public static void AddDocumentReferences(IEnumerable<string> documentNames)
    {
        if (documentNames != null)
        {
            foreach (var doc in documentNames)
            {
                AddDocumentReference(doc);
            }
        }
    }

    public List<string> GetAndClearDocumentReferences()
    {
        var references = new List<string>();
        while (_documentReferences.TryTake(out var reference))
        {
            references.Add(reference);
        }
        return references;
    }

    public async Task<ChatMessageContent> InvokeAsync(ChatMessageContent message)
    {
        // Create a chat history with the user's message
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(message.Content ?? string.Empty);
        
        // Build the response from the agent
        ChatMessageContent? response = null;
        
        await foreach (var item in Agent.InvokeAsync(chatHistory))
        {
            if (item.Message is ChatMessageContent content)
            {
                response = content;
            }
        }
        
        return response ?? new ChatMessageContent(AuthorRole.Assistant, "No response generated");
    }
    
    public async Task<ChatMessageContent> InvokeAsync(ChatHistory chatHistory)
    {
        if (chatHistory == null)
        {
            throw new ArgumentNullException(nameof(chatHistory));
        }
        
        // Build the response from the agent
        ChatMessageContent? response = null;
        
        await foreach (var item in Agent.InvokeAsync(chatHistory))
        {
            if (item.Message is ChatMessageContent content)
            {
                response = content;
            }
        }
        
        return response ?? new ChatMessageContent(AuthorRole.Assistant, "No response generated");
    }

    private static ChatCompletionAgent CreateChatAgent(Kernel kernel, IntentTool intentTool, AgentPlugin agentPlugin)
    {
        // Clone kernel for agent
        var agentKernel = kernel.Clone();
        
        // Create function from the intent tool method
        var recognizeIntentFunction = agentKernel.CreateFunctionFromMethod(
            intentTool.RecognizeIntent, 
            "IntentTool");
            
        // Import the intent tool functions into a plugin
        agentKernel.ImportPluginFromFunctions("IntentTool", [recognizeIntentFunction]);
        
        // Register the agent plugin
        agentKernel.ImportPluginFromObject(agentPlugin, "agents");
        
        // Create agent settings
        var settings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };
        
        // Create the chat completion agent
        return new ChatCompletionAgent
        {
            Name = "OrchestratorAgent",
            Instructions = @"
                You are a helpful healthcare assistant that coordinates between specialized agents to answer user questions about healthcare insurance and handle administrative tasks.
                
                You have access to these specialized agents:
                - agents.AskFAQAgent: Gets information about insurance coverage, eligibility, reimbursements, and general healthcare FAQs
                - agents.AskAdminAgent: Handles appointment scheduling, rescheduling, cancellations, and processing complaints
                
                WORKFLOW:
                1. For user queries, first analyze the intent using the IntentTool.RecognizeIntent function
                2. Based on the user's intent, call the appropriate specialized agent
                3. Return the specialist's response
                
                ROUTING RULES:
                - For insurance coverage, claims, eligibility, reimbursements: Use agents.AskFAQAgent
                - For appointment scheduling, rescheduling, cancellations, and processing complaints: Use agents.AskAdminAgent
                - If a query spans multiple domains, coordinate responses from multiple agents
                
                INTENT ANALYSIS WORKFLOW:
                1. First, analyze the user's intent using the RecognizeIntent function which returns Azure Language Service results
                2. Extract intents and entities from the result structure:
                   - The top intent is in result['result']['prediction']['topIntent']
                   - All intents with confidence scores are in result['result']['prediction']['intents']
                   - All detected entities are in result['result']['prediction']['entities']
                
                3. Use BOTH the detected intent AND entities for determining the appropriate agent:
                
                   INTENT-BASED ROUTING:
                   - For 'informatieVergoedingen' (coverage information): Use agents.AskFAQAgent
                   - For 'declaratieIndienen' (submit claim): Use agents.AskFAQAgent
                   - For 'klachtIndienen' (submit complaint): Use agents.AskAdminAgent
                   - For 'adviesVerzekering' (insurance advice): Use agents.AskFAQAgent
                   - For 'informatiePremie' (premium information): Use agents.AskFAQAgent
                   
                   ENTITY-BASED REFINEMENT:
                   - If entities like 'afspraak' (appointment) are detected: Prefer agents.AskAdminAgent
                   - If entities like 'klacht' (complaint) are detected: Prefer agents.AskAdminAgent
                
                - IMPORTANT: In your response, simply provide a clear answer to the user's question without mentioning which agent was used.
            ",
            Kernel = agentKernel,
            Arguments = new KernelArguments(settings)
        };
    }
}