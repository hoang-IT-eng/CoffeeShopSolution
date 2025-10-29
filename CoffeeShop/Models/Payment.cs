// Models/Payment.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models
{
    public class Payment
    {
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }
        public Order Order { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [StringLength(200)] // Xóa [Required] để cho phép NULL
        public string CustomerInfo { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Processing";

        [StringLength(100)]
        public string TransactionId { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        public int? CustomerId { get; set; }
        public Customer Customer { get; set; }

        public int PointsEarned { get; set; } = 0;
        public int PointsUsed { get; set; } = 0;
        public decimal DiscountFromPoints { get; set; } = 0;

        public virtual ICollection<CustomerPromotion> CustomerPromotions { get; set; } = new List<CustomerPromotion>();
    }
}