// Services/ICustomerService.cs
using CoffeeShop.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoffeeShop.Services
{
    public interface ICustomerService
    {
        // CRUD Operations
        Task<Customer> CreateCustomerAsync(Customer customer);
        Task<Customer> GetCustomerByIdAsync(int customerId);
        Task<Customer> GetCustomerByPhoneAsync(string phoneNumber);
        Task<IEnumerable<Customer>> GetAllCustomersAsync();
        Task<IEnumerable<Customer>> SearchCustomersAsync(string searchTerm);
        Task UpdateCustomerAsync(Customer customer);
        Task DeleteCustomerAsync(int customerId);

        // Loyalty & Points
        Task AddPointsAsync(int customerId, int points, int paymentId);
        Task UsePointsAsync(int customerId, int points, int paymentId);
        Task<int> CalculatePointsFromAmount(decimal amount);
        Task UpdateMembershipLevelAsync(int customerId);

        // Purchase History
        Task<IEnumerable<Payment>> GetCustomerPurchaseHistoryAsync(int customerId);
        Task<Customer> GetCustomerWithDetailsAsync(int customerId);

        // Statistics
        Task<decimal> GetCustomerTotalSpentAsync(int customerId);
        Task<int> GetCustomerTotalOrdersAsync(int customerId);
        Task UpdateCustomerStatsAsync(int customerId);
    }
}