using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace AgentsDemoSK.Tracing
{
    public class GenAITracer
    {
        private readonly ActivitySource _activitySource;
        private const string GenAiSourceName = "AgentsDemoSK.GenAI";
        private readonly ILogger<GenAITracer> _logger;
        
        // Dictionary to track which tools are used by which agent
        private readonly Dictionary<string, string> _agentToolMap = new Dictionary<string, string>();
        
        // Common tag names to ensure consistency
        private static class Tags
        {
            public const string System = "gen_ai.system";
            public const string ThreadId = "gen_ai.thread.id";
            public const string AgentName = "gen_ai.agent.name";
            public const string ToolName = "gen_ai.tool.name";
            public const string ToolUsed = "gen_ai.tool_used";
            public const string ToolInput = "gen_ai.tool.input";
            public const string ToolOutput = "gen_ai.tool.output";
            public const string Prompt = "gen_ai.prompt";
            public const string Query = "gen_ai.query";
            public const string Response = "gen_ai.response";
            public const string Intent = "gen_ai.intent";
            public const string Success = "gen_ai.success";
        }
        
        public GenAITracer(ILogger<GenAITracer> logger)
        {
            _activitySource = new ActivitySource(GenAiSourceName, "1.0.0");
            _logger = logger;
        }

        public void TraceUserMessage(string chatId, string message, string userId = "anonymous")
        {
            var content = new Dictionary<string, object>
            {
                ["message"] = new Dictionary<string, object>
                {
                    ["role"] = "user",
                    ["content"] = new Dictionary<string, object>
                    {
                        ["text"] = new Dictionary<string, object>
                        {
                            ["value"] = message
                        }
                    }
                }
            };
            
            var tags = new Dictionary<string, object>
            {
                ["gen_ai.user.id"] = userId,
                ["gen_ai.event.content"] = System.Text.Json.JsonSerializer.Serialize(content)
            };
            
            using var activity = StartActivity("gen_ai.user.message", chatId, tags);
        }
        
        public void TraceAssistantMessage(string chatId, string responseId, string message, double tokensInput = 0, double tokensOutput = 0)
        {
            var content = new Dictionary<string, object>
            {
                ["message"] = new Dictionary<string, object>
                {
                    ["role"] = "assistant",
                    ["content"] = new Dictionary<string, object>
                    {
                        ["text"] = new Dictionary<string, object>
                        {
                            ["value"] = message
                        }
                    }
                }
            };
            
            var tags = new Dictionary<string, object>
            {
                ["gen_ai.response.id"] = responseId,
                ["gen_ai.usage.input_tokens"] = tokensInput,
                ["gen_ai.usage.output_tokens"] = tokensOutput,
                ["gen_ai.event.content"] = System.Text.Json.JsonSerializer.Serialize(content)
            };
            
            using var activity = StartActivity("gen_ai.choice", chatId, tags);
        }

        public void TraceSystemMessage(string chatId, string message)
        {
            var content = new Dictionary<string, object>
            {
                ["message"] = new Dictionary<string, object>
                {
                    ["role"] = "system",
                    ["content"] = new Dictionary<string, object>
                    {
                        ["text"] = new Dictionary<string, object>
                        {
                            ["value"] = message
                        }
                    }
                }
            };
            
            var tags = new Dictionary<string, object>
            {
                ["gen_ai.event.content"] = System.Text.Json.JsonSerializer.Serialize(content)
            };
            
            using var activity = StartActivity("gen_ai.system.message", chatId, tags);
        }
        
 
        private Activity? StartActivity(string operationName, string chatId, Dictionary<string, object>? additionalTags = null)
        {
            // Determine the appropriate activity kind based on the operation name
            ActivityKind kind = ActivityKind.Internal;
            if (operationName.StartsWith("gen_ai.user.") || 
                operationName.StartsWith("gen_ai.choice") ||
                operationName.StartsWith("gen_ai.system.") ||
                operationName.StartsWith("gen_ai.evaluation.") ||
                operationName.StartsWith("gen_ai.conversation."))
            {
                // These are typically server-side activities that represent external interactions
                kind = ActivityKind.Server;
            }
            
            var activity = _activitySource.StartActivity(operationName, kind);
            if (activity != null)
            {
                activity.SetTag(Tags.System, "AgentsDemoSK");
                activity.SetTag(Tags.ThreadId, chatId);
                
                if (additionalTags != null)
                {
                    foreach (var tag in additionalTags)
                    {
                        activity.SetTag(tag.Key, tag.Value);
                    }
                }
            }
            return activity;
        }
        
        public Activity? StartAgentInvocation(string chatId, string agentName, string prompt)
        {
            var tags = new Dictionary<string, object>
            {
                [Tags.AgentName] = agentName,
                [Tags.Prompt] = prompt
            };
            
            return StartActivity($"gen_ai.agent.{agentName}", chatId, tags);
        }

        private void CompleteActivity(Activity? activity, string output, bool success = true, Dictionary<string, object>? additionalTags = null)
        {
            if (activity == null) return;
            
            activity.SetTag(Tags.Response, output);
            activity.SetTag(Tags.Success, success);
            
            if (additionalTags != null)
            {
                foreach (var tag in additionalTags)
                {
                    activity.SetTag(tag.Key, tag.Value);
                }
            }
            
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            activity.Stop();
        }
        

        public void RegisterAgentTool(string agentName, string toolName)
        {
            if (!string.IsNullOrEmpty(agentName) && !string.IsNullOrEmpty(toolName))
            {
                _agentToolMap[agentName] = toolName;
                _logger?.LogDebug("Registered tool {ToolName} for agent {AgentName}", toolName, agentName);
            }
        }

        public void CompleteAgentInvocation(Activity? activity, string response, bool success = true)
        {
            if (activity != null)
            {
                // Extract agent name from operation name (format: gen_ai.agent.AgentName)
                string agentName = activity.OperationName.Replace("gen_ai.agent.", "");
                
                // Extract chat ID from ThreadId tag
                string chatId = "default";
                if (activity.Tags.Any(t => t.Key == Tags.ThreadId))
                {
                    chatId = activity.Tags.First(t => t.Key == Tags.ThreadId).Value?.ToString() ?? "default";
                }
                
                var tags = new Dictionary<string, object>
                {
                    [Tags.AgentName] = agentName
                };
                
                // Add tool information if available from dynamic registration
                if (_agentToolMap.TryGetValue(agentName, out var toolName))
                {
                    tags[Tags.ToolUsed] = true;
                    tags[Tags.ToolName] = toolName;
                    
                    // For the OrchestratorAgent specifically, check for intent tool usage
                    // and add intent information from the IntentTracker
                    if (agentName == "OrchestratorAgent" && toolName == "IntentTool")
                    {
                        // Check if the IntentTracker has data for this chat ID
                        var intentInfo = IntentTracker.GetIntentInfoJson(chatId);
                        tags["gen_ai.intent_info"] = intentInfo;
                        
                        // Log if the intent info is empty
                        if (intentInfo.Contains("\"intent\":\"\"") || intentInfo.Contains("\"intent\": \"\""))
                        {
                            _logger?.LogWarning("Intent info is empty for chatId {ChatId}", chatId);
                        }
                        else
                        {
                            _logger?.LogInformation("Successfully retrieved intent info for chatId {ChatId}", chatId);
                        }
                    }
                }
                
                // Special handling for FAQ agent
                if (activity.OperationName == "gen_ai.agent.FAQAgent")
                {
                    var searchInfo = SearchTracker.GetSearchInfoJson();
                    tags["gen_ai.search_info"] = searchInfo;
                }
                
                CompleteActivity(activity, response, success, tags);
            }
        }

        public void TraceEvaluation(string chatId, string responseId, string evaluatorName, double score, string comment = "")
        {
            var tags = new Dictionary<string, object>
            {
                ["gen_ai.response.id"] = responseId,
                ["gen_ai.evaluator.name"] = evaluatorName,
                ["gen_ai.evaluation.score"] = score
            };
            
            if (!string.IsNullOrEmpty(comment))
            {
                tags["gen_ai.evaluation.comment"] = comment;
            }
            
            using var activity = StartActivity("gen_ai.evaluation.score", chatId, tags);
        }

        /// <summary>
        /// Start tracing a tool invocation
        /// </summary>
        public Activity? StartToolInvocation(string chatId, string agentName, string toolName, string input)
        {
            var tags = new Dictionary<string, object>
            {
                [Tags.AgentName] = agentName,
                [Tags.ToolName] = toolName,
                [Tags.ToolInput] = input
            };
            
            var activity = StartActivity($"gen_ai.tool.{toolName}", chatId, tags);
            
            // Register the tool with the agent for future reference
            RegisterAgentTool(agentName, toolName);
            
            // Log tool invocation - this is important for debugging and doesn't need to be in the Activity
            _logger.LogInformation("Tool {ToolName} invoked by {AgentName} with input: {Input}", toolName, agentName, input);
            
            return activity;
        }

        public void CompleteToolInvocation(Activity? activity, string output, bool success = true)
        {
            if (activity != null)
            {
                // Extract tool name from operation name (format: gen_ai.tool.ToolName)
                string toolName = activity.OperationName.Replace("gen_ai.tool.", "");
                
                // Try to determine the agent from activity tags
                string? agentName = null;
                if (activity.Tags.Any(t => t.Key == Tags.AgentName))
                {
                    agentName = activity.Tags.First(t => t.Key == Tags.AgentName).Value?.ToString();
                    
                    // Register this agent-tool relationship if both sides are known
                    if (!string.IsNullOrEmpty(agentName) && !string.IsNullOrEmpty(toolName))
                    {
                        RegisterAgentTool(agentName, toolName);
                    }
                }
                
                var tags = new Dictionary<string, object>
                {
                    [Tags.ToolOutput] = output,
                    [Tags.ToolUsed] = true,
                    [Tags.ToolName] = toolName
                };
                
                CompleteActivity(activity, output, success, tags);
            }
        }


        public void TraceIntentDetection(string chatId, string query, string intent, double confidenceScore = 0, 
            Dictionary<string, double>? allIntents = null, List<Dictionary<string, object>>? entities = null)
        {
            // Log information about intent detection
            _logger?.LogInformation("Intent detection called for chatId '{ChatId}' with query '{Query}', detected intent: '{Intent}' (confidence: {Confidence})",
                chatId, query, intent, confidenceScore);
            
            // Store intent information in the IntentTracker for later use
            IntentTracker.SetIntentInfo(chatId, query, intent, confidenceScore, allIntents, entities);
            
            // Verify intent was stored properly
            string intentJson = IntentTracker.GetIntentInfoJson(chatId);
            if (intentJson.Contains("\"intent\":\"\"") || intentJson.Contains("\"intent\": \"\""))
            {
                _logger?.LogWarning("Failed to store intent '{Intent}' in IntentTracker for chatId '{ChatId}'", intent, chatId);
            }
            else
            {
                _logger?.LogInformation("Successfully stored intent '{Intent}' in IntentTracker for chatId '{ChatId}'", intent, chatId);
            }
            
            var tags = new Dictionary<string, object>
            {
                [Tags.Query] = query,
                [Tags.Intent] = intent,
                ["gen_ai.intent.confidence"] = confidenceScore,
                [Tags.ToolUsed] = true,
                [Tags.ToolName] = "IntentTool",
                ["gen_ai.intent_info"] = intentJson
            };
            
            // Add additional intent information if available
            if (allIntents != null && allIntents.Count > 0)
            {
                tags["gen_ai.intent.all_intents"] = System.Text.Json.JsonSerializer.Serialize(allIntents);
            }
            
            if (entities != null && entities.Count > 0)
            {
                tags["gen_ai.intent.entities"] = System.Text.Json.JsonSerializer.Serialize(entities);
            }
            
            using var activity = StartActivity("gen_ai.intent.detection", chatId, tags);
            
            // Log intent detection for easy discovery in logs
            _logger.LogInformation("Intent detected: {Intent} (confidence: {Confidence}) for query: {Query}", 
                intent, confidenceScore, query);
            
            // If we have entities, log them too
            if (entities != null && entities.Count > 0)
            {
                _logger.LogInformation("Detected {EntityCount} entities for intent {Intent}", 
                    entities.Count, intent);
            }
        }
        
        public void TraceConversationHistory(string chatId, string conversationText)
        {
            var tags = new Dictionary<string, object>
            {
                ["gen_ai.conversation.text"] = conversationText
            };
            
            using var activity = StartActivity("gen_ai.conversation.history", chatId, tags);
        }
    }
}