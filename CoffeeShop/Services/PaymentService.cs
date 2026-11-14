using CoffeeShop.Data;
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly ICustomerService _customerService;
        private readonly IInventoryService _inventoryService;

        public PaymentService(
            IUnitOfWork unitOfWork,
            ILogger<PaymentService> logger,
            ApplicationDbContext context,
            ICustomerService customerService,
            IInventoryService inventoryService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _context = context;
            _customerService = customerService;
            _inventoryService = inventoryService;
        }

        public async Task<Payment> InitiateMobilePaymentAsync(
            int orderId,
            string? customerInfo = null,
            string? customerPhoneNumber = null,
            int? customerId = null)
        {
            var order = await LoadOrderForPaymentAsync(orderId);
            EnsurePayable(order);

            var customerForPoints = await ResolveCustomerAsync(customerId, customerPhoneNumber, customerInfo, order.CustomerName);

            var payment = new Payment
            {
                OrderId = orderId,
                Amount = order.Total,
                PaymentMethod = "Mobile",
                PaymentDate = DateTime.Now,
                CustomerInfo = customerInfo ?? order.CustomerName ?? "Không xác định",
                Status = "PendingMobile",
                Notes = "Chờ xác nhận thanh toán MoMo",
                TransactionId = $"MOMO-INIT-{Guid.NewGuid().ToString()[..8]}",
                CustomerId = customerForPoints?.Id
            };

            await _unitOfWork.Payments.AddAsync(payment);
            order.Status = "PendingMobilePayment";
            _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Mobile payment initiated for Order {OrderId}. Payment ID: {PaymentId}", orderId, payment.Id);
            return payment;
        }

        public async Task<Payment> CompleteMobilePaymentAsync(int paymentId, bool isSuccess)
        {
            var payment = await _context.Payments
                .Include(p => p.Order)
                    .ThenInclude(o => o!.OrderDetails)
                        .ThenInclude(od => od.MenuItem)
                .FirstOrDefaultAsync(p => p.Id == paymentId)
                ?? throw new InvalidOperationException("Không tìm thấy giao dịch thanh toán.");

            if (payment.PaymentMethod != "Mobile")
                throw new InvalidOperationException("Đây không phải giao dịch Mobile.");
            if (payment.Status is "Completed" or "Failed")
                throw new InvalidOperationException($"Giao dịch đã ở trạng thái {payment.Status}.");

            var order = payment.Order ?? throw new InvalidOperationException("Không tìm thấy đơn hàng liên kết với thanh toán này.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (isSuccess)
                {
                    // Stock was already deducted when the order became Ready.
                    payment.Status = "Completed";
                    payment.PaymentDate = DateTime.Now;
                    order.Status = "Completed";
                    _unitOfWork.Orders.Update(order);
                    await ReleaseTableIfIdleAsync(order.TableId, order.Id);
                    await ApplyLoyaltyAsync(payment);
                    _logger.LogInformation("Mobile payment ID {PaymentId} completed successfully.", paymentId);
                }
                else
                {
                    payment.Status = "Failed";
                    order.Status = "Ready";
                    _unitOfWork.Orders.Update(order);
                    _logger.LogWarning("Mobile payment ID {PaymentId} failed.", paymentId);
                }

                _unitOfWork.Payments.Update(payment);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                return payment;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Payment> ProcessCashPaymentAsync(
            int orderId,
            string? customerInfo = null,
            string? customerPhoneNumber = null,
            int? customerId = null)
        {
            _logger.LogInformation("Starting CASH payment for Order {OrderId}", orderId);

            var order = await LoadOrderForPaymentAsync(orderId);
            EnsurePayable(order);

            var existing = (await _unitOfWork.Payments.FindAsync(p => p.OrderId == orderId && p.Status == "Completed")).FirstOrDefault();
            if (existing != null)
                throw new InvalidOperationException("Đơn hàng đã được thanh toán.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var customerForPoints = await ResolveCustomerAsync(customerId, customerPhoneNumber, customerInfo, order.CustomerName);

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

                // Kitchen already deducted stock at Ready — do not deduct again.
                await _unitOfWork.Payments.AddAsync(payment);
                order.Status = "Completed";
                _unitOfWork.Orders.Update(order);
                await ReleaseTableIfIdleAsync(order.TableId, order.Id);
                await _unitOfWork.SaveChangesAsync();

                if (customerForPoints != null && payment.Amount > 0)
                {
                    var pointsEarned = await _customerService.CalculatePointsFromAmount(payment.Amount);
                    if (pointsEarned > 0)
                    {
                        await _customerService.AddPointsAsync(customerForPoints.Id, pointsEarned, payment.Id);
                        payment.PointsEarned = pointsEarned;
                        _unitOfWork.Payments.Update(payment);
                    }
                    await _customerService.UpdateCustomerStatsAsync(customerForPoints.Id);
                    await _unitOfWork.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Cash payment completed for Order {OrderId}. Payment ID: {PaymentId}", orderId, payment.Id);
                return payment;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<Payment?> GetPaymentByOrderIdAsync(int orderId)
        {
            var payments = await _unitOfWork.Payments.FindAsync(p => p.OrderId == orderId);
            return payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();
        }

        public async Task<Payment?> GetPaymentWithDetailsAsync(int paymentId)
        {
            return await _context.Payments
                .Include(pay => pay.Order)
                    .ThenInclude(o => o!.Table)
                .Include(pay => pay.Order)
                    .ThenInclude(o => o!.OrderDetails)
                        .ThenInclude(od => od.MenuItem)
                .Include(pay => pay.Customer)
                .FirstOrDefaultAsync(p => p.Id == paymentId);
        }

        private async Task<Order> LoadOrderForPaymentAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.MenuItem)
                .Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            return order ?? throw new InvalidOperationException("Đơn hàng không tồn tại.");
        }

        private static void EnsurePayable(Order order)
        {
            if (order.Status == "Completed")
                throw new InvalidOperationException("Đơn hàng đã được thanh toán.");
            if (order.Status == "Cancelled")
                throw new InvalidOperationException("Đơn hàng đã bị hủy.");
            if (order.Status is not ("Ready" or "PendingMobilePayment"))
                throw new InvalidOperationException("Chỉ thu tiền đơn đã xong món (Ready).");
        }

        private async Task<Customer?> ResolveCustomerAsync(
            int? customerId,
            string? customerPhoneNumber,
            string? customerInfo,
            string? orderCustomerName)
        {
            if (customerId is > 0)
            {
                return await _customerService.GetCustomerByIdAsync(customerId.Value);
            }

            if (string.IsNullOrWhiteSpace(customerPhoneNumber))
            {
                return null;
            }

            var existing = await _customerService.GetCustomerByPhoneAsync(customerPhoneNumber);
            if (existing != null) return existing;

            var name = !string.IsNullOrWhiteSpace(customerInfo)
                ? customerInfo
                : (!string.IsNullOrWhiteSpace(orderCustomerName) ? orderCustomerName! : $"Khách {customerPhoneNumber}");

            try
            {
                return await _customerService.CreateCustomerAsync(new Customer
                {
                    Name = name,
                    PhoneNumber = customerPhoneNumber
                });
            }
            catch (InvalidOperationException)
            {
                return await _customerService.GetCustomerByPhoneAsync(customerPhoneNumber);
            }
        }

        private async Task ApplyLoyaltyAsync(Payment payment)
        {
            if (!payment.CustomerId.HasValue || payment.Amount <= 0) return;

            var pointsEarned = await _customerService.CalculatePointsFromAmount(payment.Amount);
            if (pointsEarned > 0)
            {
                await _customerService.AddPointsAsync(payment.CustomerId.Value, pointsEarned, payment.Id);
                payment.PointsEarned = pointsEarned;
            }
            await _customerService.UpdateCustomerStatsAsync(payment.CustomerId.Value);
        }

        private async Task ReleaseTableIfIdleAsync(int tableId, int completedOrderId)
        {
            if (tableId <= 0) return;
            var table = await _unitOfWork.Tables.GetByIdAsync(tableId);
            if (table == null) return;

            var others = await _unitOfWork.Orders.FindAsync(o =>
                o.TableId == tableId &&
                o.Id != completedOrderId &&
                (o.Status == "New" || o.Status == "Processing" || o.Status == "Ready" || o.Status == "PendingMobilePayment"));

            if (!others.Any())
            {
                table.Status = "Empty";
                _unitOfWork.Tables.Update(table);
            }
        }
    }
}
