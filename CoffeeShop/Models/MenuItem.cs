// Models/MenuItem.cs
using CoffeeShop.ViewModels;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        [StringLength(255)]
        public string ImageUrl { get; set; } // Optional: path to an image

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = "Uncategorized"; // e.g., Coffee, Tea, Pastry

        public bool IsAvailable { get; set; } = true; // To quickly enable/disable menu items

        // Navigation property for recipes
        public virtual ICollection<MenuItemRecipe> Recipes { get; set; } = new List<MenuItemRecipe>();

    }
}