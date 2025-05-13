// Services/OrderService.cs
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInventoryService _inventoryService;

        public OrderService(IUnitOfWork unitOfWork, IInventoryService inventoryService)
        {
            _unitOfWork = unitOfWork;
            _inventoryService = inventoryService;
        }

        public async Task CreateOrderAsync(Order order, List<OrderDetail> orderDetails)
        {
            // Kiểm tra kho
            if (!await _inventoryService.CheckAvailabilityAsync(orderDetails))
            {
                throw new InvalidOperationException("Không đủ nguyên liệu để tạo đơn hàng.");
            }

            order.CreatedAt = DateTime.Now;
            order.Status = "New";
            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            foreach (var detail in orderDetails)
            {
                detail.OrderId = order.Id;
                var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                detail.Price = menuItem.Price;
                await _unitOfWork.OrderDetails.AddAsync(detail);
            }

            // Trừ kho
            await _inventoryService.DeductStockAsync(orderDetails);

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<Order>> GetPendingOrdersAsync()
        {
            return await _unitOfWork.Orders.FindAsync(o => o.Status == "New" || o.Status == "Processing");
        }

        public async Task UpdateStatusAsync(int orderId, string status)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order != null)
            {
                order.Status = status;
                _unitOfWork.Orders.Update(order);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task<Order> GetByIdAsync(int orderId)
        {
            return await _unitOfWork.Orders.GetByIdAsync(orderId);
        }
    }
}