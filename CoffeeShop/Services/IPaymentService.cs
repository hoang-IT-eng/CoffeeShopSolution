// Services/IPaymentService.cs
using CoffeeShop.Models;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public interface IPaymentService
    {
        Task ProcessPaymentAsync(int orderId, string paymentMethod);
    }
}