// Controllers/TableController.cs
using CoffeeShop.Data.UnitOfWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Waiter,Admin")]
    public class TableController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public TableController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> FloorPlan()
        {
            var tables = await _unitOfWork.Tables.GetAllAsync();
            return View(tables);
        }
    }
}