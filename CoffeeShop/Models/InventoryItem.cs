// Models/InventoryItem.cs
using CoffeeShop.ViewModels;
using System;
using System.Collections.Generic; // Add this
using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public decimal Quantity { get; set; }
        [Required]
        [StringLength(50)]
        public string Unit { get; set; }
        [Required]
        [Range(0, double.MaxValue)]
        public decimal MinimumThreshold { get; set; }
        [Required]
        public DateTime ExpirationDate { get; set; }

        // Navigation property (optional but good practice)
        public virtual ICollection<MenuItemRecipe> MenuItemRecipes { get; set; } = new List<MenuItemRecipe>();
    }
}