using System.Collections.Generic;
using System.Text.Json;

namespace AgentsDemoSK.Tracing
{
    public static class SearchTracker
    {
        private static string? _currentQuery = null;
        private static List<string>? _currentDocuments = null;
        
        public static void SetSearchInfo(string query, List<string> documents)
        {
            _currentQuery = query;
            _currentDocuments = new List<string>(documents);
        }
        
        public static string GetSearchInfoJson()
        {
            var searchInfo = new
            {
                query = _currentQuery ?? string.Empty,
                documents = _currentDocuments ?? new List<string>()
            };
            
            return JsonSerializer.Serialize(searchInfo);
        }
    }
}