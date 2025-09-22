# Multi-Agent Workshop with Semantic Kernel and AI Foundry

This repository contains workshop materials on how to implement a multi-agent application using Semantic Kernel (SK) and AI Foundry. The workshop showcases how to build a healthcare agent system with an orchestrator agent and specialized sub-agents for handling insurance questions and administrative tasks.

## Workshop Content

The workshop consists of two main components:

1. **Jupyter Notebook**: An introductory notebook (`notebooks/01_SK_Basics.ipynb`) that demonstrates the fundamentals of Semantic Kernel in .NET.

2. **Sample Application**: A complete multi-agent healthcare application in the `src` directory that implements the concepts covered in the notebook.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) (v14 or later) for the frontend
- [Visual Studio Code](https://code.visualstudio.com/) with [Polyglot Notebook extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode) for the notebooks
- Azure OpenAI API access or OpenAI API key
- Azure Cognitive Services account for Language Understanding and Search (if using those features)

## Important: Configure AppSettings

**Before running the application**, you must update the `appsettings.json` file in the `src` directory with your own API keys and endpoints. The following sections need to be configured:

```json
{
  "AzureOpenAISettings": {
    "ModelDeploymentName": "o4-mini",
    "Endpoint": "",
    "ApiKey": "",
    "ApiVersion": ""
  },
  "AzureLanguageSettings": {
    "Endpoint": "",
    "Key": "",
    "ModelDeployment": "",
    "ApiVersion": ""
  },
  "AzureSearchSettings": {
    "ServiceEndpoint": "",
    "Key": "",
    "IndexName": "",
    "TopK": 5
  },
  "ApplicationInsights": {
    "ConnectionString": ""
  },
}
```

## Running the Backend

To run the backend application:

1. Make sure you've updated the `appsettings.json` file as described above
2. Navigate to the project directory
3. Run the application using:

```powershell
cd src
dotnet build
dotnet run
```

The API will start running at https://localhost:5001 (and http://localhost:5000).

## Running the Frontend

To run the React frontend:

1. Navigate to the frontend directory:
   ```powershell
   cd frontend
   ```

2. Install dependencies:
   ```powershell
   npm install
   ```

3. Start the frontend application:
   ```powershell
   npm start
   ```

The frontend will be available at http://localhost:3000

## Features

- **Orchestrator Agent**: Coordinates between specialized agents
- **FAQ Agent**: Answers insurance-related questions using document search
- **Admin Agent**: Handles appointments, scheduling, and complaints
- **Intent Recognition**: Using Azure Language Understanding
- **Document Search**: Using Azure Cognitive Search
- **Email Notifications**: Mock email capability for confirmations

## API Endpoints

### Chat Endpoint

**POST /api/chat**

Send a message to the healthcare agent system.

Request Body:
```json
{
  "message": "What is covered by my insurance?"
}
```

Response:
```json
{
  "message": "Your insurance covers...",
  "agentsUsed": ["OrchestratorAgent", "FAQAgent"],
  "references": ["Insurance_Coverage.pdf", "Policy_Document.pdf"]
}
```

## Testing the Implementation

You can test the API using:

1. **Swagger UI**: Navigate to https://localhost:5001/swagger
2. **Frontend Application**: Use the React frontend at http://localhost:3000
3. **Postman/Curl**: Send requests directly to the API

Example curl command:
```powershell
curl -X POST "https://localhost:5001/api/chat" -H "Content-Type: application/json" -d "{ \"message\": \"What is covered by my insurance?\" }"
```

## Multi-Agent Architecture

The application demonstrates a hierarchical agent structure:

- **Orchestrator Agent**: Analyzes user intent and delegates to appropriate sub-agents
- **FAQ Agent**: Answers insurance-related questions using document search
- **Admin Agent**: Handles appointments, scheduling, and complaints

Each agent is specialized and uses different tools to accomplish its tasks.

## References

- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [SK Chat Agent Example](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/examples/example-chat-agent?pivots=programming-language-csharp)
- [SK Quick Start Guide](https://learn.microsoft.com/en-us/semantic-kernel/get-started/quick-start-guide?pivots=programming-language-csharp)
- [Semantic Kernel GitHub Repository](https://github.com/microsoft/semantic-kernel)
- [SK Agent Functions](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-functions?pivots=programming-language-csharp)
- [AI Foundry Agent Tracing](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/trace-agents-sdk)
- [Chat Completion Agent](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-types/chat-completion-agent?pivots=programming-language-csharp)
- [Agent Orchestration and Handoff](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/handoff?pivots=programming-language-csharp)