using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CAI_design_1_chat.Services
{
    public class ContextCacheService
    {
        private readonly Dictionary<int, string> _contextCache = new();
        private readonly Dictionary<int, DateTime> _cacheTimestamps = new();
        private readonly ContextObjectService _contextObjectService;
        
        // Cache expiration time (5 minutes)
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public ContextCacheService()
        {
            _contextObjectService = new ContextObjectService();
        }

        /// <summary>
        /// Get cached context JSON for a session, refreshing if needed
        /// </summary>
        public async Task<string> GetContextAsync(int sessionId)
        {
            try
            {
                // Check if cache exists and is still valid
                if (_contextCache.ContainsKey(sessionId) && IsCacheValid(sessionId))
                {
                    Console.WriteLine($"Using cached context for session {sessionId}");
                    return _contextCache[sessionId];
                }

                // Cache miss or expired - refresh
                await RefreshContextAsync(sessionId);
                return _contextCache[sessionId];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting context for session {sessionId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Force refresh context for a session (called when data changes)
        /// </summary>
        public async Task InvalidateContextAsync(int sessionId)
        {
            try
            {
                Console.WriteLine($"Invalidating context cache for session {sessionId}");
                
                // Remove from cache
                _contextCache.Remove(sessionId);
                _cacheTimestamps.Remove(sessionId);
                
                // Refresh immediately
                await RefreshContextAsync(sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error invalidating context for session {sessionId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Check if a session has cached context
        /// </summary>
        public bool HasCachedContext(int sessionId)
        {
            return _contextCache.ContainsKey(sessionId) && IsCacheValid(sessionId);
        }

        /// <summary>
        /// Clear all cached contexts (useful for memory management)
        /// </summary>
        public void ClearAllCache()
        {
            var sessionCount = _contextCache.Count;
            _contextCache.Clear();
            _cacheTimestamps.Clear();
            Console.WriteLine($"Cleared context cache for {sessionCount} sessions");
        }

        /// <summary>
        /// Get cache statistics for monitoring
        /// </summary>
        public object GetCacheStats()
        {
            var validCaches = 0;
            var expiredCaches = 0;

            foreach (var sessionId in _contextCache.Keys)
            {
                if (IsCacheValid(sessionId))
                    validCaches++;
                else
                    expiredCaches++;
            }

            return new
            {
                total_cached_sessions = _contextCache.Count,
                valid_caches = validCaches,
                expired_caches = expiredCaches,
                cache_expiration_minutes = _cacheExpiration.TotalMinutes
            };
        }

        /// <summary>
        /// Refresh context for a specific session
        /// </summary>
        private async Task RefreshContextAsync(int sessionId)
        {
            try
            {
                Console.WriteLine($"Refreshing context for session {sessionId}");
                
                var contextJson = await _contextObjectService.BuildContextJsonAsync(sessionId);
                
                // Update cache
                _contextCache[sessionId] = contextJson;
                _cacheTimestamps[sessionId] = DateTime.Now;
                
                Console.WriteLine($"Context refreshed for session {sessionId}: {contextJson.Length} characters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing context for session {sessionId}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Check if cached context is still valid (not expired)
        /// </summary>
        private bool IsCacheValid(int sessionId)
        {
            if (!_cacheTimestamps.ContainsKey(sessionId))
                return false;

            var cacheAge = DateTime.Now - _cacheTimestamps[sessionId];
            return cacheAge < _cacheExpiration;
        }
    }
}
