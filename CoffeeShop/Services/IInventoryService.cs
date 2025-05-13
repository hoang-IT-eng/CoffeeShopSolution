// Services/IInventoryService.cs
using CoffeeShop.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public interface IInventoryService
    {
        Task<bool> CheckAvailabilityAsync(List<OrderDetail> orderDetails);
        Task DeductStockAsync(List<OrderDetail> orderDetails);
        Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync(decimal threshold);
    }
}