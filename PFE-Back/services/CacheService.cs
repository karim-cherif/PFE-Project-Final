using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace PFE_Back.services
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task RemoveByPatternAsync(string pattern);
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class;
    }

    /// <summary>
    /// High-performance caching service with memory and distributed cache support
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly ILogger<CacheService> _logger;
        private readonly CacheOptions _options;

        public CacheService(
            IMemoryCache memoryCache,
            ILogger<CacheService> logger,
            CacheOptions options,
            IDistributedCache? distributedCache = null)
        {
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
            _logger = logger;
            _options = options;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                // Try memory cache first (fastest)
                if (_memoryCache.TryGetValue(key, out T? memoryValue))
                {
                    _logger.LogDebug("Cache hit (memory): {Key}", key);
                    return memoryValue;
                }

                // Try distributed cache if available
                if (_distributedCache != null)
                {
                    var distributedValue = await _distributedCache.GetStringAsync(key);
                    if (!string.IsNullOrEmpty(distributedValue))
                    {
                        var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue);

                        // Store in memory cache for faster access
                        _memoryCache.Set(key, deserializedValue, TimeSpan.FromMinutes(_options.MemoryCacheMinutes));

                        _logger.LogDebug("Cache hit (distributed): {Key}", key);
                        return deserializedValue;
                    }
                }

                _logger.LogDebug("Cache miss: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var exp = expiration ?? TimeSpan.FromMinutes(_options.DefaultExpirationMinutes);

                // Set in memory cache
                _memoryCache.Set(key, value, exp);

                // Set in distributed cache if available
                if (_distributedCache != null)
                {
                    var serializedValue = JsonSerializer.Serialize(value);
                    var distributedOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = exp
                    };
                    await _distributedCache.SetStringAsync(key, serializedValue, distributedOptions);
                }

                _logger.LogDebug("Cache set: {Key} (expires in {Expiration})", key, exp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);

                if (_distributedCache != null)
                {
                    await _distributedCache.RemoveAsync(key);
                }

                _logger.LogDebug("Cache removed: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
            }
        }

        public Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                // For memory cache, we need to track keys (simplified implementation)
                // In production, consider using a more sophisticated approach

                _logger.LogDebug("Cache pattern removal: {Pattern}", pattern);

                // This is a simplified implementation
                // For production, implement proper pattern-based cache invalidation
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache values by pattern: {Pattern}", pattern);
                return Task.CompletedTask;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class
        {
            var cachedValue = await GetAsync<T>(key);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            var value = await getItem();
            if (value != null)
            {
                await SetAsync(key, value, expiration);
            }

            return value;
        }
    }

    public class CacheOptions
    {
        public int DefaultExpirationMinutes { get; set; } = 30;
        public int MemoryCacheMinutes { get; set; } = 5;
        public bool EnableDistributedCache { get; set; } = false;
    }

    /// <summary>
    /// Cache key generator for consistent cache key naming
    /// </summary>
    public static class CacheKeys
    {
        public static string UserDocuments(Guid userId) => $"user_documents_{userId}";
        public static string DocumentDetails(Guid documentId) => $"document_{documentId}";
        public static string UserProfile(Guid userId) => $"user_profile_{userId}";
        public static string DocumentStats(Guid userId) => $"document_stats_{userId}";
        public static string OcrStatistics(Guid userId) => $"ocr_stats_{userId}";
        public static string SystemStats() => "system_stats";
        public static string DocumentTypeDistribution(Guid userId) => $"doc_type_dist_{userId}";
    }

    /// <summary>
    /// Cache invalidation service for maintaining cache consistency
    /// </summary>
    public interface ICacheInvalidationService
    {
        Task InvalidateUserCacheAsync(Guid userId);
        Task InvalidateDocumentCacheAsync(Guid documentId, Guid userId);
        Task InvalidateSystemCacheAsync();
    }

    public class CacheInvalidationService : ICacheInvalidationService
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheInvalidationService> _logger;

        public CacheInvalidationService(ICacheService cacheService, ILogger<CacheInvalidationService> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task InvalidateUserCacheAsync(Guid userId)
        {
            try
            {
                await _cacheService.RemoveAsync(CacheKeys.UserDocuments(userId));
                await _cacheService.RemoveAsync(CacheKeys.UserProfile(userId));
                await _cacheService.RemoveAsync(CacheKeys.DocumentStats(userId));
                await _cacheService.RemoveAsync(CacheKeys.OcrStatistics(userId));
                await _cacheService.RemoveAsync(CacheKeys.DocumentTypeDistribution(userId));

                _logger.LogDebug("Invalidated cache for user: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating user cache: {UserId}", userId);
            }
        }

        public async Task InvalidateDocumentCacheAsync(Guid documentId, Guid userId)
        {
            try
            {
                await _cacheService.RemoveAsync(CacheKeys.DocumentDetails(documentId));
                await _cacheService.RemoveAsync(CacheKeys.UserDocuments(userId));
                await _cacheService.RemoveAsync(CacheKeys.DocumentStats(userId));
                await _cacheService.RemoveAsync(CacheKeys.DocumentTypeDistribution(userId));

                _logger.LogDebug("Invalidated cache for document: {DocumentId}", documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating document cache: {DocumentId}", documentId);
            }
        }

        public async Task InvalidateSystemCacheAsync()
        {
            try
            {
                await _cacheService.RemoveAsync(CacheKeys.SystemStats());

                _logger.LogDebug("Invalidated system cache");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating system cache");
            }
        }
    }
}
