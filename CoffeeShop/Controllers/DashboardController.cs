using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInventoryService _inventoryService;

        public DashboardController(IUnitOfWork unitOfWork, IInventoryService inventoryService)
        {
            _unitOfWork = unitOfWork;
            _inventoryService = inventoryService;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            // 1. Cảnh báo nguyên liệu thiếu (Quantity <= MinimumThreshold)
            var lowStockItems = await _inventoryService.GetLowStockItemsAsync();

            // 2. Cảnh báo nguyên liệu sắp hết hạn (trong 7 ngày tới)
            var nearExpiryItems = await _inventoryService.GetExpiringItemsAsync(7);

            // 3. Nguyên liệu đã hết hạn
            var expiredItems = await _inventoryService.GetItemsByExpirationAsync(today);
            var actualExpiredItems = expiredItems.Where(i => i.ExpirationDate < today);

            // 4. Thống kê doanh thu và đơn hàng
            var ordersToday = await _unitOfWork.Orders.FindAsync(o => o.CreatedAt.Date == today);
            var dailyRevenue = ordersToday.Sum(o => o.Total);

            var newOrders = await _unitOfWork.Orders.FindAsync(o => o.Status == "New");
            var processingOrders = await _unitOfWork.Orders.FindAsync(o => o.Status == "Processing");

            var occupiedTables = await _unitOfWork.Tables.FindAsync(t => t.Status == "Occupied");

            // 5. Dữ liệu cho biểu đồ doanh thu 7 ngày
            var last7Days = Enumerable.Range(0, 7)
                .Select(d => today.AddDays(-d))
                .OrderBy(d => d)
                .ToList();

            var chartLabels = new List<string>();
            var chartData = new List<decimal>();

            foreach (var day in last7Days)
            {
                var dayOrders = await _unitOfWork.Orders.FindAsync(o => o.CreatedAt.Date == day);
                var dayTotal = dayOrders.Sum(o => o.Total);

                chartLabels.Add($"'{day:dd/MM}'");
                chartData.Add(dayTotal);
            }

            // ViewBag assignments
            ViewBag.DailyRevenue = dailyRevenue;
            ViewBag.NewOrders = newOrders.Count();
            ViewBag.ProcessingOrders = processingOrders.Count();
            ViewBag.TablesInUse = occupiedTables.Count();

            // Cảnh báo kho
            ViewBag.LowStockItems = lowStockItems.ToList();
            ViewBag.NearExpiryItems = nearExpiryItems.ToList();
            ViewBag.ExpiredItems = actualExpiredItems.ToList();

            // Tổng số cảnh báo
            ViewBag.TotalWarnings = lowStockItems.Count() + nearExpiryItems.Count() + actualExpiredItems.Count();

            // Dữ liệu biểu đồ
            ViewBag.ChartLabels = string.Join(",", chartLabels);
            ViewBag.ChartData = string.Join(",", chartData);

            return View();
        }
    }
}