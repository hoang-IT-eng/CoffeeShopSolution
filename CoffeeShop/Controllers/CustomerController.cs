// Controllers/CustomerController.cs
using CoffeeShop.Models;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Admin,Cashier")]
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        // GET: Customer
        public async Task<IActionResult> Index(string searchTerm)
        {
            var customers = string.IsNullOrEmpty(searchTerm)
                ? await _customerService.GetAllCustomersAsync()
                : await _customerService.SearchCustomersAsync(searchTerm);

            ViewBag.SearchTerm = searchTerm;
            return View(customers);
        }

        // GET: Customer/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var customer = await _customerService.GetCustomerWithDetailsAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: Customer/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _customerService.CreateCustomerAsync(customer);
                    TempData["SuccessMessage"] = "Tạo khách hàng thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi tạo khách hàng.");
                }
            }

            return View(customer);
        }

        // GET: Customer/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // POST: Customer/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _customerService.UpdateCustomerAsync(customer);
                    TempData["SuccessMessage"] = "Cập nhật khách hàng thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi cập nhật khách hàng.");
                }
            }

            return View(customer);
        }

        // POST: Customer/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _customerService.DeleteCustomerAsync(id);
                TempData["SuccessMessage"] = "Xóa khách hàng thành công!";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi xóa khách hàng.";
            }

            return RedirectToAction(nameof(Index));
        }

        // API for quick search (AJAX)
        [HttpGet]
        public async Task<IActionResult> QuickSearch(string phone)
        {
            if (string.IsNullOrEmpty(phone))
            {
                return Json(null);
            }

            var customer = await _customerService.GetCustomerByPhoneAsync(phone);
            if (customer != null)
            {
                return Json(new
                {
                    id = customer.Id,
                    name = customer.Name,
                    phone = customer.PhoneNumber,
                    points = customer.LoyaltyPoints,
                    level = customer.MembershipLevel
                });
            }

            return Json(null);
        }
    }
}