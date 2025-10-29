// Data/ApplicationDbContext.cs
using CoffeeShop.Models;
using CoffeeShop.ViewModels;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser> // Assuming ApplicationUser is your user class
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
        public DbSet<CustomerPromotion> CustomerPromotions { get; set; }
        public DbSet<Customer> Customers { get; set; }
        // public DbSet<ApplicationUser> ApplicationUsers { get; set; } // If you need to query ApplicationUser directly

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình độ chính xác cho decimal (Your existing configurations - KEEP THESE)
            modelBuilder.Entity<MenuItem>()
                .Property(m => m.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.Total)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderDetail>()
                .Property(od => od.Price)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Payment>()
    .Property(p => p.Notes)
    .IsRequired(false);
            modelBuilder.Entity<Payment>(entity => // Bạn có thể thêm cấu hình cho Payment ở đây
            {
                // ... các cấu hình khác cho Payment ...

                entity.Property(p => p.TransactionId)
                      .HasMaxLength(100) // Giữ nguyên StringLength nếu có
                      .IsRequired(false); // Quan trọng: Cho phép NULL
            });

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.Quantity)
                .HasPrecision(18, 2); // Good for inventory quantity

            modelBuilder.Entity<MenuItemRecipe>()
                .Property(r => r.QuantityRequired)
                .HasPrecision(18, 4); // Changed to 4 for potentially smaller recipe quantities, adjust if needed

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            // START OF ADDED CONFIGURATIONS FOR MenuItemRecipe RELATIONSHIPS
            modelBuilder.Entity<MenuItem>()
                .HasMany(m => m.Recipes)
                .WithOne(r => r.MenuItem)
                .HasForeignKey(r => r.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade); // If a MenuItem is deleted, its recipes are also deleted

            modelBuilder.Entity<InventoryItem>()
                .HasMany(i => i.MenuItemRecipes) // Ensure your InventoryItem model has 'public virtual ICollection<MenuItemRecipe> MenuItemRecipes { get; set; }'
                .WithOne(r => r.InventoryItem)
                .HasForeignKey(r => r.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting an InventoryItem if it's used in a recipe.
                                                    // You might want Cascade if that's your business rule, but Restrict is safer.
                                                    // END OF ADDED CONFIGURATIONS

            // Example: If you want to ensure Table Number is unique (Optional, add if needed)
            // modelBuilder.Entity<Table>()
            //    .HasIndex(t => t.TableNumber)
            //    .IsUnique();
        }
    }
}