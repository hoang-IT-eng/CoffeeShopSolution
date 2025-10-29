using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderService _orderService;

        public HomeController(IUnitOfWork unitOfWork, IOrderService orderService)
        {
            _unitOfWork = unitOfWork;
            _orderService = orderService;
        }

        public async Task<IActionResult> Index()
        {
            // T?ng quan tr?ng thái bàn
            var tables = await _unitOfWork.Tables.GetAllAsync();
            ViewBag.EmptyTables = tables.Count(t => t.Status == "Empty");
            ViewBag.OccupiedTables = tables.Count(t => t.Status == "Occupied");
            ViewBag.ReservedTables = tables.Count(t => t.Status == "Reserved");

            // ??n hàng ?ang ch? x? lý
            var pendingOrders = await _orderService.GetPendingOrdersAsync();
            return View(pendingOrders);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}