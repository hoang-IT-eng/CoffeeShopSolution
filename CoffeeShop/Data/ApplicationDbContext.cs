// Data/ApplicationDbContext.cs
using CoffeeShop.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<MenuItemRecipe> MenuItemRecipes { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình độ chính xác cho decimal
            modelBuilder.Entity<MenuItem>()
                .Property(m => m.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.Total)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderDetail>()
                .Property(od => od.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.Quantity)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MenuItemRecipe>()
                .Property(r => r.QuantityRequired)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Payment>()
    .Property(p => p.Amount)
    .HasPrecision(18, 2);
        }
    }
}