using CoffeeShop.Data;
using CoffeeShop.Models;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Services
{
    public class ShiftService : IShiftService
    {
        private readonly ApplicationDbContext _db;

        public ShiftService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<CashShift?> GetCurrentAsync()
        {
            return await _db.CashShifts
                .Where(s => s.Status == "Open")
                .OrderByDescending(s => s.OpenedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<CashShift> OpenAsync(string openedBy, decimal openingFloat = 0, string? notes = null)
        {
            var existing = await GetCurrentAsync();
            if (existing != null)
            {
                throw new InvalidOperationException($"Ca #{existing.Id} đang mở — đóng trước khi mở ca mới.");
            }

            var shift = new CashShift
            {
                OpenedBy = string.IsNullOrWhiteSpace(openedBy) ? "unknown" : openedBy.Trim(),
                OpenedAt = DateTime.Now,
                Status = "Open",
                OpeningFloat = openingFloat < 0 ? 0 : openingFloat,
                Notes = notes
            };
            _db.CashShifts.Add(shift);
            await _db.SaveChangesAsync();
            return shift;
        }

        public async Task<CashShift> CloseAsync(string closedBy, string? notes = null)
        {
            var shift = await GetCurrentAsync()
                ?? throw new InvalidOperationException("Không có ca đang mở.");

            var paid = await _db.Payments
                .Where(p =>
                    p.PaymentDate >= shift.OpenedAt &&
                    (p.Status == "Paid" || p.Status == "Completed"))
                .ToListAsync();

            decimal cash = 0, card = 0, mobile = 0;
            var orderIds = new HashSet<int>();
            foreach (var p in paid)
            {
                orderIds.Add(p.OrderId);
                var method = (p.PaymentMethod ?? "").Trim();
                if (string.Equals(method, PaymentService.MethodCash, StringComparison.OrdinalIgnoreCase))
                    cash += p.Amount;
                else if (string.Equals(method, PaymentService.MethodCard, StringComparison.OrdinalIgnoreCase))
                    card += p.Amount;
                else
                    mobile += p.Amount; // MobileSim / Mobile / aliases
            }

            shift.CashTotal = cash;
            shift.CardTotal = card;
            shift.MobileSimTotal = mobile;
            shift.GrandTotal = cash + card + mobile;
            shift.PaidOrderCount = orderIds.Count;
            shift.ClosedAt = DateTime.Now;
            shift.ClosedBy = string.IsNullOrWhiteSpace(closedBy) ? "unknown" : closedBy.Trim();
            shift.Status = "Closed";
            if (!string.IsNullOrWhiteSpace(notes))
            {
                shift.Notes = string.IsNullOrWhiteSpace(shift.Notes) ? notes : $"{shift.Notes} | {notes}";
            }

            await _db.SaveChangesAsync();
            return shift;
        }

        public Task<object> MapAsync(CashShift shift)
        {
            object payload = new
            {
                id = shift.Id,
                status = shift.Status,
                openedAt = shift.OpenedAt,
                closedAt = shift.ClosedAt,
                openedBy = shift.OpenedBy,
                closedBy = shift.ClosedBy,
                openingFloat = shift.OpeningFloat,
                cashTotal = shift.CashTotal,
                cardTotal = shift.CardTotal,
                mobileSimTotal = shift.MobileSimTotal,
                grandTotal = shift.GrandTotal,
                paidOrderCount = shift.PaidOrderCount,
                notes = shift.Notes,
                isOpen = shift.Status == "Open"
            };
            return Task.FromResult(payload);
        }
    }
}
