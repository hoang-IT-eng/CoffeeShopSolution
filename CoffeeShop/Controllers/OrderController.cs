// Controllers/OrderController.cs - Phiên bản cải thiện
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Waiter,Admin")]
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IInventoryService _inventoryService;

        public OrderController(IOrderService orderService, IUnitOfWork unitOfWork, IInventoryService inventoryService)
        {
            _orderService = orderService;
            _unitOfWork = unitOfWork;
            _inventoryService = inventoryService;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _orderService.GetPendingOrdersAsync();
            return View(orders);
        }

        public async Task<IActionResult> Create(int? tableId) // Giữ nguyên tham số tableId nếu bạn có chức năng tạo đơn nhanh từ bàn
        {
            var tablesFromDb = await _unitOfWork.Tables.GetAllAsync();
            // Chuyển đổi List<Table> thành IEnumerable<SelectListItem>
            ViewBag.Tables = tablesFromDb.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.Name
            }).ToList(); // Hoặc .AsEnumerable()

            var menuItems = await _unitOfWork.MenuItems.GetAllAsync();
            ViewBag.MenuItems = menuItems;

            var order = new Order();
            if (tableId.HasValue)
            {
                order.TableId = tableId.Value;
                // Logic cập nhật trạng thái bàn "Occupied" nên ở action POST sau khi tạo đơn thành công
                // Hoặc nếu bạn muốn nó được chọn sẵn trong dropdown:
                // ViewBag.Tables = tablesFromDb.Select(t => new SelectListItem
                // {
                // Value = t.Id.ToString(),
                // Text = t.Name,
                // Selected = (t.Id == tableId.Value) // Đánh dấu bàn được chọn sẵn
                // }).ToList();
            }
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order order, List<OrderDetail> orderDetails)
        {
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
                    // CHỈ KIỂM TRA TỒN KHO, CHƯA TRỪ
                    var (isAvailable, missingItems) = await _inventoryService.CheckAvailabilityWithDetailsAsync(orderDetails);
                    if (!isAvailable)
                    {
                        foreach (var missing in missingItems)
                        {
                            ModelState.AddModelError("", missing);
                        }
                        // !!!! QUAN TRỌNG: Chuẩn bị lại ViewBag TRƯỚC KHI return View !!!!
                        await PrepareCreateOrderViewBagAsync(order.TableId); // Gọi hàm chuẩn bị ViewBag
                        return View(order);
                    }

                    // GỌI SERVICE ĐỂ TẠO ĐƠN HÀNG (như đã thảo luận trước)
                    // Tính tổng tiền trước khi gọi service hoặc trong service
                    decimal total = 0;
                    foreach (var detail in orderDetails)
                    {
                        var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                        if (menuItem == null)
                        {
                            ModelState.AddModelError("", $"Không tìm thấy món với ID: {detail.MenuItemId}");
                            await PrepareCreateOrderViewBagAsync(order.TableId);
                            return View(order);
                        }
                        total += detail.Quantity * menuItem.Price;
                    }
                    order.Total = total;

                    // Nên là: await _orderService.CreateOrderAsync(order, orderDetails);
                    // Nhưng tạm thời giữ logic cũ của bạn để tập trung vào lỗi ViewBag
                    order.CreatedAt = DateTime.Now;
                    order.Status = "New";
                    await _unitOfWork.Orders.AddAsync(order);
                    await _unitOfWork.SaveChangesAsync();

                    foreach (var detail in orderDetails)
                    {
                        detail.OrderId = order.Id;
                        var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                        detail.Price = menuItem.Price; // Đảm bảo giá được gán
                        await _unitOfWork.OrderDetails.AddAsync(detail);
                    }
                    await _unitOfWork.SaveChangesAsync();


                    // Cập nhật trạng thái bàn nếu có TableId
                    if (order.TableId > 0)
                    {
                        var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId);
                        if (table != null && table.Status != "Occupied")
                        {
                            table.Status = "Occupied";
                            _unitOfWork.Tables.Update(table);
                            await _unitOfWork.SaveChangesAsync();
                        }
                    }

                    TempData["SuccessMessage"] = $"Đã tạo đơn hàng #{order.Id} thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException argEx)
                {
                    ModelState.AddModelError("", $"Lỗi dữ liệu đơn hàng: {argEx.Message}");
                }
                catch (InvalidOperationException opEx)
                {
                    ModelState.AddModelError("", $"Lỗi xử lý đơn hàng: {opEx.Message}");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Lỗi tạo đơn hàng: {ex.Message}");
                }
            }

            // !!!! QUAN TRỌNG: Chuẩn bị lại ViewBag TRƯỚC KHI return View nếu ModelState không hợp lệ !!!!
            await PrepareCreateOrderViewBagAsync(order.TableId); // Gọi hàm chuẩn bị ViewBag
            return View(order);
        }
        // Hàm helper để chuẩn bị ViewBag cho cả action GET và POST (khi ModelState invalid)
        private async Task PrepareCreateOrderViewBagAsync(int? selectedTableId = null)
        {
            var tablesFromDb = await _unitOfWork.Tables.GetAllAsync();
            ViewBag.Tables = tablesFromDb.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.Name,
                Selected = (selectedTableId.HasValue && t.Id == selectedTableId.Value)
            }).ToList();

            var menuItems = await _unitOfWork.MenuItems.GetAllAsync();
            ViewBag.MenuItems = menuItems;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            try
            {
                var order = await _orderService.GetOrderWithDetailsAsync(id);
                if (order == null)
                {
                    return NotFound();
                }

                // TRỪ KHO KHI HOÀN THÀNH ĐƠN HÀNG
                if (status == "Ready" && order.Status != "Ready")
                {
                    // Kiểm tra lại tồn kho trước khi hoàn thành
                    var (isAvailable, missingItems) = await _inventoryService.CheckAvailabilityWithDetailsAsync(order.OrderDetails.ToList());
                    if (!isAvailable)
                    {
                        TempData["ErrorMessage"] = $"Không thể hoàn thành đơn hàng #{id}: " + string.Join(", ", missingItems);
                        return RedirectToAction(nameof(Index));
                    }

                    await _inventoryService.DeductStockAsync(order.OrderDetails.ToList());
                    TempData["SuccessMessage"] = $"Đơn hàng #{id} đã hoàn thành và đã trừ nguyên liệu khỏi kho!";
                }

                await _orderService.UpdateStatusAsync(id, status);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi cập nhật trạng thái: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Waiter")]
        public async Task<IActionResult> CancelOrder(int orderId)
        {
            var order = await _orderService.GetOrderWithDetailsAsync(orderId);
            if (order == null)
            {
                return NotFound();
            }

            // CHỈ HỦY ĐƠN CHƯA HOÀN THÀNH (chưa trừ kho)
            if (order.Status == "New" || order.Status == "Processing")
            {
                await _orderService.UpdateStatusAsync(orderId, "Cancelled");

                // Cập nhật trạng thái bàn nếu cần
                var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId);
                if (table != null && table.Status == "Occupied")
                {
                    var otherOrdersOnTable = await _unitOfWork.Orders.FindAsync(o =>
                        o.TableId == order.TableId &&
                        o.Id != orderId &&
                        (o.Status == "New" || o.Status == "Processing" || o.Status == "Ready"));

                    if (!otherOrdersOnTable.Any())
                    {
                        table.Status = "Available";
                        _unitOfWork.Tables.Update(table);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                TempData["SuccessMessage"] = $"Đơn hàng #{orderId} đã được hủy.";
            }
            else if (order.Status == "Ready")
            {
                // Nếu đơn đã hoàn thành thì cần hoàn trả nguyên liệu
                await _inventoryService.RestockFromOrderAsync(order.OrderDetails.ToList());
                await _orderService.UpdateStatusAsync(orderId, "Cancelled");
                TempData["SuccessMessage"] = $"Đơn hàng #{orderId} đã được hủy và hoàn trả nguyên liệu.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Không thể hủy đơn hàng #{orderId} với trạng thái {order.Status}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _orderService.GetOrderWithDetailsAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            return View(order);
        }
    }
}