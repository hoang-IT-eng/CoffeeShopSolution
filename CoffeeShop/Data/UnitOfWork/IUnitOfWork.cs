// Data/UnitOfWork/IUnitOfWork.cs
using CoffeeShop.Data.Repositories;
using CoffeeShop.Models;

namespace CoffeeShop.Data.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<Order> Orders { get; }
        IRepository<OrderDetail> OrderDetails { get; }
        IRepository<MenuItem> MenuItems { get; }
        IRepository<Table> Tables { get; }
        IRepository<InventoryItem> InventoryItems { get; }
        IRepository<MenuItemRecipe> MenuItemRecipes { get; }
        IRepository<Payment> Payments { get; }
        Task<int> SaveChangesAsync();
    }
}