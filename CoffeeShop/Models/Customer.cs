// Models/Customer.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Tên khách hàng")]
        public string Name { get; set; }

        [Required]
        [StringLength(15)]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [StringLength(200)]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Điểm tích lũy")]
        public int LoyaltyPoints { get; set; } = 0;

        [Display(Name = "Hạng thành viên")]
        public string MembershipLevel { get; set; } = "Bronze";

        [Display(Name = "Ngày tham gia")]
        public DateTime JoinDate { get; set; } = DateTime.Now;

        [Display(Name = "Tổng chi tiêu")]
        public decimal TotalSpent { get; set; } = 0;

        [Display(Name = "Số đơn hàng")]
        public int TotalOrders { get; set; } = 0;

        [Display(Name = "Ngày cập nhật")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public virtual ICollection<CustomerPromotion> CustomerPromotions { get; set; } = new List<CustomerPromotion>();
    }
}