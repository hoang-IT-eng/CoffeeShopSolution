using CoffeeShop.Data;
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Controllers
{
    [Authorize]
    public class TableController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public TableController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            var tables = await _unitOfWork.Tables.GetAllAsync();
            return View(tables);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            if (!new[] { "Empty", "Occupied", "Reserved" }.Contains(status))
            {
                return BadRequest("Trạng thái không hợp lệ.");
            }

            var table = await _unitOfWork.Tables.GetByIdAsync(id);
            if (table == null)
            {
                return NotFound();
            }

            table.Status = status;
            await _unitOfWork.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}