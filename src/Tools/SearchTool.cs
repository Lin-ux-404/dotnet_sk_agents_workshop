using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgentsDemoSK.Agents;
using AgentsDemoSK.Tracing;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AgentsDemoSK.Tools;

public class SearchTool : ITool
{
    private readonly SearchClient _searchClient;
    private readonly int _topK;
    private readonly GenAITracer? _genAITracer;
    private readonly ILogger<SearchTool> _logger;
    private static string _currentChatId = string.Empty;
    private static string _currentAgentName = string.Empty;

    public string Name => "SearchTool";

    public SearchTool(IConfiguration configuration, GenAITracer? genAITracer = null, ILogger<SearchTool>? logger = null)
    {
        var settings = configuration.GetSection("AzureSearchSettings");
        string endpoint = settings["ServiceEndpoint"] ?? throw new ArgumentNullException("AzureSearchSettings:ServiceEndpoint not configured");
        string key = settings["Key"] ?? throw new ArgumentNullException("AzureSearchSettings:Key not configured");
        string indexName = settings["IndexName"] ?? throw new ArgumentNullException("AzureSearchSettings:IndexName not configured");

        if (!int.TryParse(settings["TopK"], out _topK))
        {
            _topK = 5; 
        }

        _searchClient = new SearchClient(
            new Uri(endpoint),
            indexName,
            new AzureKeyCredential(key));
            
        _genAITracer = genAITracer;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Sets the context for the current search operation
    /// </summary>
    public static void SetContext(string chatId, string agentName)
    {
        _currentChatId = chatId;
        _currentAgentName = agentName;
    }

    /// Searches documents using hybrid search (keyword + vector) and grounds responses using RAG.
    [KernelFunction, Description("Searches documents using hybrid search (keyword + vector) and grounds responses using RAG.")]
    public async Task<string> SearchDocuments(
        [Description("The question to search for")] string query,
        [Description("Number of vector neighbors")] int kNearestNeighbors = 50,
        [Description("Number of top results to return")] int topResults = 5)
    {
        Activity? searchActivity = null;
        
        // Start tracing the search tool invocation if tracer is available
        if (_genAITracer != null && !string.IsNullOrEmpty(_currentChatId) && !string.IsNullOrEmpty(_currentAgentName))
        {
            searchActivity = _genAITracer.StartToolInvocation(
                _currentChatId,
                _currentAgentName,
                "SearchTool",
                query);
        }
        
        try
        {
            _logger.LogInformation($"Searching for '{query}' with {topResults} results");
            
            // Create vector search config
            var vectorSearchOptions = new VectorSearchOptions
            {
                Queries = { 
                    // Use VectorizableTextQuery for integrated vectorization instead of VectorizedQuery with raw vectors
                    new VectorizableTextQuery(query)
                    { 
                        KNearestNeighborsCount = kNearestNeighbors,
                        Fields = { "text_vector" }
                    }
                }
            };
            
            // Perform hybrid search
            var searchOptions = new SearchOptions
            {
                VectorSearch = vectorSearchOptions,
                Size = topResults,
                Select = { "title", "chunk" }
            };
            
            var searchResponse = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions);
            var searchResults = searchResponse.Value;
            
            // Format sources for RAG
            var sources = new List<Dictionary<string, string>>();
            var usedDocuments = new List<string>();
            
            foreach (var searchResult in searchResults.GetResults())
            {
                if (searchResult.Document.TryGetValue("title", out object? titleObj) && 
                    searchResult.Document.TryGetValue("chunk", out object? contentObj))
                {
                    string title = titleObj?.ToString() ?? "Untitled";
                    string content = contentObj?.ToString() ?? "";
                    
                    var sourceInfo = new Dictionary<string, string>
                    {
                        ["title"] = title,
                        ["content"] = content
                    };
                    
                    sources.Add(sourceInfo);
                    
                    // Keep track of documents used for reference
                    if (!string.IsNullOrEmpty(title) && !usedDocuments.Contains(title))
                    {
                        usedDocuments.Add(title);
                    }
                }
            }
            
            // Add document references to the orchestrator
            OrchestratorAgent.AddDocumentReferences(usedDocuments);
            
            // Store search query and results in the tracker for tracing
            AgentsDemoSK.Tracing.SearchTracker.SetSearchInfo(query, usedDocuments);
            
            // Format the result
            var result = new
            {
                query,
                references = sources.ConvertAll(src => new
                {
                    title = src["title"],
                    content = src["content"]
                }),
                source_references = usedDocuments
            };
            
            string resultJson = JsonSerializer.Serialize(result);
            
            // Complete the search activity if started
            if (searchActivity != null)
            {
                var shortResult = new
                {
                    query,
                    documents_found = usedDocuments.Count,
                    document_titles = usedDocuments
                };
                
                _genAITracer?.CompleteToolInvocation(
                    searchActivity, 
                    JsonSerializer.Serialize(shortResult), 
                    true);
            }
            
            return resultJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in SearchDocuments: {ex.Message}");
            
            // Complete the search activity with error if started
            if (searchActivity != null)
            {
                _genAITracer?.CompleteToolInvocation(
                    searchActivity, 
                    $"Error: {ex.Message}", 
                    false);
            }
            
            // Return error response
            var errorResult = new
            {
                query,
                answer = $"An error occurred while searching: {ex.Message}",
                references = Array.Empty<object>()
            };
            
            return JsonSerializer.Serialize(errorResult);
        }
    }
}