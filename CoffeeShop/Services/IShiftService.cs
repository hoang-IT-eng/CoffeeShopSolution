using CoffeeShop.Models;

namespace CoffeeShop.Services
{
    public interface IShiftService
    {
        Task<CashShift?> GetCurrentAsync();
        Task<CashShift> OpenAsync(string openedBy, decimal openingFloat = 0, string? notes = null);
        Task<CashShift> CloseAsync(string closedBy, string? notes = null);
        Task<object> MapAsync(CashShift shift);
    }
}
