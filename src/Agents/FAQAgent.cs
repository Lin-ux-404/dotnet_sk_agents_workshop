using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using AgentsDemoSK.Tools;

namespace AgentsDemoSK.Agents;

public class FAQAgent : IAgent
{

    public string Name => "FAQAgent";

    public ChatCompletionAgent Agent { get; }

    public FAQAgent(Kernel kernel, SearchTool searchTool)
    {
        Agent = CreateChatAgent(kernel, searchTool);
    }

    private static ChatCompletionAgent CreateChatAgent(Kernel kernel, SearchTool searchTool)
    {
        // TODO: Exercise 1 - Clone the kernel for the agent
        // Hint: Use the Kernel.Clone() method to create a copy of the kernel
        
        // TODO: Exercise 2 - Create function from the search tool method
        // Hint: Use kernel.CreateFunctionFromMethod to create a function from the SearchDocuments method
        
        // TODO: Exercise 3 - Create agent settings
        // Hint: Initialize a new KernelArguments object
        
        return new ChatCompletionAgent
        {
            Name = "FAQAgent",
            Kernel = agentKernel,
            Instructions = """
                You are a specialized healthcare insurance FAQ agent.
                You can answer questions about insurance coverage, reimbursements, eligibility, and common healthcare queries.
                
                When responding to insurance-related queries:
                1. First understand the specific question or topic the user is asking about
                2. Search for relevant information using the SearchDocuments function from SearchTool
                3. Ground your response in the search results' content
                4. Always cite the PDF sources you used in your answer
                5. If you don't have specific information, be honest about limitations
                
                KEY KNOWLEDGE AREAS:
                - Insurance coverage for different treatments and procedures
                - Eligibility requirements for reimbursements
                - Coverage differences between basic and supplementary insurance
                - Common healthcare insurance terms and policies
                
                RESPONSE FORMAT:
                - Use plain, conversational language that's easy to understand
                - Avoid technical jargon when possible, or explain it when necessary
                - Structure complex answers with bullet points for clarity
                - Be concise but thorough in your explanations
                - IMPORTANT: Always include the source PDF documents you referenced in your answer
                - ALWAYS include a "References:" section at the end with the list of PDF documents used in this exact format
                - Format the sources as: "References: Document1.pdf, Document2.pdf, Document3.pdf"
            """,
            // TODO: Exercise 4 - Pass the settings to the Arguments property
            Arguments = new KernelArguments(settings)
        };
    }
}