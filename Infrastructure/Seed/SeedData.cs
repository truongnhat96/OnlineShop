using Infrastructure.SqlServer.DataContext;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Seed
{
    public static class SeedData
    {
        public static async Task InitializeAsync(ShopContext context)
        {
            var adminRole = await context.Roles.FindAsync(1);
            var userRole = await context.Roles.FindAsync(2);
            var productCategory = await context.Categories.ToListAsync();
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
                await context.Categories.AddRangeAsync(productCategory);
            }
            if (adminRole == null)
            {
                adminRole = new Role { Name = "Admin" };
                await context.Roles.AddAsync(adminRole);
            }
            if (userRole == null)
            {
                userRole = new Role { Name = "User" };
                await context.Roles.AddAsync(userRole);
            }
            await context.SaveChangesAsync();
            var superUser = await context.Users.FindAsync(1);
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
                await context.Users.AddAsync(superUser);
            }
            if (await context.SaveChangesAsync() == 0)
            {
                throw new Exception("Data has existed or failed to seed initial data.");
            }
        }
    }
}
