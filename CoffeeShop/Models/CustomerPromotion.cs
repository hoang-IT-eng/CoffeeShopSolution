// Models/CustomerPromotion.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models
{
    public class CustomerPromotion
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }

        [Required]
        [StringLength(100)]
        public string PromotionName { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public int PointsUsed { get; set; }

        public decimal DiscountAmount { get; set; }

        public DateTime UsedAt { get; set; } = DateTime.Now;

        [Required]
        public int PaymentId { get; set; }
        public Payment Payment { get; set; }
    }
}