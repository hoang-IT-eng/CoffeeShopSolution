using CoffeeShop.Models;

namespace CoffeeShop.Services
{
    public interface IPaymentService
    {
        Task<Payment> ProcessCashPaymentAsync(int orderId, string? customerInfo = null, string? customerPhoneNumber = null, int? customerId = null);
        Task<Payment> InitiateMobilePaymentAsync(int orderId, string? customerInfo = null, string? customerPhoneNumber = null, int? customerId = null);
        Task<Payment> CompleteMobilePaymentAsync(int paymentId, bool isSuccess);
        Task<Payment?> GetPaymentByOrderIdAsync(int orderId);
        Task<Payment?> GetPaymentWithDetailsAsync(int paymentId);
    }
}
