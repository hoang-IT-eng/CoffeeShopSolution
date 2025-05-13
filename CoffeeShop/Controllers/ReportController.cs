// Controllers/ReportController.cs
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

        public async Task<IActionResult> Sales()
        {
            var startDate = DateTime.Today.AddDays(-6);
            var endDate = DateTime.Today;

            // Lấy danh sách Payments và await kết quả
            var payments = await _unitOfWork.Payments
                .FindAsync(p => p.PaymentDate >= startDate && p.PaymentDate <= endDate);

            // GroupBy trên IEnumerable<Payment>
            var salesData = payments
                .GroupBy(p => p.PaymentDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(p => p.Amount) })
                .OrderBy(x => x.Date)
                .ToList();

            ViewBag.Dates = salesData.Select(x => x.Date.ToString("yyyy-MM-dd")).ToArray();
            ViewBag.Totals = salesData.Select(x => x.Total).ToArray();
            return View();
        }
    }
}