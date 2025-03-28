namespace Infrastructure.Models.Caching
{
    public class CacheOption
    {
        public CacheTypes Type { get; set; } = CacheTypes.Memory;
        public CacheRedisOption? CacheRedisOptions { get; set; }
    }
}
