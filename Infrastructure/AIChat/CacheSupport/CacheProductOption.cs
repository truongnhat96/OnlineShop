namespace Infrastructure.AIChat.CacheSupport
{
    public class CacheProductOption
    {
        public string CacheKey { get; set; } = "product_cache_key";
        public TimeSpan CacheLifeTime { get; set; } = TimeSpan.FromHours(12);
    }
}
