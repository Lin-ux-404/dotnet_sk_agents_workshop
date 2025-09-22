using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AgentsDemoSK.Tracing
{
 
    public static class IntentTracker
    {
        // Use thread-safe collections to store by conversation ID
        private static readonly ConcurrentDictionary<string, string> _queries = new();
        private static readonly ConcurrentDictionary<string, string> _intents = new();
        private static readonly ConcurrentDictionary<string, double> _confidenceScores = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, double>> _allIntents = new();
        private static readonly ConcurrentDictionary<string, List<Dictionary<string, object>>> _entities = new();
        
        // Store the last processed values (backward compatibility)
        private static string? _lastChatId = null;
        private static string? _lastQuery = null;
        private static string? _lastIntent = null;
        private static double _lastConfidenceScore = 0;
        
        public static void SetIntentInfo(
            string chatId,
            string query,
            string intent, 
            double confidenceScore, 
            Dictionary<string, double>? allIntents = null, 
            List<Dictionary<string, object>>? entities = null)
        {
            // Store by conversation ID
            _queries[chatId] = query;
            _intents[chatId] = intent;
            _confidenceScores[chatId] = confidenceScore;
            
            // Clone collections to avoid reference issues
            if (allIntents != null)
            {
                _allIntents[chatId] = new Dictionary<string, double>(allIntents);
            }
            
            if (entities != null)
            {
                _entities[chatId] = new List<Dictionary<string, object>>(entities);
            }

            _lastChatId = chatId;
            _lastQuery = query;
            _lastIntent = intent;
            _lastConfidenceScore = confidenceScore;
        }
        
        public static void SetIntentInfo(
            string query,
            string intent, 
            double confidenceScore, 
            Dictionary<string, double>? allIntents = null, 
            List<Dictionary<string, object>>? entities = null)
        {
            // Use a default key for backward compatibility
            SetIntentInfo("default", query, intent, confidenceScore, allIntents, entities);
        }

        public static string GetIntentInfoJson(string chatId)
        {
            _queries.TryGetValue(chatId, out var query);
            _intents.TryGetValue(chatId, out var intent);
            _confidenceScores.TryGetValue(chatId, out var confidence);
            _allIntents.TryGetValue(chatId, out var allIntents);
            _entities.TryGetValue(chatId, out var entities);
            
            var intentInfo = new
            {
                query = query ?? string.Empty,
                intent = intent ?? string.Empty,
                confidence = confidence,
                allIntents = allIntents ?? new Dictionary<string, double>(),
                entities = entities ?? new List<Dictionary<string, object>>()
            };
            
            return JsonSerializer.Serialize(intentInfo);
        }
        
        public static string GetIntentInfoJson()
        {
            return GetIntentInfoJson(_lastChatId ?? "default");
        }
        
        public static void Clear(string chatId)
        {
            _queries.TryRemove(chatId, out _);
            _intents.TryRemove(chatId, out _);
            _confidenceScores.TryRemove(chatId, out _);
            _allIntents.TryRemove(chatId, out _);
            _entities.TryRemove(chatId, out _);
            
            // Reset last values if they match the cleared chat ID
            if (_lastChatId == chatId)
            {
                _lastChatId = null;
                _lastQuery = null;
                _lastIntent = null;
                _lastConfidenceScore = 0;
            }
        }

        public static void Clear()
        {
            _queries.Clear();
            _intents.Clear();
            _confidenceScores.Clear();
            _allIntents.Clear();
            _entities.Clear();
            
            _lastChatId = null;
            _lastQuery = null;
            _lastIntent = null;
            _lastConfidenceScore = 0;
        }
    }
}