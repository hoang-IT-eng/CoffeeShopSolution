// Controllers/PaymentController.cs
using CoffeeShop.Models;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoffeeShop.Controllers
{
    [Authorize(Roles = "Cashier,Admin")]
    public class PaymentController : Controller
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        [HttpGet]
        public async Task<IActionResult> WaitForMobilePayment(int paymentId)
        {
            var payment = await _paymentService.GetPaymentWithDetailsAsync(paymentId); // Lấy cả OrderDetails
            if (payment == null || payment.PaymentMethod != "Mobile")
            {
                TempData["ErrorMessage"] = "Giao dịch không hợp lệ hoặc không phải thanh toán Mobile.";
                return RedirectToAction(nameof(Index));
            }
            if (payment.Status == "Completed" || payment.Status == "Failed")
            {
                TempData["InfoMessage"] = $"Giao dịch này đã ở trạng thái {payment.Status}.";
                return RedirectToAction("Receipt", new { paymentId = payment.Id });
            }

            // TODO: Tạo mã QR giả lập nếu muốn
            // Ví dụ: ViewBag.QrCodeContent = $"coffeeshop://pay?paymentId={payment.Id}&amount={payment.Amount}";
            // Rồi dùng thư viện ген QR ở view
            ViewBag.PaymentId = paymentId;
            ViewBag.OrderTotal = payment.Amount; // Hoặc payment.Order.Total
            ViewBag.OrderId = payment.OrderId;
            return View(payment); // Truyền đối tượng payment vào view
        }
        [HttpGet]
        public async Task<IActionResult> ExportReceiptAsText(int paymentId)
        {
            var payment = await _paymentService.GetPaymentWithDetailsAsync(paymentId);
            if (payment == null || payment.Order == null) // Kiểm tra cả payment.Order
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin thanh toán hoặc đơn hàng liên kết.";
                return RedirectToAction(nameof(Receipt), new { paymentId }); // Hoặc Index
            }

            // Sử dụng StringBuilder để tạo nội dung text hiệu quả
            var sb = new StringBuilder();
            var culture = new System.Globalization.CultureInfo("vi-VN");

            // Thông tin cửa hàng (ví dụ)
            sb.AppendLine("COFFEE SHOP XYZ");
            sb.AppendLine("Địa chỉ: 123 Đường ABC, Quận 1, TP. HCM");
            sb.AppendLine("Điện thoại: 0123 456 789");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine($"         HÓA ĐƠN THANH TOÁN (Mã GD: {payment.Id})");
            sb.AppendLine("--------------------------------------------------");

            sb.AppendLine($"Ngày giờ: {payment.PaymentDate.ToString("dd/MM/yyyy HH:mm", culture)}");
            sb.AppendLine($"Phương thức: {payment.PaymentMethod}");
            sb.AppendLine($"Khách hàng (ghi chú): {payment.CustomerInfo ?? "Không xác định"}");
            if (payment.CustomerId.HasValue && payment.Customer != null)
            {
                sb.AppendLine($"Khách hàng TV: {payment.Customer.Name} (SĐT: {payment.Customer.PhoneNumber})");
                sb.AppendLine($"Điểm đã tích lũy (giao dịch này): {payment.PointsEarned} điểm");
            }
            sb.AppendLine($"Mã đơn hàng: {payment.OrderId}");
            sb.AppendLine($"Bàn: {payment.Order?.Table?.Name ?? "Mang đi/Không xác định"}");
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("CHI TIẾT MÓN:");
            sb.AppendLine("--------------------------------------------------");
            // Định dạng cột cho đẹp hơn
            // Ví dụ: Tên món (30 ký tự), SL (5), Đơn giá (10), Thành tiền (12)
            // Bạn có thể điều chỉnh độ rộng này
            sb.AppendLine(String.Format("{0,-30} {1,5} {2,10} {3,12}", "Tên món", "SL", "Đ.Giá", "T.Tiền"));
            sb.AppendLine("--------------------------------------------------");

            if (payment.Order?.OrderDetails?.Any() == true)
            {
                foreach (var detail in payment.Order.OrderDetails)
                {
                    string menuItemName = detail.MenuItem?.Name ?? "N/A";
                    if (menuItemName.Length > 28) menuItemName = menuItemName.Substring(0, 28) + ".."; // Rút gọn nếu quá dài

                    sb.AppendLine(String.Format("{0,-30} {1,5} {2,10} {3,12}",
                        menuItemName,
                        $"x{detail.Quantity}",
                        detail.Price.ToString("N0", culture),
                        (detail.Price * detail.Quantity).ToString("N0", culture)
                    ));
                }
            }
            else
            {
                sb.AppendLine("Không có chi tiết món.");
            }
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine(String.Format("{0,-47} {1,12}", "TỔNG CỘNG:", payment.Order.Total.ToString("N0", culture) + " VND"));
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("Cảm ơn quý khách và hẹn gặp lại!");
            sb.AppendLine("www.coffeeshopxyz.com");


            // Chuẩn bị file để tải về
            var fileName = $"HoaDon_{payment.OrderId}_{payment.Id}.txt";
            var fileBytes = Encoding.UTF8.GetBytes(sb.ToString());

            return File(fileBytes, "text/plain", fileName);
        }
        [HttpPost]
        [ValidateAntiForgeryToken] // Hoặc bỏ ValidateAntiForgeryToken nếu đây là webhook thực sự từ bên ngoài
        public async Task<IActionResult> ConfirmMobilePayment(int paymentId, bool success)
        {
            try
            {
                var payment = await _paymentService.CompleteMobilePaymentAsync(paymentId, success);
                if (success)
                {
                    TempData["SuccessMessage"] = $"Thanh toán MoMo cho Payment ID #{payment.Id} (Đơn #{payment.OrderId}) thành công!";
                    return Json(new { redirectTo = Url.Action("Receipt", "Payment", new { paymentId = payment.Id }) });
                }
                else
                {
                    TempData["ErrorMessage"] = $"Thanh toán MoMo cho Payment ID #{payment.Id} (Đơn #{payment.OrderId}) thất bại.";
                    // Có thể chuyển về trang Process của Order đó, hoặc trang Index
                    return Json(new { redirectTo = Url.Action("Index", "Payment") });
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return Json(new { error = ex.Message, redirectTo = Url.Action("Index", "Payment") });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xác nhận thanh toán MoMo: " + ex.Message;
                return Json(new { error = "Lỗi không xác định.", redirectTo = Url.Action("Index", "Payment") });
            }
        }

        public PaymentController(IPaymentService paymentService, IOrderService orderService)
        {
            _paymentService = paymentService;
            _orderService = orderService;
        }

        // Hiển thị danh sách đơn hàng sẵn sàng thanh toán
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var readyOrders = await _orderService.GetReadyOrdersAsync();
            return View(readyOrders);
        }

        // Hiển thị form thanh toán cho đơn hàng cụ thể
        [HttpGet]
        public async Task<IActionResult> Process(int orderId)
        {
            var order = await _orderService.GetOrderWithDetailsAsync(orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Đơn hàng không tồn tại.";
                return RedirectToAction("Index");
            }

            if (order.Status == "Completed")
            {
                TempData["ErrorMessage"] = "Đơn hàng đã được thanh toán.";
                return RedirectToAction("Index");
            }

            ViewBag.Order = order;
            return View();
        }

        // Xử lý thanh toán
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(int orderId, string paymentMethod,
                                        string customerPhoneNumber, int? customerId,
                                        string customerInfo = null)
        {
            if (orderId <= 0)
            {
                ModelState.AddModelError("", "Order ID không hợp lệ.");
            }
            if (string.IsNullOrEmpty(paymentMethod))
            {
                ModelState.AddModelError("", "Vui lòng chọn phương thức thanh toán.");
            }
            // Không bắt buộc nhập SĐT ở đây, service sẽ xử lý logic tạo mới hoặc bỏ qua
            // if (string.IsNullOrEmpty(customerPhoneNumber))
            // {
            //     ModelState.AddModelError("customerPhoneNumber", "Vui lòng nhập số điện thoại khách hàng để tích điểm.");
            // }


            if (!ModelState.IsValid)
            {
                // ... (tải lại ViewBag.Order và return View() như cũ) ...
                var orderForView = await _orderService.GetOrderWithDetailsAsync(orderId); // Sửa tên biến
                ViewBag.Order = orderForView;
                var availablePaymentMethods = new List<SelectListItem> // Chuẩn bị lại payment methods
        {
            new SelectListItem { Value = "Cash", Text = "Tiền mặt" },
            new SelectListItem { Value = "Mobile", Text = "Thanh toán MoMo (Giả lập)" }
        };
                ViewBag.AvailablePaymentMethods = availablePaymentMethods;
                return View();
            }

            try
            {
                Payment paymentResult;
                if (paymentMethod == "Cash")
                {
                    paymentResult = await _paymentService.ProcessCashPaymentAsync(orderId, customerInfo, customerPhoneNumber, customerId);
                    TempData["SuccessMessage"] = $"Thanh toán Tiền mặt thành công cho đơn #{orderId}! Mã giao dịch: {paymentResult.Id}.";
                    if (paymentResult.CustomerId.HasValue) {/*...*/}
                    return RedirectToAction("Receipt", new { paymentId = paymentResult.Id });
                }
                else if (paymentMethod == "Mobile")
                {
                    paymentResult = await _paymentService.InitiateMobilePaymentAsync(orderId, customerInfo, customerPhoneNumber, customerId);
                    TempData["InfoMessage"] = $"Đã khởi tạo thanh toán MoMo cho đơn #{orderId}. Payment ID: {paymentResult.Id}. Vui lòng xác nhận thanh toán.";
                    // Chuyển đến trang hiển thị QR hoặc chờ xác nhận
                    return RedirectToAction("WaitForMobilePayment", new { paymentId = paymentResult.Id });
                }
                else
                {
                    throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");
                }
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException?.Message ?? "No inner exception";
                var fullError = $"Lỗi khi xử lý thanh toán: {ex.Message}. Chi tiết: {innerException}";
                ModelState.AddModelError("", fullError);
                TempData["ErrorMessage"] = fullError;
                Console.WriteLine($"Payment Error: {ex}"); // Log lỗi
            }

            // Nếu có lỗi, tải lại thông tin đơn hàng để hiển thị lại form
            var orderReload = await _orderService.GetOrderWithDetailsAsync(orderId);
            ViewBag.Order = orderReload;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Receipt(int paymentId)
        {
            var payment = await _paymentService.GetPaymentWithDetailsAsync(paymentId);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // In hóa đơn
        [HttpGet]
        public async Task<IActionResult> PrintReceipt(int paymentId)
        {
            var payment = await _paymentService.GetPaymentWithDetailsAsync(paymentId);
            if (payment == null)
            {
                return NotFound();
            }

            // Trả về view dành cho in (layout đơn giản)
            return View("PrintReceipt", payment);
        }

    }
}