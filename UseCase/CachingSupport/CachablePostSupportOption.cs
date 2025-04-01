namespace UseCase.CachingSupport
{
    public class CachablePostSupportOption
    {
        public string CacheKey { get; set; } = "CACHE_Post";
        public TimeSpan CacheLifeTime { get; set; } = TimeSpan.FromMinutes(95);
    }
}
