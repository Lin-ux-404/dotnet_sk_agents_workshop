using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AgentsDemoSK.Agents;

/// Interface for all agents in the system - simplified for direct access to ChatCompletionAgent
public interface IAgent
{
    /// Gets the name of the agent
    string Name { get; }

    /// Gets the underlying chat completion agent
    ChatCompletionAgent Agent { get; }
}