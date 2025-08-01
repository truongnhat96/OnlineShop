
using Infrastructure.SqlServer.DataContext;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Seed
{
    public class SeedDataMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var dbContext = context.RequestServices.GetRequiredService<ShopContext>();
            var logger = context.RequestServices.GetRequiredService<ILogger<SeedDataMiddleware>>();
            var adminRole = await dbContext.Roles.FindAsync(1);
            var userRole = await dbContext.Roles.FindAsync(2);
            var productCategory = await dbContext.Categories.ToListAsync();
            if (productCategory.Count == 0)
            {
                productCategory.Add(new Category { Name = "Diệt Virus" });
                productCategory.Add(new Category { Name = "Key Office" });
                productCategory.Add(new Category { Name = "ChatGPT" });
                productCategory.Add(new Category { Name = "AutoCAD" });
                productCategory.Add(new Category { Name = "Project" });
                productCategory.Add(new Category { Name = "Visio" });
                productCategory.Add(new Category { Name = "Visual Studio" });
                productCategory.Add(new Category { Name = "IDM" });
                productCategory.Add(new Category { Name = "VPN" });
                productCategory.Add(new Category { Name = "Thủ Thuật" });
                await dbContext.Categories.AddRangeAsync(productCategory);
            }
            if (adminRole == null)
            {
                adminRole = new Role { Name = "Admin" };
                await dbContext.Roles.AddAsync(adminRole);
            }
            if (userRole == null)
            {
                userRole = new Role { Name = "User" };
                await dbContext.Roles.AddAsync(userRole);
            }
            await dbContext.SaveChangesAsync();
            var superUser = await dbContext.Users.FindAsync(1);
            if (superUser == null)
            {
                superUser = new User
                {
                    Username = "AdminTruongshop204",
                    DisplayName = "Super Admin TruongShop",
                    Email = "luongnhattruong2004@gmail.com",
                    Password = "@1TruongShop02k4",
                    RoleId = 1
                };
                await dbContext.Users.AddAsync(superUser);
            }
            if (await dbContext.SaveChangesAsync() > 0)
            {
                logger.LogInformation("Database has been seeded with initial data.");
            }
            else
            {
                logger.LogInformation("Database already has initial data, no changes made.");
            }
            await next(context);
        }
    }
}
