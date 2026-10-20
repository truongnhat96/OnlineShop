using Microsoft.Extensions.Configuration;

namespace Infrastructure.Models;

public sealed class StoreBranding
{
    public string Name { get; init; } = "MotoShop";
    public string Tagline { get; init; } = "Phụ kiện cho hành trình của bạn";
    public string Logo { get; init; } = "/images/motoshop-logo.svg";
    public string Favicon { get; init; } = "/images/motoshop-mark.svg";
    public string Hero { get; init; } = "/images/motoshop-hero.svg";
    public static StoreBranding From(IConfiguration configuration) => new()
    {
        Name = configuration["Storefront:BrandName"] ?? "MotoShop",
        Tagline = configuration["Storefront:Tagline"] ?? "Phụ kiện cho hành trình của bạn",
        Logo = configuration["Storefront:Logo"] ?? "/images/motoshop-logo.svg",
        Favicon = configuration["Storefront:Favicon"] ?? "/images/motoshop-mark.svg",
        Hero = configuration["Storefront:Hero"] ?? "/images/motoshop-hero.svg"
    };
}
