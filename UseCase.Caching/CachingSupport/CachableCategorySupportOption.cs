namespace UseCase.Caching.CachingSupport
{
    public class CachableCategorySupportOption
    {
        public string CacheKey { get; set; } = "CACHE_Category";
        public TimeSpan CacheLifeTime { get; set; } = TimeSpan.FromHours(45);
    }
}