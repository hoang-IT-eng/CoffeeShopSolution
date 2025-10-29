// Services/PaymentService.cs
using CoffeeShop.Data;
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.EntityFrameworkCore; // Đảm bảo có using này
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly ICustomerService _customerService;
        private readonly IInventoryService _inventoryService; // <<<< THÊM: Inject IInventoryService

        public PaymentService(IUnitOfWork unitOfWork, ILogger<PaymentService> logger,
                         ApplicationDbContext context, ICustomerService customerService,
                         IInventoryService inventoryService) // <<<< THÊM: Tham số IInventoryService
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _context = context;
            _customerService = customerService;
            _inventoryService = inventoryService; // <<<< THÊM: Gán IInventoryService
        }

        public async Task<Payment> InitiateMobilePaymentAsync(int orderId, string customerInfo = null, string customerPhoneNumber = null, int? customerId = null)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null) throw new InvalidOperationException("Đơn hàng không tồn tại.");
            if (order.Status == "Completed" || order.Status == "PendingMobilePayment") // Sửa trạng thái kiểm tra
                throw new InvalidOperationException($"Đơn hàng đang ở trạng thái {order.Status}, không thể khởi tạo thanh toán mới.");

            Customer customerForPoints = null;
            // <<<< THÊM/HOÀN THIỆN: Logic tìm/tạo customerForPoints >>>>
            // (Copy và điều chỉnh từ ProcessCashPaymentAsync nếu cần)
            if (customerId.HasValue && customerId.Value > 0)
            {
                customerForPoints = await _customerService.GetCustomerByIdAsync(customerId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(customerPhoneNumber))
            {
                customerForPoints = await _customerService.GetCustomerByPhoneAsync(customerPhoneNumber);
                if (customerForPoints == null)
                {
                    string newCustomerName = !string.IsNullOrWhiteSpace(customerInfo) ? customerInfo : $"Khách {customerPhoneNumber}";
                    if (newCustomerName == "Không xác định" && order.CustomerName != null && order.CustomerName != "Không xác định")
                    {
                        newCustomerName = order.CustomerName;
                    }
                    _logger.LogInformation($"Customer with phone {customerPhoneNumber} not found (Mobile Init). Attempting to create: {newCustomerName}");
                    try
                    {
                        customerForPoints = await _customerService.CreateCustomerAsync(new Customer
                        {
                            Name = newCustomerName,
                            PhoneNumber = customerPhoneNumber,
                        });
                        _logger.LogInformation($"New customer created (Mobile Init) with ID: {customerForPoints.Id}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.LogWarning($"Failed to create new customer (Mobile Init). Phone might already exist. Error: {ex.Message}. Re-fetching.");
                        customerForPoints = await _customerService.GetCustomerByPhoneAsync(customerPhoneNumber);
                    }
                }
            }
            // <<<< KẾT THÚC THÊM/HOÀN THIỆN LOGIC CUSTOMER >>>>

            var payment = new Payment
            {
                OrderId = orderId,
                Amount = order.Total,
                PaymentMethod = "Mobile",
                PaymentDate = DateTime.Now, // Thời điểm khởi tạo
                CustomerInfo = customerInfo ?? order.CustomerName ?? "Không xác định",
                Status = "PendingMobile", // Đã sửa thành chuỗi ngắn hơn
                Notes = "Chờ xác nhận thanh toán MoMo",
                TransactionId = $"MOMO-INIT-{Guid.NewGuid().ToString().Substring(0, 8)}",
                CustomerId = customerForPoints?.Id
            };

            await _unitOfWork.Payments.AddAsync(payment);
            order.Status = "PendingMobilePayment";
            _unitOfWork.Orders.Update(order);

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation($"Mobile payment initiated for Order {orderId}. Payment ID: {payment.Id}");
            return payment;
        }

        public async Task<Payment> CompleteMobilePaymentAsync(int paymentId, bool isSuccess)
        {
            var payment = await _context.Payments
                               .Include(p => p.Order)
                               // <<<< THÊM: Include OrderDetails và MenuItem của chúng >>>>
                               .ThenInclude(o => o.OrderDetails)
                               .ThenInclude(od => od.MenuItem)
                               // <<<< KẾT THÚC THÊM INCLUDE >>>>
                               .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null) throw new InvalidOperationException("Không tìm thấy giao dịch thanh toán.");
            if (payment.PaymentMethod != "Mobile") throw new InvalidOperationException("Đây không phải giao dịch Mobile.");
            if (payment.Status == "Completed" || payment.Status == "Failed")
                throw new InvalidOperationException($"Giao dịch đã ở trạng thái {payment.Status}.");

            var order = payment.Order;
            if (order == null) throw new InvalidOperationException("Không tìm thấy đơn hàng liên kết với thanh toán này.");


            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    if (isSuccess)
                    {
                        payment.Status = "Completed";
                        payment.PaymentDate = DateTime.Now;

                        // <<<< THÊM: Kiểm tra lại tồn kho trước khi trừ >>>>
                        if (order.OrderDetails == null || !order.OrderDetails.Any())
                        {
                            // Điều này không nên xảy ra nếu Include ở trên hoạt động đúng
                            _logger.LogError($"OrderDetails not loaded for Order ID {order.Id} during mobile payment completion for Payment ID {paymentId}.");
                            throw new InvalidOperationException("Không thể xác minh tồn kho do thiếu chi tiết đơn hàng.");
                        }
                        var (isAvailable, missingItems) = await _inventoryService.CheckAvailabilityWithDetailsAsync(order.OrderDetails.ToList());
                        if (!isAvailable)
                        {
                            payment.Status = "Failed";
                            payment.Notes = "Thanh toán thất bại do không đủ hàng tồn kho.";
                            order.Status = "Ready"; // Hoặc trạng thái phù hợp
                            _unitOfWork.Orders.Update(order);
                            _unitOfWork.Payments.Update(payment);
                            await _unitOfWork.SaveChangesAsync();
                            await transaction.CommitAsync();
                            _logger.LogWarning($"Mobile payment ID {paymentId} failed due to insufficient stock: {string.Join(", ", missingItems)}");
                            throw new InvalidOperationException("Không đủ nguyên liệu trong kho để hoàn tất thanh toán Mobile: " + string.Join(", ", missingItems));
                        }
                        // <<<< KẾT THÚC KIỂM TRA TỒN KHO >>>>

                        // <<<< THÊM: Trừ kho nguyên liệu >>>>
                        await _inventoryService.DeductStockAsync(order.OrderDetails.ToList());
                        // <<<< KẾT THÚC TRỪ KHO >>>>

                        order.Status = "Completed";
                        _unitOfWork.Orders.Update(order);

                        if (order.TableId > 0)
                        {
                            var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId);
                            if (table != null)
                            {
                                table.Status = "Empty";
                                _unitOfWork.Tables.Update(table);
                            }
                        }

                        if (payment.CustomerId.HasValue && payment.Amount > 0)
                        {
                            int pointsEarned = await _customerService.CalculatePointsFromAmount(payment.Amount);
                            if (pointsEarned > 0)
                            {
                                await _customerService.AddPointsAsync(payment.CustomerId.Value, pointsEarned, payment.Id);
                                payment.PointsEarned = pointsEarned;
                            }
                            await _customerService.UpdateCustomerStatsAsync(payment.CustomerId.Value);
                        }
                        _logger.LogInformation($"Mobile payment ID {paymentId} completed successfully. Stock deducted.");
                    }
                    else
                    {
                        payment.Status = "Failed";
                        if (order != null)
                        {
                            order.Status = "Ready";
                            _unitOfWork.Orders.Update(order);
                        }
                        _logger.LogWarning($"Mobile payment ID {paymentId} failed.");
                    }

                    _unitOfWork.Payments.Update(payment);
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return payment;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error completing mobile payment ID {paymentId}.");
                    throw;
                }
            }
        }

        public async Task<Payment> ProcessCashPaymentAsync(int orderId, string customerInfo = null, string customerPhoneNumber = null, int? customerId = null)
        {
            _logger.LogInformation($"Starting CASH payment process for Order {orderId}. Phone: {customerPhoneNumber}, CustID: {customerId}");

            // <<<< SỬA: Tải Order với OrderDetails và MenuItem ngay từ đầu >>>>
            var order = await _context.Orders
                                  .Include(o => o.OrderDetails)
                                  .ThenInclude(od => od.MenuItem)
                                  .FirstOrDefaultAsync(o => o.Id == orderId);
            // <<<< KẾT THÚC SỬA >>>>

            if (order == null) throw new InvalidOperationException("Đơn hàng không tồn tại.");
            if (order.Status == "Completed") throw new InvalidOperationException("Đơn hàng đã được thanh toán.");

            var existingPaymentForOrder = (await _unitOfWork.Payments.FindAsync(p => p.OrderId == orderId && p.Status == "Completed")).FirstOrDefault();
            if (existingPaymentForOrder != null) throw new InvalidOperationException("Đơn hàng đã được thanh toán (tìm thấy payment đã hoàn thành).");

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    Customer customerForPoints = null;
                    // <<<< THÊM/HOÀN THIỆN: Logic tìm/tạo customerForPoints (đã làm ở lần trước) >>>>
                    if (customerId.HasValue && customerId.Value > 0)
                    {
                        customerForPoints = await _customerService.GetCustomerByIdAsync(customerId.Value);
                    }
                    else if (!string.IsNullOrWhiteSpace(customerPhoneNumber))
                    {
                        customerForPoints = await _customerService.GetCustomerByPhoneAsync(customerPhoneNumber);
                        if (customerForPoints == null)
                        {
                            string newCustomerName = !string.IsNullOrWhiteSpace(customerInfo) ? customerInfo : $"Khách {customerPhoneNumber}";
                            if (newCustomerName == "Không xác định" && order.CustomerName != null && order.CustomerName != "Không xác định")
                            {
                                newCustomerName = order.CustomerName;
                            }
                            _logger.LogInformation($"Customer with phone {customerPhoneNumber} not found. Attempting to create new customer: {newCustomerName}");
                            try
                            {
                                customerForPoints = await _customerService.CreateCustomerAsync(new Customer
                                {
                                    Name = newCustomerName,
                                    PhoneNumber = customerPhoneNumber,
                                });
                                _logger.LogInformation($"New customer created with ID: {customerForPoints.Id}");
                            }
                            catch (InvalidOperationException ex)
                            {
                                _logger.LogWarning($"Failed to create new customer (Cash Payment). Phone might already exist. Error: {ex.Message}. Trying to re-fetch.");
                                customerForPoints = await _customerService.GetCustomerByPhoneAsync(customerPhoneNumber);
                            }
                        }
                    }
                    // <<<< KẾT THÚC LOGIC CUSTOMER >>>>

                    var payment = new Payment
                    {
                        OrderId = orderId,
                        Amount = order.Total,
                        PaymentMethod = "Cash",
                        PaymentDate = DateTime.Now,
                        CustomerInfo = customerInfo ?? order.CustomerName ?? "Không xác định",
                        Status = "Completed",
                        Notes = string.Empty,
                        TransactionId = $"CASH-{orderId}-{DateTime.UtcNow.Ticks}",
                        CustomerId = customerForPoints?.Id
                    };

                    await _unitOfWork.Payments.AddAsync(payment);
                    // <<<< XÓA: SaveChangesAsync() sớm ở đây, sẽ save một lần ở cuối >>>>
                    // await _unitOfWork.SaveChangesAsync(); // Lưu payment để có payment.Id

                    // <<<< THÊM: Kiểm tra lại tồn kho trước khi trừ >>>>
                    if (order.OrderDetails == null || !order.OrderDetails.Any())
                    {
                        // Điều này không nên xảy ra nếu Include ở trên hoạt động đúng
                        _logger.LogError($"OrderDetails not loaded for Order ID {order.Id} during cash payment for Payment ID {payment.Id}.");
                        throw new InvalidOperationException("Không thể xác minh tồn kho do thiếu chi tiết đơn hàng.");
                    }
                    var (isAvailable, missingItems) = await _inventoryService.CheckAvailabilityWithDetailsAsync(order.OrderDetails.ToList());
                    if (!isAvailable)
                    {
                        await transaction.RollbackAsync(); // QUAN TRỌNG: Rollback trước khi throw
                        _logger.LogWarning($"Cash payment for Order ID {orderId} failed due to insufficient stock: {string.Join(", ", missingItems)}");
                        throw new InvalidOperationException("Không đủ nguyên liệu trong kho để hoàn tất thanh toán: " + string.Join(", ", missingItems));
                    }
                    // <<<< KẾT THÚC KIỂM TRA TỒN KHO >>>>

                    // <<<< THÊM: Trừ kho nguyên liệu >>>>
                    await _inventoryService.DeductStockAsync(order.OrderDetails.ToList());
                    // <<<< KẾT THÚC TRỪ KHO >>>>


                    order.Status = "Completed";
                    _unitOfWork.Orders.Update(order);

                    if (order.TableId > 0)
                    {
                        var table = await _unitOfWork.Tables.GetByIdAsync(order.TableId);
                        if (table != null)
                        {
                            table.Status = "Empty";
                            _unitOfWork.Tables.Update(table);
                        }
                    }

                    if (customerForPoints != null && payment.Amount > 0)
                    {
                        int pointsEarned = await _customerService.CalculatePointsFromAmount(payment.Amount);
                        if (pointsEarned > 0)
                        {
                            await _customerService.AddPointsAsync(customerForPoints.Id, pointsEarned, payment.Id); // payment.Id sẽ có sau SaveChangesAsync đầu tiên
                            payment.PointsEarned = pointsEarned;
                            // _unitOfWork.Payments.Update(payment); // Không cần update payment ở đây nếu AddPointsAsync không save và payment.Id chưa có
                        }
                        await _customerService.UpdateCustomerStatsAsync(customerForPoints.Id);
                    }

                    await _unitOfWork.SaveChangesAsync(); // <<<< LƯU TẤT CẢ THAY ĐỔI MỘT LẦN (bao gồm payment, order, table, customer, inventory)
                                                          // Lúc này payment.Id sẽ có giá trị

                    // Nếu AddPointsAsync cần payment.Id và bạn đã update payment.PointsEarned
                    // Bạn có thể cần gọi SaveChangesAsync một lần nữa ở đây nếu AddPointsAsync cũng update Customer
                    // HOẶC tốt hơn là AddPointsAsync chỉ update Customer.LoyaltyPoints và PaymentService update payment.PointsEarned
                    // và một SaveChangesAsync duy nhất ở cuối là đủ.
                    // Hiện tại, AddPointsAsync trong CustomerService đã có _unitOfWork.Customers.Update(customer);
                    // và UpdateCustomerStatsAsync cũng có UpdateCustomerAsync (mà nó cũng update customer).
                    // Nên SaveChangesAsync() ở trên đã bao gồm cả thay đổi cho customer.

                    await transaction.CommitAsync();

                    _logger.LogInformation($"Cash Payment completed successfully for Order {orderId}. Payment ID: {payment.Id}. Stock deducted.");
                    return payment;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Cash Payment failed for Order {orderId}: {ex.Message} --- Inner: {ex.InnerException?.Message}");
                    throw;
                }
            }
        }

        // <<<< XÓA: Phương thức ProcessPaymentAsync chung chung này nếu không còn dùng đến >>>>
        // public async Task<Payment> ProcessPaymentAsync(int orderId, string paymentMethod, ...) { ... }
        // <<<< KẾT THÚC XÓA >>>>


        // <<<< XÓA HOẶC GIỮ LẠI NẾU CẦN: Các phương thức private ProcessPaymentByMethod, ProcessCardPayment, ProcessMobilePayment >>>>
        // Nếu bạn đã bỏ thanh toán Card và Mobile cũ, và ProcessCashPaymentAsync đã xử lý riêng,
        // thì ProcessPaymentByMethod và các hàm con của nó có thể không cần thiết nữa.
        // private async Task<bool> ProcessPaymentByMethod(string paymentMethod) { ... }
        // private async Task<bool> ProcessCardPayment() { ... }
        // private async Task<bool> ProcessMobilePayment() { ... }
        // private async Task<bool> ProcessCardPayment(Payment payment) { ... } // Phiên bản cũ
        // private async Task<bool> ProcessMobilePayment(Payment payment) { ... } // Phiên bản cũ
        // <<<< KẾT THÚC XÓA/GIỮ LẠI >>>>


        public async Task<Payment> GetPaymentByOrderIdAsync(int orderId)
        {
            var payments = await _unitOfWork.Payments.FindAsync(p => p.OrderId == orderId);
            return payments.FirstOrDefault();
        }

        public async Task<Payment> GetPaymentWithDetailsAsync(int paymentId)
        {
            var payment = await _context.Payments
                                     .Include(pay => pay.Order)
                                         .ThenInclude(o => o.Table)
                                     .Include(pay => pay.Order)
                                         .ThenInclude(o => o.OrderDetails)
                                             .ThenInclude(od => od.MenuItem)
                                     .Include(pay => pay.Customer)
                                     .FirstOrDefaultAsync(p => p.Id == paymentId);
            return payment;
        }
    }
}