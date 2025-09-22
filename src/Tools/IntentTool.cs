using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgentsDemoSK.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AgentsDemoSK.Tools;

/// <summary>
/// Tool for recognizing user intent using Azure Language Service
/// </summary>
public class IntentTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _key;
    private readonly string _apiVersion;
    private readonly string _modelDeployment;
    private readonly ILogger<IntentTool>? _logger;
    private readonly GenAITracer? _genAITracer;
    private static string _currentChatId = string.Empty;
    
    // Simple static fields to store the latest detected intent and confidence
    private static string _lastDetectedIntent = string.Empty;
    private static double _lastConfidenceScore = 0;

    public string Name => "IntentTool";
    
    // Static properties to access the last detected intent
    public static string LastDetectedIntent => _lastDetectedIntent;
    public static double LastConfidenceScore => _lastConfidenceScore;

    public IntentTool(IConfiguration configuration)
    {
        _httpClient = new HttpClient();
        
        // Get configuration values
        var settings = configuration.GetSection("AzureLanguageSettings");
        _endpoint = settings["Endpoint"] ?? throw new ArgumentNullException("AzureLanguageSettings:Endpoint not configured");
        _key = settings["Key"] ?? throw new ArgumentNullException("AzureLanguageSettings:Key not configured");
        _apiVersion = settings["ApiVersion"] ?? "2024-11-15-preview";
        _modelDeployment = settings["ModelDeployment"] ?? throw new ArgumentNullException("AzureLanguageSettings:ModelDeployment not configured");
        
        // Try to get logger and GenAITracer from the DI container
        var serviceProvider = configuration as IServiceProvider;
        _logger = serviceProvider?.GetService<ILogger<IntentTool>>();
        _genAITracer = serviceProvider?.GetService<GenAITracer>();
    }
    

    public static void SetContext(string chatId)
    {
        _currentChatId = chatId;
    }

    [KernelFunction, Description("Recognizes the intent and entities from the user's query using Azure Language Service.")]
    public async Task<string> RecognizeIntent(
        [Description("The user's query to analyze")] string query, 
        [Description("Language code (default: nl for Dutch)")] string language = "nl")
    {
        Activity? intentActivity = null;
        
        // Start tool invocation trace using GenAITracer
        if (_genAITracer != null && !string.IsNullOrEmpty(_currentChatId))
        {
            intentActivity = _genAITracer.StartToolInvocation(
                _currentChatId,
                "OrchestratorAgent", // Default agent name
                "IntentTool",
                query);
        }
        else
        {
            // Fallback to basic logging if tracer isn't available
            _logger?.LogInformation("Processing intent for query: {query}", query);
        }

        try
        {
            // Call Azure Language Service
            var result = await AnalyzeConversation(query, language);
            
            // Try to parse the result to extract the top intent
            string topIntent = "unknown";
            double confidenceScore = 0;
            Dictionary<string, double> allIntents = new Dictionary<string, double>();
            List<Dictionary<string, object>> entities = new List<Dictionary<string, object>>();
            
            try
            {
                var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
                if (jsonResult.TryGetProperty("result", out var resultProp) &&
                    resultProp.TryGetProperty("prediction", out var predictionProp))
                {
                    // Extract top intent
                    if (predictionProp.TryGetProperty("topIntent", out var topIntentProp))
                    {
                        topIntent = topIntentProp.GetString() ?? "unknown";
                    }
                    
                    // Extract all intents and their confidence scores
                    if (predictionProp.TryGetProperty("intents", out var intentsArray))
                    {
                        foreach (var intentElement in intentsArray.EnumerateArray())
                        {
                            if (intentElement.TryGetProperty("category", out var categoryProp) &&
                                intentElement.TryGetProperty("confidenceScore", out var scoreProp))
                            {
                                string intentName = categoryProp.GetString() ?? "unknown";
                                double score = scoreProp.GetDouble();
                                
                                allIntents[intentName] = score;
                                
                                // Use top intent's confidence score
                                if (intentName == topIntent)
                                {
                                    confidenceScore = score;
                                }
                            }
                        }
                    }
                    
                    // Extract entities if available
                    if (predictionProp.TryGetProperty("entities", out var entitiesArray))
                    {
                        foreach (var entityElement in entitiesArray.EnumerateArray())
                        {
                            var entityDict = new Dictionary<string, object>();
                            
                            if (entityElement.TryGetProperty("category", out var entityCategory))
                            {
                                entityDict["category"] = entityCategory.GetString() ?? "";
                            }
                            
                            if (entityElement.TryGetProperty("text", out var entityText))
                            {
                                entityDict["text"] = entityText.GetString() ?? "";
                            }
                            
                            if (entityElement.TryGetProperty("confidenceScore", out var entityScore))
                            {
                                entityDict["confidenceScore"] = entityScore.GetDouble();
                            }
                            
                            entities.Add(entityDict);
                        }
                    }
                    
                    // Store the last detected intent information for static access
                    _lastDetectedIntent = topIntent;
                    _lastConfidenceScore = confidenceScore;
                    
                    // Always directly update the IntentTracker first for reliable intent tracking
                    IntentTracker.SetIntentInfo(_currentChatId, query, topIntent, confidenceScore, allIntents, entities);
                    
                }
            }
            catch (Exception ex)
            {
                // Just log parsing error but continue
                _logger?.LogWarning("Error parsing intent result: {errorMessage}", ex.Message);
            }
            
            // Add intent information to the activity
            if (intentActivity != null)
            {
                intentActivity.SetTag("gen_ai.intent", topIntent);
                intentActivity.SetTag("gen_ai.intent.confidence", confidenceScore);
                
                // Add all intents as a serialized object
                if (allIntents.Count > 0)
                {
                    intentActivity.SetTag("gen_ai.intent.all_intents", JsonSerializer.Serialize(allIntents));
                }
                
                // Add entities as a serialized object if any were found
                if (entities.Count > 0)
                {
                    intentActivity.SetTag("gen_ai.intent.entities", JsonSerializer.Serialize(entities));
                }
            }
            
            // Complete the intent activity successfully
            if (intentActivity != null && _genAITracer != null)
            {
                _genAITracer.CompleteToolInvocation(intentActivity, result, true);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            // Log the error
            _logger?.LogError(ex, "Error calling intent service: {errorMessage}", ex.Message);
            
            // Complete the intent activity with error
            if (intentActivity != null && _genAITracer != null)
            {
                _genAITracer.CompleteToolInvocation(intentActivity, $"Error: {ex.Message}", false);
            }
            
            // Use fallback intent
            string fallbackIntent = "informatieVergoedingen";
            double fallbackScore = 0.5;
            
            // Store fallback values in the static fields
            _lastDetectedIntent = fallbackIntent;
            _lastConfidenceScore = fallbackScore;
            
            // Create a dictionary with the fallback intent
            var fallbackIntents = new Dictionary<string, double>
            {
                { fallbackIntent, fallbackScore }
            };
            
            // Always update the IntentTracker even in the fallback case
            IntentTracker.SetIntentInfo(_currentChatId, query, fallbackIntent, fallbackScore, fallbackIntents, new List<Dictionary<string, object>>());
            
            // Provide a fallback response
            var fallbackResponse = new
            {
                kind = "ConversationResult",
                result = new
                {
                    query = query,
                    prediction = new
                    {
                        topIntent = fallbackIntent,
                        projectKind = "Conversation",
                        intents = new[]
                        {
                            new { category = fallbackIntent, confidenceScore = fallbackScore }
                        },
                        entities = Array.Empty<object>()
                    }
                }
            };

            return JsonSerializer.Serialize(fallbackResponse);
        }
    }

    private async Task<string> AnalyzeConversation(string text, string language = "nl")
    {
        // Full URL with query parameters
        string url = $"{_endpoint}/language/:analyze-conversations?api-version={_apiVersion}";
        
        // Headers
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _key);
        
        // Request body
        var payload = new
        {
            kind = "Conversation",
            analysisInput = new
            {
                conversationItem = new
                {
                    id = "user1",
                    text = text,
                    modality = "text",
                    language = language,
                    participantId = "user1"
                }
            },
            parameters = new
            {
                projectName = "test",
                verbose = true,
                deploymentName = _modelDeployment,
                stringIndexType = "TextElement_V8"
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        
        // Make the POST request
        var response = await _httpClient.PostAsync(url, content);
        
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            throw new InvalidOperationException("Empty response received from the API");
        }
        
        return responseContent;
    }
}