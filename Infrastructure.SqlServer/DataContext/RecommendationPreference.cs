using System.ComponentModel.DataAnnotations;

namespace Infrastructure.SqlServer.DataContext;

public class RecommendationPreference
{
    [Key] public int UserId { get; set; }
    public bool Enabled { get; set; }
    public bool Chosen { get; set; }
    public int? VehicleModelId { get; set; }
    public int? Year { get; set; }
    public DateTimeOffset? VehicleConfirmedAt { get; set; }
}

public class RecommendationEvent
{
    public long Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(16)] public string Kind { get; set; } = "";
    public int? ProductId { get; set; }
    [MaxLength(300)] public string? Terms { get; set; }
    public DateTimeOffset At { get; set; }
    [MaxLength(100)] public string DedupeKey { get; set; } = "";
}
