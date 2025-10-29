// Services/CustomerService.cs
using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // CRUD Operations
        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            // Check if phone number already exists
            var existingCustomer = await GetCustomerByPhoneAsync(customer.PhoneNumber);
            if (existingCustomer != null)
            {
                throw new InvalidOperationException("Số điện thoại đã tồn tại.");
            }

            customer.JoinDate = DateTime.Now;
            customer.LastUpdated = DateTime.Now;
            customer.MembershipLevel = "Bronze";
            customer.LoyaltyPoints = 0;
            customer.TotalSpent = 0;
            customer.TotalOrders = 0;

            await _unitOfWork.Customers.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            return customer;
        }

        public async Task<Customer> GetCustomerByIdAsync(int customerId)
        {
            return await _unitOfWork.Customers.GetByIdAsync(customerId);
        }

        public async Task<Customer> GetCustomerByPhoneAsync(string phoneNumber)
        {
            var customers = await _unitOfWork.Customers.GetAllAsync();
            return customers.FirstOrDefault(c => c.PhoneNumber == phoneNumber);
        }

        public async Task<IEnumerable<Customer>> GetAllCustomersAsync()
        {
            return await _unitOfWork.Customers.GetAllAsync();
        }

        public async Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm)
        {
            var customers = await _unitOfWork.Customers.GetAllAsync();
            return customers.Where(c =>
                c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.PhoneNumber.Contains(searchTerm) ||
                (!string.IsNullOrEmpty(c.Email) && c.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            );
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            customer.LastUpdated = DateTime.Now;
            _unitOfWork.Customers.Update(customer); // Thay UpdateAsync bằng Update
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task DeleteCustomerAsync(int customerId)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer != null)
            {
                _unitOfWork.Customers.Remove(customer); // Thay DeleteAsync bằng Remove
                await _unitOfWork.SaveChangesAsync();
            }
        }

        // Loyalty & Points
        public async Task AddPointsAsync(int customerId, int points, int paymentId)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer != null)
            {
                customer.LoyaltyPoints += points;
                customer.LastUpdated = DateTime.Now;
                await UpdateCustomerAsync(customer);
            }
        }

        public async Task UsePointsAsync(int customerId, int points, int paymentId)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer != null)
            {
                if (customer.LoyaltyPoints < points)
                {
                    throw new InvalidOperationException("Không đủ điểm tích lũy.");
                }

                customer.LoyaltyPoints -= points;
                customer.LastUpdated = DateTime.Now;
                await UpdateCustomerAsync(customer);

                // Create customer promotion record
                var promotion = new CustomerPromotion
                {
                    CustomerId = customerId,
                    PromotionName = "Sử dụng điểm tích lũy",
                    Description = $"Sử dụng {points} điểm tích lũy",
                    PointsUsed = points,
                    DiscountAmount = points * 0.01m, // 1 điểm = 0.01 VND
                    UsedAt = DateTime.Now,
                    PaymentId = paymentId
                };

                await _unitOfWork.CustomerPromotions.AddAsync(promotion);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        public async Task<int> CalculatePointsFromAmount(decimal amount)
        {
            // 1 VND = 0.01 điểm (tức là 100 VND = 1 điểm)
            await Task.CompletedTask; // For async signature
            return (int)(amount / 100);
        }

        public async Task UpdateMembershipLevelAsync(int customerId)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer != null)
            {
                string newLevel = customer.TotalSpent switch
                {
                    >= 10000000 => "Diamond", // >= 10 triệu
                    >= 5000000 => "Gold",     // >= 5 triệu
                    >= 1000000 => "Silver",   // >= 1 triệu
                    _ => "Bronze"
                };

                if (customer.MembershipLevel != newLevel)
                {
                    customer.MembershipLevel = newLevel;
                    await UpdateCustomerAsync(customer);
                }
            }
        }

        // Purchase History
        public async Task<IEnumerable<Payment>> GetCustomerPurchaseHistoryAsync(int customerId)
        {
            var payments = await _unitOfWork.Payments.GetAllAsync();
            return payments.Where(p => p.CustomerId == customerId).OrderByDescending(p => p.PaymentDate);
        }

        public async Task<Customer> GetCustomerWithDetailsAsync(int customerId)
        {
            // For basic implementation, just return customer
            // In real scenario, you might want to include related data
            return await GetCustomerByIdAsync(customerId);
        }

        // Statistics
        public async Task<decimal> GetCustomerTotalSpentAsync(int customerId)
        {
            var payments = await GetCustomerPurchaseHistoryAsync(customerId);
            return payments.Sum(p => p.Amount);
        }

        public async Task<int> GetCustomerTotalOrdersAsync(int customerId)
        {
            var payments = await GetCustomerPurchaseHistoryAsync(customerId);
            return payments.Count();
        }

        public async Task UpdateCustomerStatsAsync(int customerId)
        {
            var customer = await GetCustomerByIdAsync(customerId);
            if (customer != null)
            {
                customer.TotalSpent = await GetCustomerTotalSpentAsync(customerId);
                customer.TotalOrders = await GetCustomerTotalOrdersAsync(customerId);
                await UpdateCustomerAsync(customer);
                await UpdateMembershipLevelAsync(customerId);
            }
        }
    }
}