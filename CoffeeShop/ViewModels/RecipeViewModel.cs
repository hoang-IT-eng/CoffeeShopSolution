using System.ComponentModel.DataAnnotations;

namespace CoffeeShop.ViewModels // Make sure the namespace matches your project structure
{
    public class RecipeViewModel
    {
        // This can be useful if you need to track existing recipe IDs during edit
        // public int Id { get; set; } // Or MenuItemRecipeId

        [Required(ErrorMessage = "Please select an ingredient.")]
        public int InventoryItemId { get; set; }

        // This is for display purposes in the view, not for binding back
        // If you populate it in the controller, you can show the name.
        public string? InventoryItemName { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal QuantityRequired { get; set; }
    }
}