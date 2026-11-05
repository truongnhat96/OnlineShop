namespace UseCase.Recommendations;

public enum FitmentKind { Unknown = 0, VehicleIndependent = 1, Verified = 2 }

public sealed class VehicleOption
{
    public int Id { get; set; }
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Variant { get; set; } = "";
    public string Market { get; set; } = "VN";
    public string Aliases { get; set; } = "";
}

public sealed class FitmentRecord
{
    public int VehicleModelId { get; set; }
    public int YearFrom { get; set; }
    public int YearTo { get; set; }
    public string EvidenceUrl { get; set; } = "";
    public string? Notes { get; set; }
}

public sealed class RecommendationProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "";
    public int CategoryId { get; set; }
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tags { get; set; } = "";
    public string UseCases { get; set; } = "";
    public string Image { get; set; } = "";
    public double Price { get; set; }
    public int Quantity { get; set; }
    public bool IsSample { get; set; }
    public FitmentKind Fitment { get; set; }
    public List<FitmentRecord> Fitments { get; set; } = [];
}

public sealed class RecommendationQuery
{
    public string? Query { get; set; }
    public int? CategoryId { get; set; }
    public string? UseCase { get; set; }
    public string? Brand { get; set; }
    public int? VehicleModelId { get; set; }
    public int? Year { get; set; }
    public double? MinPrice { get; set; }
    public double? MaxPrice { get; set; }
    public bool PreferLowPrice { get; set; }
    public int Limit { get; set; } = 6;
}

public sealed class RecommendationItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Image { get; set; } = "";
    public double Price { get; set; }
    public int Quantity { get; set; }
    public bool IsSample { get; set; }
    public string Compatibility { get; set; } = "";
    public List<string> Reasons { get; set; } = [];
    public double Score { get; set; }
}

public sealed class RecommendationResult
{
    public string Status { get; set; } = "Ready";
    public string? Clarification { get; set; }
    public RecommendationQuery EffectiveCriteria { get; set; } = new();
    public List<RecommendationItem> Items { get; set; } = [];
}

public interface IRecommendationCatalog
{
    Task<List<RecommendationProduct>> ProductsAsync(CancellationToken cancellationToken = default);
    Task<List<VehicleOption>> VehiclesAsync(CancellationToken cancellationToken = default);
}

public interface IProductRecommendationService
{
    Task<RecommendationResult> RecommendAsync(RecommendationQuery request, CancellationToken cancellationToken = default);
}
