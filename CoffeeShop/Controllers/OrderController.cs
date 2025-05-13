// Controllers/OrderController.cs
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Waiter,Admin")]
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IUnitOfWork _unitOfWork;

        public OrderController(IOrderService orderService, IUnitOfWork unitOfWork)
        {
            _orderService = orderService;
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _orderService.GetPendingOrdersAsync();
            return View(orders);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Tables = await _unitOfWork.Tables.GetAllAsync();
            ViewBag.MenuItems = await _unitOfWork.MenuItems.GetAllAsync();
            return View(new Order());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order, List<OrderDetail> orderDetails)
        {
            // Loại bỏ validation cho navigation properties
            ModelState.Remove("Table");
            foreach (var i in Enumerable.Range(0, orderDetails.Count))
            {
                ModelState.Remove($"OrderDetails[{i}].Order");
                ModelState.Remove($"OrderDetails[{i}].MenuItem");
            }

            if (ModelState.IsValid && orderDetails != null && orderDetails.Any())
            {
                try
                {
                    // Tính tổng giá trị đơn hàng
                    decimal total = 0;
                    foreach (var detail in orderDetails)
                    {
                        var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                        total += detail.Quantity * menuItem.Price;
                    }
                    order.Total = total;

                    await _orderService.CreateOrderAsync(order, orderDetails);
                    return RedirectToAction(nameof(Index));
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            else
            {
                // Debug lỗi ModelState
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                Console.WriteLine("ModelState errors: " + string.Join(", ", errors));
            }
            ViewBag.Tables = await _unitOfWork.Tables.GetAllAsync();
            ViewBag.MenuItems = await _unitOfWork.MenuItems.GetAllAsync();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            await _orderService.UpdateStatusAsync(id, status);
            return RedirectToAction(nameof(Index));
        }
    }
}