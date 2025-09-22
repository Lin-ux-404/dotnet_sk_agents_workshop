using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace AgentsDemoSK.Tools;

public interface ITool
{    string Name { get; }
}