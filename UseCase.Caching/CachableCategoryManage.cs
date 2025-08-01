using System.Text.Json;
using Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using UseCase.Business_Logic;
using UseCase.Caching.CachingSupport;
using UseCase.UnitOfWork;

namespace UseCase.Caching
{
    public class CachableCategoryManage : ICategoryManage
    {
        private readonly IProductUnitOfWork _productUnitOfWork;
        private readonly IDistributedCache _distributedCache;
        private readonly CachableCategorySupportOption _cachableCategorySupportOption;
        private readonly DistributedCacheEntryOptions _cacheEntryOptions;
        private readonly ILogger<CachableCategoryManage> _logger;

        public CachableCategoryManage(IProductUnitOfWork productUnitOfWork, IDistributedCache distributedCache, CachableCategorySupportOption cachableCategorySupportOption, ILogger<CachableCategoryManage> logger)
        {
            _productUnitOfWork = productUnitOfWork;
            _distributedCache = distributedCache;
            _cachableCategorySupportOption = cachableCategorySupportOption;
            _logger = logger;
            _cacheEntryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cachableCategorySupportOption.CacheLifeTime
            };
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            var cacheKey = _cachableCategorySupportOption.CacheKey;
            var cachedCategories = await _distributedCache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedCategories))
            {
                _logger.LogInformation("Retrieved categories from cache.");
                return JsonSerializer.Deserialize<IEnumerable<Category>>(cachedCategories) ?? new List<Category>();
            }

            var categories = await _productUnitOfWork.CategoryRepository.GetAllCategoriesAsync() ?? new List<Category>();
            if (categories != null && categories.Any())
            {
                await _distributedCache.SetStringAsync(cacheKey, JsonSerializer.Serialize(categories), _cacheEntryOptions);
                _logger.LogInformation("Cached categories successfully.");
            }
            else
            {
                _logger.LogWarning("No categories found to cache.");
            }
            return categories!;
        }

        // Implement methods from ICategoryManage interface here
    }
}