// Data/SeedData.cs
using CoffeeShop.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace CoffeeShop.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Tạo roles
            string[] roleNames = { "Admin", "Waiter", "Cashier" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Tạo admin user
            var admin = new ApplicationUser
            {
                UserName = "admin@coffee.com",
                Email = "admin@coffee.com",
                FullName = "Quản trị viên",
                Role = "Admin"
            };
            if (await userManager.FindByEmailAsync(admin.Email) == null)
            {
                await userManager.CreateAsync(admin, "Admin@123");
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            await context.SaveChangesAsync();

            // Seed tables
            int table1Id, table2Id;
            if (!context.Tables.Any())
            {
                var table1 = new Table { Name = "Bàn 1", Area = "Trong nhà", Status = "Trống" };
                var table2 = new Table { Name = "Bàn 2", Area = "Sân vườn", Status = "Trống" };
                context.Tables.AddRange(table1, table2);
                await context.SaveChangesAsync();
                table1Id = table1.Id;
                table2Id = table2.Id;
            }
            else
            {
                var tables = context.Tables.ToList();
                table1Id = tables.First(t => t.Name == "Bàn 1").Id;
                table2Id = tables.First(t => t.Name == "Bàn 2").Id;
            }

            // Seed menu items
            int espressoId, cappuccinoId;
            if (!context.MenuItems.Any())
            {
                var espresso = new MenuItem { Name = "Espresso", Price = 25000, Category = "Cà phê", ImageUrl = "" };
                var cappuccino = new MenuItem { Name = "Cappuccino", Price = 35000, Category = "Cà phê", ImageUrl = "" };
                context.MenuItems.AddRange(espresso, cappuccino);
                await context.SaveChangesAsync();
                espressoId = espresso.Id;
                cappuccinoId = cappuccino.Id;
            }
            else
            {
                var menuItems = context.MenuItems.ToList();
                espressoId = menuItems.First(m => m.Name == "Espresso").Id;
                cappuccinoId = menuItems.First(m => m.Name == "Cappuccino").Id;
            }

            // Seed inventory items
            int coffeeBeanId, milkId;
            if (!context.InventoryItems.Any())
            {
                var coffeeBean = new InventoryItem { Name = "Hạt cà phê", Quantity = 10, Unit = "kg" };
                var milk = new InventoryItem { Name = "Sữa", Quantity = 5, Unit = "lít" };
                context.InventoryItems.AddRange(coffeeBean, milk);
                await context.SaveChangesAsync();
                coffeeBeanId = coffeeBean.Id;
                milkId = milk.Id;
            }
            else
            {
                var inventoryItems = context.InventoryItems.ToList();
                coffeeBeanId = inventoryItems.First(i => i.Name == "Hạt cà phê").Id;
                milkId = inventoryItems.First(i => i.Name == "Sữa").Id;
            }

            // Seed menu item recipes
            if (!context.MenuItemRecipes.Any())
            {
                context.MenuItemRecipes.AddRange(
                    //new MenuItemRecipe { MenuItemId = espressoId, InventoryItemId = coffeeBeanId, QuantityRequired = 0.02m },
                    //new MenuItemRecipe { MenuItemId = cappuccinoId, InventoryItemId = coffeeBeanId, QuantityRequired = 0.02m },
                    //new MenuItemRecipe { MenuItemId = cappuccinoId, InventoryItemId = milkId, QuantityRequired = 0.1m }
                );
                await context.SaveChangesAsync();
            }

            // Seed orders
            int orderId;
            if (!context.Orders.Any())
            {
                var order = new Order { TableId = table1Id, CreatedAt = DateTime.Now.AddDays(-2), Status = "Completed", Total = 60000 };
                context.Orders.Add(order);
                await context.SaveChangesAsync();
                orderId = order.Id;
            }
            else
            {
                orderId = context.Orders.First().Id;
            }

            // Seed payments
            if (!context.Payments.Any())
            {
                context.Payments.AddRange(
                    new Payment { OrderId = orderId, Amount = 60000, PaymentMethod = "Cash", PaymentDate = DateTime.Now.AddDays(-2) },
                    new Payment { OrderId = orderId, Amount = 35000, PaymentMethod = "Card", PaymentDate = DateTime.Now.AddDays(-1) },
                    new Payment { OrderId = orderId, Amount = 25000, PaymentMethod = "Mobile", PaymentDate = DateTime.Now }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}