namespace Infrastructure.Seed
{
    public static class SeedDataExtensions
    {
        public static IApplicationBuilder UseSeedData(this IApplicationBuilder app)
        {
            return app.UseMiddleware<SeedDataMiddleware>();
        }

        public static IServiceCollection AddSeedData(this IServiceCollection services)
        {
            return services.AddScoped<SeedDataMiddleware>();
        }
    }
}
