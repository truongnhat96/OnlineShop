using System.ComponentModel.DataAnnotations;

namespace Infrastructure.SqlServer.DataContext;

public class VehicleModel
{
    public int Id { get; set; }
    [MaxLength(50)] public string Brand { get; set; } = "";
    [MaxLength(100)] public string Model { get; set; } = "";
    [MaxLength(100)] public string Variant { get; set; } = "";
    [MaxLength(20)] public string Market { get; set; } = "VN";
    [MaxLength(500)] public string Aliases { get; set; } = "";
    public virtual ICollection<ProductFitment> Fitments { get; set; } = [];
}

public class ProductFitment
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int VehicleModelId { get; set; }
    public int YearFrom { get; set; }
    public int YearTo { get; set; }
    [MaxLength(2048)] public string EvidenceUrl { get; set; } = "";
    [MaxLength(1000)] public string? Notes { get; set; }
    public virtual Product Product { get; set; } = null!;
    public virtual VehicleModel VehicleModel { get; set; } = null!;
}
