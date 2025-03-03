using AutoMapper;
using Infrastructure.SqlServer;
using Infrastructure.SqlServer.AutoMapper;
using Infrastructure.SqlServer.DataContext;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UseCase;
using UseCase.Business_Logic;
using UseCase.Repository;
using UseCase.UnitOfWork;

namespace Infrastructure
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddSession(config =>
                    config.IdleTimeout = TimeSpan.FromHours(10));

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Login";
                    options.AccessDeniedPath = "/Forbidden";
                });

            builder.Services.AddMailService(builder.Configuration);

            RegisterServicesForDatabase(builder.Services, builder.Configuration);

            var app = builder.Build();

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

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }

        private static void RegisterServicesForDatabase(IServiceCollection services, ConfigurationManager configuration)
        {
            services.AddAutoMapper(typeof(EntityMapper));
            services.AddDbContext<ShopContext>(option =>
                 option.UseSqlServer(configuration.GetConnectionString("OnlineShopDatabase"))
                       .UseLazyLoadingProxies());

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
            services.AddTransient<ISearchingUnitOfWork>(service => new SearchingUnitOfWork(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));
            services.AddTransient<ICartItemUnitOfWork>(service => new CartItemUnitOfWork(service.GetRequiredService<ShopContext>(), service.GetRequiredService<IMapper>()));

            services.AddTransient<IHomeManage>(service => new HomeManage(service.GetRequiredService<ISearchingUnitOfWork>()));
            services.AddTransient<IUserManage>(service => new UserManage(service.GetRequiredService<IUserUnitOfWork>()));
            services.AddTransient<IProductManage>(service => new ProductManage(service.GetRequiredService<IProductUnitOfWork>()));
            services.AddTransient<ICartManage>(service => new CartManage(service.GetRequiredService<ICartItemUnitOfWork>()));
        }
    }
}
