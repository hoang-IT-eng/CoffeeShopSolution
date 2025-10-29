// Models/Order.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required]
        public int TableId { get; set; }
        public Table Table { get; set; }

        public DateTime CreatedAt { get; set; }

        [Required]
        public string Status { get; set; } = "New";

        public decimal Total { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerName { get; set; }

        // Thêm Navigation Property
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}