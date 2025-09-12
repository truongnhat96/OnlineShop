using AutoMapper;
using Infrastructure.AIChat;
using Infrastructure.Models.Caching;
using Infrastructure.PaymentSupport;
using Infrastructure.Seed;
using Infrastructure.SqlServer;
using Infrastructure.SqlServer.AutoMapper;
using Infrastructure.SqlServer.DataContext;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Threading.RateLimiting;
using UseCase;
using UseCase.Business_Logic;
using UseCase.Caching;
using UseCase.Repository;
using UseCase.UnitOfWork;

namespace Infrastructure
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            System.Console.WriteLine("Database connection string: " + builder.Configuration.GetConnectionString("DefaultConnection"));

            builder.Services.AddSession(config =>
                    config.IdleTimeout = TimeSpan.FromHours(10));

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Login";
                    options.AccessDeniedPath = "/Forbidden";
                });


            builder.Services.AddMailService(builder.Configuration);
            builder.Services.AddPaymentService(builder.Configuration);

            builder.Services.AddRateLimiter(rateLimiterOptions =>
            {
                rateLimiterOptions.AddSlidingWindowLimiter("SlidingWindow", options =>
                {
                    options.PermitLimit = 1000;
                    options.Window = TimeSpan.FromMinutes(20);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 150;
                    options.SegmentsPerWindow = 4;
                }).AddConcurrencyLimiter("Concurrency", options =>
                {
                    options.PermitLimit = 50;
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 25;
                });
            });

            builder.Services.AddChatbot(builder.Configuration);

            //  builder.Configuration.AddUserSecrets<Program>(true, true);

            await RegisterServicesForDatabase(builder.Services, builder.Configuration);

            var app = builder.Build();


	
	    // 1. Khởi EF Migrations *luôn luôn*, bất kể môi trường
		using (var scope = app.Services.CreateScope())
		{
		    var db = scope.ServiceProvider.GetRequiredService<ShopContext>();

		    var pending = await db.Database.GetPendingMigrationsAsync();
		    if (pending.Any())
		    {
		        Console.WriteLine($"Applying {pending.Count()} pending migrations...");
		        await db.Database.MigrateAsync();
		        Console.WriteLine("Migrations applied.");
		    }
		    else
		    {
		        Console.WriteLine("No pending migrations.");
		    }
		}

            // 2. Khởi tạo dữ liệu mẫu, chỉ chạy một lần khi ứng dụng khởi động
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ShopContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                // Seed initial data
                try
                {
                    await SeedData.InitializeAsync(context);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(message: ex.Message);
                }
            }


            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }


            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseSession();

            app.UseRouting();

            app.UseRateLimiter();

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }

        private static Task RegisterServicesForDatabase(IServiceCollection services, ConfigurationManager configuration)
        {
            services.AddAutoMapper(typeof(EntityMapper));
            services.AddDbContext<ShopContext>(option =>
                 option.UseSqlServer(configuration.GetConnectionString("DefaultConnection"), sqlOpt => sqlOpt.EnableRetryOnFailure())
                       .UseLazyLoadingProxies());
            var cacheOption = new CacheOption(); //configuration.GetSection("Cache").Get<CacheOption>() ?? throw new("");
            InitCache(services, configuration, cacheOption);

            services.AddTransient<IUserRepository>(service => new UserRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IRoleRepository>(service => new RoleRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IPostRepository>(service => new PostRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IReviewRepository>(service => new ReviewRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IProductRepository>(service => new ProductRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<ICategoryRepository>(service => new CategoryRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IItemInforRepository>(service => new ItemInforRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<ICartItemRepository>(service => new CartItemRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IDiscountRepository>(service => new DiscountRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IDiscountUsageRepository>(service => new DiscountUsageRepository(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));

            services.AddTransient<IUserUnitOfWork>(service => new UserUnitOfWork(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IProductUnitOfWork>(service => new ProductUnitOfWork(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<ICartItemUnitOfWork>(service => new CartItemUnitOfWork(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<IReviewUnitOfWork>(service => new ReviewUnitOfWork(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));

            services.AddTransient<IHomeManage>(service => new HomeManage(service.GetRequiredService<IProductRepository>()));
            services.AddTransient<IReviewerFinder>(service => new ReviewerFinder(service.GetRequiredService<IReviewUnitOfWork>() /*, service.GetRequiredService<IReviewUnitOfWork>(), service.GetRequiredService<IDistributedCache>()*/));
            services.AddTransient<IUserManage>(service => new UserManage(service.GetRequiredService<IUserUnitOfWork>()));
            services.AddTransient<ICategoryManage>(service => new CachableCategoryManage(service.GetRequiredService<IProductUnitOfWork>(), service.GetRequiredService<IDistributedCache>(), new(), service.GetRequiredService<ILogger<CachableCategoryManage>>()));
            services.AddTransient<IPostManage>(service => new CachablePostManage(service.GetRequiredService<IPostRepository>(), service.GetRequiredService<IDistributedCache>(), new(), service.GetRequiredService<ILogger<CachablePostManage>>()));
            services.AddTransient<IProductManage>(service => new ProductManage(service.GetRequiredService<IProductUnitOfWork>()));
            services.AddTransient<ICartManage>(service => new CachableCartManage(service.GetRequiredService<ICartItemUnitOfWork>(), service.GetRequiredService<IDistributedCache>(), new()));
            return Task.CompletedTask;
        }

        private static Task InitCache(IServiceCollection services, ConfigurationManager configuration, CacheOption cacheOption)
        {
            switch (cacheOption.Type)
            {
                case CacheTypes.Memory:
                    services.AddDistributedMemoryCache();
                    break;
                case CacheTypes.Redis:
                    if (cacheOption.CacheRedisOptions == null)
                    {
                        throw new("Redis option is required");
                    }
                    services.AddStackExchangeRedisCache(option =>
                    {
                        option.Configuration = configuration.GetConnectionString(cacheOption.CacheRedisOptions.ConnectionStringName);
                    });
                    break;
                default:
                    throw new("Cache type not supported");
            }
            return Task.CompletedTask;
        }
    }
}
