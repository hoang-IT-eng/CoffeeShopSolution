using CoffeeShop.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public interface IInventoryService
    {
        Task<bool> CheckAvailabilityAsync(List<OrderDetail> orderDetails);
        Task DeductStockAsync(List<OrderDetail> orderDetails);
        Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(decimal threshold);
        Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(); // Overload không cần threshold
        Task AddStockAsync(int inventoryItemId, decimal quantity, DateTime expirationDate);
        Task<IEnumerable<InventoryItem>> GetItemsByExpirationAsync(DateTime? beforeDate);
        Task<IEnumerable<InventoryItem>> GetExpiringItemsAsync(int daysFromNow); // Nguyên liệu sắp hết hạn
        Task<InventoryItem> GetByIdAsync(int id);
        Task DeleteAsync(int id);
        Task<(bool isAvailable, List<string> missingItems)> CheckAvailabilityWithDetailsAsync(List<OrderDetail> orderDetails);
        Task RestockFromOrderAsync(List<OrderDetail> orderDetails);

    }
}