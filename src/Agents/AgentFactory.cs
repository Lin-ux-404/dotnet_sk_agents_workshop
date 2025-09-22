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
    

    public AdminAgent CreateAdminAgent()
    {
        // TODO: Exercise 4 - Implement this method
        // 1. Create any necessary tools for the admin agent
        // 2. Initialize and return a new AdminAgent with the kernel and tools
        
        throw new NotImplementedException("Exercise: Implement the CreateAdminAgent method");
    }
    

    public AgentPlugin CreateAgentPlugin()
    {
        // TODO:  Exercise 5 - Implement this method
        // 1. Create the FAQ and Admin agents
        // 2. Initialize a new AgentPlugin
        // 3. Register the agents with the plugin
        // 4. Return the configured plugin
        
        throw new NotImplementedException("Exercise: Implement the CreateAgentPlugin method");
    }

    public OrchestratorAgent CreateOrchestrator()
    {
        // TODO:  Exercise 6 Implement this method
        // 1. Create the necessary sub-agents
        // 2. Initialize and return a new OrchestratorAgent with the kernel, sub-agents, and any required configuration
        
        throw new NotImplementedException("Exercise: Implement the CreateOrchestrator method");
    }
}