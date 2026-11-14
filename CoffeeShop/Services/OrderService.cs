using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;

namespace CoffeeShop.Services
{
    public class OrderService : IOrderService
    {
        private static readonly HashSet<string> OpenStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "New", "Processing", "Ready", "PendingMobilePayment"
        };

        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task CreateOrderAsync(Order order, List<OrderDetail> orderDetails)
        {
            if (order == null || orderDetails == null || !orderDetails.Any())
            {
                throw new ArgumentException("Thông tin đơn hàng không hợp lệ.");
            }

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
                detail.Price = menuItem.Price;
            }

            order.Total = total;
            order.CreatedAt = DateTime.Now;
            order.Status = "New";

            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            foreach (var detail in orderDetails)
            {
                detail.OrderId = order.Id;
                await _unitOfWork.OrderDetails.AddAsync(detail);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<Order>> GetPendingOrdersAsync()
        {
            // Floor / shift: every open ticket including Ready waiting for payment.
            var orders = (await _unitOfWork.Orders.FindWithIncludeAsync(
                o => OpenStatuses.Contains(o.Status),
                o => o.Table))
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            await AttachDetailsAsync(orders);
            return orders;
        }

        public async Task<IEnumerable<Order>> GetReadyOrdersAsync()
        {
            // Cashier queue: ready to collect payment (or unfinished mobile sim).
            var orders = (await _unitOfWork.Orders.FindWithIncludeAsync(
                o => o.Status == "Ready" || o.Status == "PendingMobilePayment",
                o => o.Table))
                .OrderBy(o => o.CreatedAt)
                .ToList();

            await AttachDetailsAsync(orders);
            return orders;
        }

        public async Task UpdateStatusAsync(int orderId, string status)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
            {
                throw new InvalidOperationException("Không tìm thấy đơn hàng.");
            }

            order.Status = status;
            _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<Order?> GetByIdAsync(int orderId)
        {
            return await _unitOfWork.Orders.GetByIdAsync(orderId);
        }

        public async Task<Order?> GetOrderWithDetailsAsync(int orderId)
        {
            var order = await _unitOfWork.Orders.GetByIdWithIncludesAsync(orderId, o => o.Table);
            if (order == null)
            {
                return null;
            }

            await AttachDetailsAsync(new List<Order> { order });
            return order;
        }

        public static bool IsOpenStatus(string? status) =>
            !string.IsNullOrWhiteSpace(status) && OpenStatuses.Contains(status);

        private async Task AttachDetailsAsync(List<Order> orders)
        {
            if (orders.Count == 0)
            {
                return;
            }

            var ids = orders.Select(o => o.Id).ToList();
            var details = (await _unitOfWork.OrderDetails.FindAsync(d => ids.Contains(d.OrderId))).ToList();
            var menuIds = details.Select(d => d.MenuItemId).Distinct().ToList();
            var menus = menuIds.Count == 0
                ? new Dictionary<int, MenuItem>()
                : (await _unitOfWork.MenuItems.FindAsync(m => menuIds.Contains(m.Id))).ToDictionary(m => m.Id);

            foreach (var order in orders)
            {
                order.OrderDetails = details.Where(d => d.OrderId == order.Id).ToList();
                foreach (var detail in order.OrderDetails)
                {
                    if (menus.TryGetValue(detail.MenuItemId, out var item))
                    {
                        detail.MenuItem = item;
                    }
                }
            }
        }
    }
}
