// Data/UnitOfWork/IUnitOfWork.cs
using CoffeeShop.Data.Repositories;
using CoffeeShop.Models;
using CoffeeShop.ViewModels;

namespace CoffeeShop.Data.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Order> Orders { get; }
        IRepository<OrderDetail> OrderDetails { get; }
        IRepository<MenuItem> MenuItems { get; }
        IRepository<Table> Tables { get; }
        IRepository<InventoryItem> InventoryItems { get; }
        IRepository<MenuItemRecipe> MenuItemRecipes { get; } // Add this
        IRepository<Payment> Payments { get; }
        IRepository<Customer> Customers { get; }
        IRepository<CustomerPromotion> CustomerPromotions { get; }
        Task<int> SaveChangesAsync();
    }
}