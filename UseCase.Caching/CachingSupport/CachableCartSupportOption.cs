namespace UseCase.Caching.CachingSupport
{
    public class CachableCartSupportOption
    {
        public string CacheKey { get; set; } = "CACHE_Cart";
        public TimeSpan CacheLifeTime { get; set; } = TimeSpan.FromHours(1);
    }
}
