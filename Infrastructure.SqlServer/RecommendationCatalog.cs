using Infrastructure.SqlServer.DataContext;
using Microsoft.EntityFrameworkCore;
using UseCase.Recommendations;

namespace Infrastructure.SqlServer;

public sealed class RecommendationCatalog(ShopContext db) : IRecommendationCatalog
{
    public Task<List<VehicleOption>> VehiclesAsync(CancellationToken cancellationToken = default) => db.VehicleModels.AsNoTracking()
        .Select(v => new VehicleOption { Id = v.Id, Brand = v.Brand, Model = v.Model, Variant = v.Variant, Market = v.Market, Aliases = v.Aliases }).ToListAsync(cancellationToken);

    public Task<List<RecommendationProduct>> ProductsAsync(CancellationToken cancellationToken = default) => db.Products.AsNoTracking()
        .Select(p => new RecommendationProduct {
            Id = p.Id, Name = p.Name, Brand = p.Brand, CategoryId = p.CategoryId, Category = p.Category == null ? "" : p.Category.Name,
            Description = p.Description, Tags = p.RecommendationTags ?? "", UseCases = p.RecommendationUseCases ?? "", Image = p.ImageUrl,
            Price = p.Price, Quantity = p.Quantity, IsSample = p.IsSampleData, Fitment = (FitmentKind)p.FitmentKind,
            Fitments = p.Fitments.Select(f => new FitmentRecord { VehicleModelId = f.VehicleModelId, YearFrom = f.YearFrom, YearTo = f.YearTo, EvidenceUrl = f.EvidenceUrl, Notes = f.Notes }).ToList()
        }).ToListAsync(cancellationToken);
}
