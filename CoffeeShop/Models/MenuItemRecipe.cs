using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CoffeeShop.Models;

namespace CoffeeShop.ViewModels
{
    // Kept in ViewModels namespace to match existing EF migrations/snapshots.
    public class MenuItemRecipe
    {
        public int Id { get; set; }

        [Required]
        public int MenuItemId { get; set; }

        [ForeignKey("MenuItemId")]
        public virtual MenuItem? MenuItem { get; set; }

        [Required]
        public int InventoryItemId { get; set; }

        [ForeignKey("InventoryItemId")]
        public virtual InventoryItem? InventoryItem { get; set; }

        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal QuantityRequired { get; set; }
    }
}
