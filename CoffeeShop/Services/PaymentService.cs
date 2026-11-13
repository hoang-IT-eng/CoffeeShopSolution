using CoffeeShop.Data;
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services
{
    public class PaymentService : IPaymentService
    {
        public const string MethodCash = "Cash";
        public const string MethodCard = "Card";
        public const string MethodMobileSim = "MobileSim";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PaymentService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly ICustomerService _customerService;

        public PaymentService(
            IUnitOfWork unitOfWork,
            ILogger<PaymentService> logger,
            ApplicationDbContext context,
            ICustomerService customerService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _context = context;
            _customerService = customerService;
        }

        public Task<Payment> ProcessCashPaymentAsync(
            int orderId,
            string? customerInfo = null,
            string? customerPhoneNumber = null,
            int? customerId = null) =>
            SettleImmediateAsync(orderId, MethodCash, customerInfo, customerPhoneNumber, customerId);

        public Task<Payment> ProcessCardPaymentAsync(
            int orderId,
            string? customerInfo = null,
            string? customerPhoneNumber = null,
            int? customerId = null) =>
            SettleImmediateAsync(orderId, MethodCard, customerInfo, customerPhoneNumber, customerId);

        public async Task<Payment> InitiateSimulatedPaymentAsync(
            int orderId,
            string? customerInfo = null,
            string? customerPhoneNumber = null,
            int? customerId = null)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId)
                ?? throw new InvalidOperationException("Đơn hàng không tồn tại.");

            EnsurePayable(order);
            await EnsureNoOpenPendingAsync(orderId);

            var customerForPoints = await ResolveCustomerAsync(customerId, customerPhoneNumber, customerInfo, order.CustomerName);
            var tx = $"SIM-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

            var payment = new Payment
            {
                OrderId = orderId,
                Amount = order.Total,
                PaymentMethod = MethodMobileSim,
                PaymentDate = DateTime.Now,
                CustomerInfo = customerInfo ?? order.CustomerName ?? "Không xác định",
                Status = "Pending",
                Notes = "Chờ giả lập thanh toán (MobileSim)",
                TransactionId = tx,
                CustomerId = customerForPoints?.Id
            };

            await _unitOfWork.Payments.AddAsync(payment);
            order.Status = "PendingMobilePayment";
            _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("MobileSim payment {PaymentId} initiated for order {OrderId}", payment.Id, orderId);
            return payment;
        }

        public async Task<Payment> CompleteSimulatedPaymentAsync(int paymentId, bool isSuccess, string source = "confirm")
        {
            var payment = await _context.Payments
                .Include(p => p.Order)
                    .ThenInclude(o => o!.OrderDetails)
                        .ThenInclude(od => od.MenuItem)
                .FirstOrDefaultAsync(p => p.Id == paymentId)
                ?? throw new InvalidOperationException("Không tìm thấy giao dịch thanh toán.");

            if (payment.PaymentMethod is not (MethodMobileSim or "Mobile"))
            {
                throw new InvalidOperationException("Chỉ áp dụng cho thanh toán MobileSim.");
            }

            var canonical = IPaymentService.CanonicalStatus(payment.Status);
            if (canonical == "Paid")
            {
                if (isSuccess)
                {
                    return payment; // idempotent success
                }

                throw new InvalidOperationException("Giao dịch đã Paid — không thể đánh Failed.");
            }

            if (canonical == "Failed")
            {
                if (!isSuccess)
                {
                    return payment;
                }

                throw new InvalidOperationException("Giao dịch đã Failed — tạo thanh toán mới nếu cần.");
            }

            if (canonical != "Pending")
            {
                throw new InvalidOperationException($"Giao dịch đang ở trạng thái {payment.Status}.");
            }

            var order = payment.Order
                ?? throw new InvalidOperationException("Không tìm thấy đơn hàng liên kết với thanh toán này.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (isSuccess)
                {
                    payment.Status = "Paid";
                    payment.PaymentDate = DateTime.Now;
                    payment.Notes = $"Giả lập thành công via {source}";
                    order.Status = "Completed";
                    _unitOfWork.Orders.Update(order);
                    await ReleaseTableIfIdleAsync(order.TableId, order.Id);

                    if (payment.CustomerId.HasValue && payment.Amount > 0)
                    {
                        var points = await _customerService.CalculatePointsFromAmount(payment.Amount);
                        if (points > 0)
                        {
                            await _customerService.AddPointsAsync(payment.CustomerId.Value, points, payment.Id);
                            payment.PointsEarned = points;
                        }

                        await _customerService.UpdateCustomerStatsAsync(payment.CustomerId.Value);
                    }

                    _logger.LogInformation("MobileSim payment {PaymentId} Paid via {Source}", paymentId, source);
                }
                else
                {
                    payment.Status = "Failed";
                    payment.Notes = $"Giả lập thất bại via {source}";
                    order.Status = "Ready";
                    _unitOfWork.Orders.Update(order);
                    _logger.LogWarning("MobileSim payment {PaymentId} Failed via {Source}", paymentId, source);
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

        public async Task<Payment> GetPaymentByOrderIdAsync(int orderId)
        {
            var payments = await _unitOfWork.Payments.FindAsync(p => p.OrderId == orderId);
            return payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()!;
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

        private async Task<Payment> SettleImmediateAsync(
            int orderId,
            string method,
            string? customerInfo,
            string? customerPhoneNumber,
            int? customerId)
        {
            _logger.LogInformation("Starting {Method} payment for Order {OrderId}", method, orderId);

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.MenuItem)
                .FirstOrDefaultAsync(o => o.Id == orderId)
                ?? throw new InvalidOperationException("Đơn hàng không tồn tại.");

            EnsurePayable(order);
            await EnsureNoCompletedAsync(orderId);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var customerForPoints = await ResolveCustomerAsync(customerId, customerPhoneNumber, customerInfo, order.CustomerName);

                var payment = new Payment
                {
                    OrderId = orderId,
                    Amount = order.Total,
                    PaymentMethod = method,
                    PaymentDate = DateTime.Now,
                    CustomerInfo = customerInfo ?? order.CustomerName ?? "Không xác định",
                    Status = "Paid",
                    Notes = method == MethodCard ? "Thẻ giả lập — thành công ngay" : "Tiền mặt tại quầy",
                    TransactionId = $"{method.ToUpperInvariant()}-{orderId}-{DateTime.UtcNow.Ticks}",
                    CustomerId = customerForPoints?.Id
                };

                await _unitOfWork.Payments.AddAsync(payment);

                // Stock already deducted when kitchen marked Ready.
                order.Status = "Completed";
                _unitOfWork.Orders.Update(order);
                await ReleaseTableIfIdleAsync(order.TableId, order.Id);

                if (customerForPoints != null && payment.Amount > 0)
                {
                    var pointsEarned = await _customerService.CalculatePointsFromAmount(payment.Amount);
                    if (pointsEarned > 0)
                    {
                        await _customerService.AddPointsAsync(customerForPoints.Id, pointsEarned, payment.Id);
                        payment.PointsEarned = pointsEarned;
                    }

                    await _customerService.UpdateCustomerStatsAsync(customerForPoints.Id);
                }

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("{Method} payment Paid for Order {OrderId}. Payment ID: {PaymentId}", method, orderId, payment.Id);
                return payment;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static void EnsurePayable(Order order)
        {
            if (order.Status == "Completed")
            {
                throw new InvalidOperationException("Đơn hàng đã được thanh toán.");
            }

            if (order.Status == "Cancelled")
            {
                throw new InvalidOperationException("Không thể thanh toán đơn đã hủy.");
            }

            if (order.Status == "PendingMobilePayment")
            {
                throw new InvalidOperationException("Đơn đang chờ xác nhận thanh toán mobile giả lập.");
            }

            if (order.Status != "Ready")
            {
                throw new InvalidOperationException("Chỉ thanh toán được đơn đã xong món (Ready).");
            }
        }

        private async Task EnsureNoCompletedAsync(int orderId)
        {
            var existing = (await _unitOfWork.Payments.FindAsync(p =>
                p.OrderId == orderId &&
                (p.Status == "Paid" || p.Status == "Completed"))).FirstOrDefault();
            if (existing != null)
            {
                throw new InvalidOperationException("Đơn hàng đã được thanh toán.");
            }
        }

        private async Task EnsureNoOpenPendingAsync(int orderId)
        {
            await EnsureNoCompletedAsync(orderId);
            var pending = (await _unitOfWork.Payments.FindAsync(p =>
                p.OrderId == orderId &&
                (p.Status == "Pending" || p.Status == "PendingMobile"))).FirstOrDefault();
            if (pending != null)
            {
                throw new InvalidOperationException($"Đã có giao dịch chờ #{pending.Id}. Xác nhận hoặc giả lập thất bại trước.");
            }
        }

        private async Task ReleaseTableIfIdleAsync(int tableId, int completedOrderId)
        {
            if (tableId <= 0)
            {
                return;
            }

            var table = await _unitOfWork.Tables.GetByIdAsync(tableId);
            if (table == null)
            {
                return;
            }

            var others = await _unitOfWork.Orders.FindAsync(o =>
                o.TableId == tableId &&
                o.Id != completedOrderId &&
                OrderService.IsOpenStatus(o.Status));

            if (!others.Any() && table.Status != "Empty")
            {
                table.Status = "Empty";
                _unitOfWork.Tables.Update(table);
            }
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
            if (existing != null)
            {
                return existing;
            }

            var name = !string.IsNullOrWhiteSpace(customerInfo)
                ? customerInfo
                : (!string.IsNullOrWhiteSpace(orderCustomerName) ? orderCustomerName : $"Khách {customerPhoneNumber}");

            try
            {
                return await _customerService.CreateCustomerAsync(new Customer
                {
                    Name = name!,
                    PhoneNumber = customerPhoneNumber,
                    Email = "",
                    Address = ""
                });
            }
            catch (InvalidOperationException)
            {
                return await _customerService.GetCustomerByPhoneAsync(customerPhoneNumber);
            }
        }
    }
}
