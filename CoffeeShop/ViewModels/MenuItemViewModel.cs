using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
// Potentially: using CoffeeShop.Models; if you need to reference domain models directly for some reason

namespace CoffeeShop.ViewModels // Make sure the namespace matches your project structure
{
    public class MenuItemViewModel
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
        public string ImageUrl { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = "Uncategorized";

        public bool IsAvailable { get; set; } = true;

        // This list will hold the recipe details for the form
        public List<RecipeViewModel> Recipes { get; set; } = new List<RecipeViewModel>();
    }
}