using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class InventoryController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly IUnitOfWork _unitOfWork; // Thêm IUnitOfWork

        public InventoryController(IInventoryService inventoryService, IUnitOfWork unitOfWork)
        {
            _inventoryService = inventoryService;
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index(DateTime? beforeDate)
        {
            var items = await _inventoryService.GetItemsByExpirationAsync(beforeDate);
            ViewBag.BeforeDate = beforeDate?.ToString("yyyy-MM-dd");
            return View(items);
        }


        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InventoryItem item)
        {
            if (ModelState.IsValid)
            {
                await _unitOfWork.InventoryItems.AddAsync(item); // Thêm vào database
                await _unitOfWork.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(item);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _inventoryService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, decimal quantity, string action, DateTime expirationDate)
        {
            var item = await _inventoryService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            // Validation
            if (quantity <= 0)
            {
                ModelState.AddModelError("", "Số lượng phải lớn hơn 0!");
                return View(item);
            }

            if (string.IsNullOrEmpty(action))
            {
                ModelState.AddModelError("", "Vui lòng chọn hành động!");
                return View(item);
            }

            try
            {
                if (action == "add")
                {
                    await _inventoryService.AddStockAsync(id, quantity, expirationDate);
                    TempData["Success"] = $"Đã nhập thêm {quantity} {item.Unit} {item.Name}";
                }
                else if (action == "deduct")
                {
                    if (item.Quantity < quantity)
                    {
                        ModelState.AddModelError("", $"Số lượng trong kho không đủ! Hiện có: {item.Quantity} {item.Unit}");
                        return View(item);
                    }

                    await _inventoryService.AddStockAsync(id, -quantity, item.ExpirationDate);
                    TempData["Success"] = $"Đã xuất {quantity} {item.Unit} {item.Name}";
                }
                else
                {
                    ModelState.AddModelError("", "Hành động không hợp lệ!");
                    return View(item);
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(item);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _inventoryService.GetByIdAsync(id);
            if (item != null)
            {
                await _inventoryService.DeleteAsync(id); // Sử dụng DeleteAsync từ service
            }
            return RedirectToAction(nameof(Index));
        }
    }
}