using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using AgentsDemoSK.Tools;
using AgentsDemoSK.Tracing;

namespace AgentsDemoSK.Agents;

public interface IAgentFactory
{
    OrchestratorAgent CreateOrchestrator();
    
    AgentPlugin CreateAgentPlugin();

    FAQAgent CreateFAQAgent();
    
    AdminAgent CreateAdminAgent();
}

/// Factory for creating agents
public class AgentFactory : IAgentFactory
{
    private readonly Kernel _kernel;
    private readonly IConfiguration _configuration;
    private readonly GenAITracer _genAITracer;
    private readonly ILogger<SearchTool> _searchToolLogger;
    
    public AgentFactory(
        Kernel kernel, 
        IConfiguration configuration, 
        GenAITracer genAITracer,
        ILogger<SearchTool> searchToolLogger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _genAITracer = genAITracer ?? throw new ArgumentNullException(nameof(genAITracer));
        _searchToolLogger = searchToolLogger ?? throw new ArgumentNullException(nameof(searchToolLogger));
    }

    /// Creates an FAQ agent for healthcare insurance questions
    public FAQAgent CreateFAQAgent()
    {
        var searchTool = new SearchTool(_configuration, _genAITracer, _searchToolLogger);
        return new FAQAgent(_kernel, searchTool);
    }
    
    /// Creates an admin agent for appointment and complaint handling
    public AdminAgent CreateAdminAgent()
    {
        var emailTool = new EmailTool();
        return new AdminAgent(_kernel, emailTool);
    }
    
    /// Creates an agent plugin with registered sub-agents
    public AgentPlugin CreateAgentPlugin()
    {
        var agentPlugin = new AgentPlugin(_genAITracer);
        
        // Create and register specialized agents
        agentPlugin.RegisterAgent(CreateFAQAgent());
        agentPlugin.RegisterAgent(CreateAdminAgent());
        
        return agentPlugin;
    }

    /// Creates an orchestrator agent with its sub-agents
    public OrchestratorAgent CreateOrchestrator()
    {

        var intentTool = new IntentTool(_configuration);
        var agentPlugin = CreateAgentPlugin();
    
        return new OrchestratorAgent(_kernel, intentTool, agentPlugin);
    }
}