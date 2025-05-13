// Models/Payment.cs
using System;

namespace CoffeeShop.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } // Cash, Card, Mobile
        public DateTime PaymentDate { get; set; }
    }
}