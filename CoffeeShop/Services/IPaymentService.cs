using CoffeeShop.Models;

namespace CoffeeShop.Services
{
    public interface IPaymentService
    {
        Task<Payment> ProcessCashPaymentAsync(int orderId, string? customerInfo = null, string? customerPhoneNumber = null, int? customerId = null);
        Task<Payment> ProcessCardPaymentAsync(int orderId, string? customerInfo = null, string? customerPhoneNumber = null, int? customerId = null);
        Task<Payment> InitiateSimulatedPaymentAsync(int orderId, string? customerInfo = null, string? customerPhoneNumber = null, int? customerId = null);
        Task<Payment> CompleteSimulatedPaymentAsync(int paymentId, bool isSuccess, string source = "confirm");
        Task<Payment> GetPaymentByOrderIdAsync(int orderId);
        Task<Payment?> GetPaymentWithDetailsAsync(int paymentId);

        /// <summary>Normalize API statuses: Pending, Paid, Failed (plus legacy Completed/PendingMobile).</summary>
        static string CanonicalStatus(string? status) => status switch
        {
            "Completed" or "Paid" => "Paid",
            "PendingMobile" or "Pending" => "Pending",
            "Failed" => "Failed",
            _ => status ?? "Pending"
        };

        static bool IsPending(string? status)
        {
            var s = CanonicalStatus(status);
            return s == "Pending";
        }

        static bool IsPaid(string? status) => CanonicalStatus(status) == "Paid";
    }
}
