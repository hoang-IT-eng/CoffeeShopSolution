using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReportController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Sales(DateTime? startDate, DateTime? endDate)
        {
            var orders = await _unitOfWork.Orders.GetAllAsync();
            var payments = await _unitOfWork.Payments.GetAllAsync();

            // Lọc theo khoảng thời gian nếu có
            if (startDate.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt.Date >= startDate.Value.Date).ToList();
            }
            if (endDate.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt.Date <= endDate.Value.Date).ToList();
            }

            var dailySales = orders
                .GroupBy(o => o.CreatedAt.Date)
                .Select(g => new DailySale { Date = g.Key, Total = g.Sum(o => o.Total) })
                .OrderBy(g => g.Date)
                .ToList();

            var paymentMethods = payments
                .GroupBy(p => p.PaymentMethod)
                .Select(g => new PaymentMethodSummary { Method = g.Key, Total = g.Sum(p => p.Amount) })
                .ToList();

            var model = new ReportViewModel
            {
                DailySales = dailySales,
                PaymentMethods = paymentMethods,
                StartDate = startDate,
                EndDate = endDate
            };

            return View(model);
        }
    }
}