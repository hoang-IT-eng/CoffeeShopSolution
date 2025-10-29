// Services/IPaymentService.cs
using CoffeeShop.Models;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public interface IPaymentService
    {
        Task<Payment> ProcessCashPaymentAsync(int orderId, string customerInfo = null, string customerPhoneNumber = null, int? customerId = null);
        Task<Payment> InitiateMobilePaymentAsync(int orderId, string customerInfo = null, string customerPhoneNumber = null, int? customerId = null);
        Task<Payment> CompleteMobilePaymentAsync(int paymentId, bool isSuccess);

        // <<<< XÓA HOẶC COMMENT OUT DÒNG NÀY >>>>
        // Task<Payment> ProcessPaymentAsync(int orderId, string paymentMethod,
        //                              string customerInfo = null,
        //                              string customerPhoneNumber = null,
        //                              int? customerId = null);
        // <<<< KẾT THÚC XÓA/COMMENT >>>>

        Task<Payment> GetPaymentByOrderIdAsync(int orderId);
        Task<Payment> GetPaymentWithDetailsAsync(int paymentId);
    }
}