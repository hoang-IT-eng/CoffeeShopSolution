// Controllers/PaymentController.cs
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Cashier,Admin")]
    public class PaymentController : Controller
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;

        public PaymentController(IPaymentService paymentService, IOrderService orderService)
        {
            _paymentService = paymentService;
            _orderService = orderService;
        }

        [HttpGet]
        public async Task<IActionResult> Process(int orderId)
        {
            var order = await _orderService.GetByIdAsync(orderId);
            if (order == null || order.Status == "Completed")
            {
                return NotFound();
            }
            ViewBag.Order = order;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int orderId, string paymentMethod)
        {
            try
            {
                await _paymentService.ProcessPaymentAsync(orderId, paymentMethod);
                return RedirectToAction("Index", "Order");
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
            var order = await _orderService.GetByIdAsync(orderId);
            ViewBag.Order = order;
            return View();
        }
    }
}