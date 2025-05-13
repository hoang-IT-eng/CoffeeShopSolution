// Services/PaymentService.cs
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using System;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PaymentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task ProcessPaymentAsync(int orderId, string paymentMethod)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null || order.Status == "Completed")
            {
                throw new InvalidOperationException("Đơn hàng không hợp lệ hoặc đã được thanh toán.");
            }

            var payment = new Payment
            {
                OrderId = orderId,
                Amount = order.Total,
                PaymentMethod = paymentMethod,
                PaymentDate = DateTime.Now
            };

            await _unitOfWork.Payments.AddAsync(payment);
            order.Status = "Completed";
            _unitOfWork.Orders.Update(order);

            await _unitOfWork.SaveChangesAsync();
        }
    }
}