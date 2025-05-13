// Controllers/MenuItemController.cs
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MenuItemController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public MenuItemController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            var menuItems = await _unitOfWork.MenuItems.GetAllAsync();
            return View(menuItems);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MenuItem menuItem)
        {
            if (ModelState.IsValid)
            {
                await _unitOfWork.MenuItems.AddAsync(menuItem);
                await _unitOfWork.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(menuItem);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }
            return View(menuItem);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MenuItem menuItem)
        {
            if (id != menuItem.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                _unitOfWork.MenuItems.Update(menuItem);
                await _unitOfWork.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(menuItem);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(id);
            if (menuItem == null)
            {
                return NotFound();
            }
            return View(menuItem);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(id);
            if (menuItem != null)
            {
                _unitOfWork.MenuItems.Remove(menuItem); // Gọi Remove với đối tượng MenuItem
                await _unitOfWork.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}