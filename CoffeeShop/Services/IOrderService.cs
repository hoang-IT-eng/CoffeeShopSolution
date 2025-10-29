// Services/IOrderService.cs
using CoffeeShop.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public interface IOrderService
    {
        Task CreateOrderAsync(Order order, List<OrderDetail> orderDetails);
        Task<IEnumerable<Order>> GetPendingOrdersAsync();
        Task<IEnumerable<Order>> GetReadyOrdersAsync();
        Task UpdateStatusAsync(int orderId, string status);
        Task<Order> GetByIdAsync(int orderId);
        Task<Order> GetOrderWithDetailsAsync(int orderId);
    }
}