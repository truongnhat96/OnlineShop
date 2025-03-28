using System.Text.Json.Serialization;

namespace Infrastructure.Models.Caching
{
    [JsonConverter(typeof(JsonStringEnumConverter<CacheTypes>))]
    public enum CacheTypes
    {
        Memory,
        Redis
    }
}
