using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using AgentsDemoSK.Tools;

namespace AgentsDemoSK.Agents;


public class AdminAgent : IAgent
{
    /// Gets the name of the agent
    public string Name => "AdminAgent";

    public ChatCompletionAgent Agent { get; }

    public AdminAgent(Kernel kernel, EmailTool emailTool)
    {
        Agent = CreateChatAgent(kernel, emailTool);
    }

    private static ChatCompletionAgent CreateChatAgent(Kernel kernel, EmailTool emailTool)
    {
        // Clone kernel for agent
        var agentKernel = kernel.Clone();
        
        // Create function from the email tool method
        var sendConfirmationEmailFunction = agentKernel.CreateFunctionFromMethod(
            emailTool.SendConfirmationEmail, 
            "EmailTool");
        
        // Import the functions into a plugin
        agentKernel.ImportPluginFromFunctions("EmailTool", [sendConfirmationEmailFunction]);
        
        // Create agent settings
        var settings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };
        
        return new ChatCompletionAgent
        {
            Name = "AdminAgent",
            Kernel = agentKernel,
            Instructions = """
                You are a specialized healthcare administrative assistant.
                You help patients schedule, reschedule, or cancel appointments, and process complaints.
                
                When handling administrative tasks:
                1. First understand the specific request (appointment booking, rescheduling, cancellation, or complaint)
                2. Gather all necessary information from the user
                3. Process the request and confirm actions taken
                4. Send confirmation emails when tasks are completed
                
                APPOINTMENT HANDLING:
                - Collect patient name, preferred date/time, reason for visit
                - For rescheduling, get original and new appointment details
                - For cancellations, confirm appointment details and cancellation reason
                
                COMPLAINT HANDLING:
                - Listen carefully to the complaint details
                - Acknowledge the issue with empathy
                - Explain the next steps in the complaint process
                - Collect email for follow-up communication
                
                RESPONSE FORMAT:
                - Use professional, courteous language
                - Confirm actions taken and next steps
                - Ask for email address to send confirmations
                - Request feedback about the service when appropriate
            """,
            Arguments = new KernelArguments(settings)
        };
    }
}