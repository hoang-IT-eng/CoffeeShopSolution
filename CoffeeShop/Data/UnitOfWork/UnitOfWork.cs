// Data/UnitOfWork/UnitOfWork.cs
using CoffeeShop.Data.Repositories;
using CoffeeShop.Models;
using CoffeeShop.ViewModels;

namespace CoffeeShop.Data.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public IRepository<Order> Orders { get; private set; }
        public IRepository<OrderDetail> OrderDetails { get; private set; }
        public IRepository<MenuItem> MenuItems { get; private set; }
        public IRepository<Table> Tables { get; private set; }
        public IRepository<InventoryItem> InventoryItems { get; private set; }
        public IRepository<MenuItemRecipe> MenuItemRecipes { get; private set; } // Add this
        public IRepository<Payment> Payments { get; private set; }
        public IRepository<Customer> Customers { get; private set; }
        public IRepository<CustomerPromotion> CustomerPromotions { get; private set; }


        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            Orders = new Repository<Order>(context);
            OrderDetails = new Repository<OrderDetail>(context);
            MenuItems = new Repository<MenuItem>(context);
            Tables = new Repository<Table>(context);
            InventoryItems = new Repository<InventoryItem>(context);
            MenuItemRecipes = new Repository<MenuItemRecipe>(context); // Add this
            Payments = new Repository<Payment>(context);
            Customers = new Repository<Customer>(context);
            CustomerPromotions = new Repository<CustomerPromotion>(context);
        }

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
        public void Dispose() => _context.Dispose();
    }
}