using Microsoft.EntityFrameworkCore;


namespace Infrastructure.SqlServer.DataContext
{
    public class ShopContext : DbContext
    {
        private readonly string _connectionString = @"Server=.\SQLEXPRESS;Database=OnlineShop;Trusted_Connection=True;TrustServerCertificate=True;";

        //Use to run command add migrations and update database
        public ShopContext()
        {
        }

        public ShopContext(DbContextOptions<ShopContext> options) : base(options)
        {
        }

        public required DbSet<Category> Categories { get; set; }
        public required DbSet<Product> Products { get; set; }
        public required DbSet<Post> Posts { get; set; }
        public required DbSet<Role> Roles { get; set; }
        public required DbSet<User> Users { get; set; }
        public required DbSet<ItemInfor> ItemInfors { get; set; }
        public required DbSet<CartItem> CartItems { get; set; }
        public required DbSet<Review> Reviews { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_connectionString).UseLazyLoadingProxies().EnableSensitiveDataLogging();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(p => p.Name);
            });
            modelBuilder.Entity<ItemInfor>(entity =>
            {
                entity.HasOne(i => i.Product)
                    .WithMany(p => p.ItemInfor)
                    .HasForeignKey(i => i.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasOne(r => r.Product)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(r => r.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(r => r.User)
                    .WithMany(u => u.Reviews)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasOne(u => u.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(u => u.Username);
            });
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasOne(c => c.Product)
                    .WithMany(p => p.CartItems)
                    .HasForeignKey(c => c.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(c => c.User)
                    .WithMany(u => u.CartItems)
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Post>(entity =>
            {
                entity.HasOne(p => p.User)
                    .WithMany(u => u.Posts)
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(p => p.Title);
                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Posts)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
