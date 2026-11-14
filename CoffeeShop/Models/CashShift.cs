using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models
{
    public class CashShift
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string OpenedBy { get; set; } = "";

        [MaxLength(100)]
        public string? ClosedBy { get; set; }

        public DateTime OpenedAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Open"; // Open | Closed

        public decimal OpeningFloat { get; set; }

        public decimal CashTotal { get; set; }

        public decimal CardTotal { get; set; }

        public decimal MobileSimTotal { get; set; }

        public decimal GrandTotal { get; set; }

        public int PaidOrderCount { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
