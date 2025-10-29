// Services/OrderService.cs - Phiên bản cải thiện
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace CoffeeShop.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task CreateOrderAsync(Order order, List<OrderDetail> orderDetails) // Hoặc Task<Order>
        {
            if (order == null || orderDetails == null || !orderDetails.Any())
            {
                throw new ArgumentException("Thông tin đơn hàng không hợp lệ.");
            }

            // 1. Tính tổng tiền
            decimal total = 0;
            foreach (var detail in orderDetails)
            {
                if (detail.MenuItemId <= 0 || detail.Quantity <= 0)
                {
                    throw new ArgumentException($"Chi tiết đơn hàng không hợp lệ cho MenuItemId: {detail.MenuItemId}");
                }
                var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                if (menuItem == null)
                {
                    throw new InvalidOperationException($"Không tìm thấy MenuItem với ID: {detail.MenuItemId}");
                }
                total += detail.Quantity * menuItem.Price;
                detail.Price = menuItem.Price; // Gán đơn giá vào chi tiết
            }
            order.Total = total;

            // 2. Thiết lập thông tin cơ bản cho Order
            order.CreatedAt = DateTime.Now;
            order.Status = "New"; // Hoặc một trạng thái mặc định khác

            // 3. Lưu Order để lấy ID
            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync(); // Quan trọng: Lưu để order.Id được gán

            // 4. Gán OrderId cho OrderDetails và lưu chúng
            foreach (var detail in orderDetails)
            {
                detail.OrderId = order.Id; // Sử dụng ID vừa được tạo
                await _unitOfWork.OrderDetails.AddAsync(detail);
            }

            // 5. Lưu tất cả OrderDetails
            await _unitOfWork.SaveChangesAsync();

            // return order; // Nếu phương thức trả về Task<Order>
        }

        public async Task<IEnumerable<Order>> GetPendingOrdersAsync()
        {
            var orders = await _unitOfWork.Orders.FindAsync(
                o => o.Status == "New" || o.Status == "Processing"
            );

            // Load Table manually for each order
            foreach (var order in orders)
            {
                order.Table = await _unitOfWork.Tables.GetByIdAsync(order.TableId);
            }

            return orders;
        }

        public async Task<IEnumerable<Order>> GetReadyOrdersAsync()
        {
            var orders = await _unitOfWork.Orders.FindAsync(
                o => o.Status == "Processing" || o.Status == "Ready" || o.Status == "New"
            );

            // Load related data manually
            foreach (var order in orders)
            {
                order.Table = await _unitOfWork.Tables.GetByIdAsync(order.TableId);

                var orderDetails = await _unitOfWork.OrderDetails.FindAsync(od => od.OrderId == order.Id);
                order.OrderDetails = orderDetails.ToList();

                foreach (var detail in order.OrderDetails)
                {
                    detail.MenuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                }
            }

            return orders;
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

        public async Task<Order> GetOrderWithDetailsAsync(int orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order != null)
            {
                // Load Table
                order.Table = await _unitOfWork.Tables.GetByIdAsync(order.TableId);

                // Load OrderDetails
                var orderDetails = await _unitOfWork.OrderDetails.FindAsync(od => od.OrderId == orderId);
                order.OrderDetails = orderDetails.ToList();

                // Load MenuItem for each OrderDetail
                foreach (var detail in order.OrderDetails)
                {
                    detail.MenuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                }
            }
            return order;
        }
    }
}