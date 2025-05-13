// Models/MenuItemRecipe.cs
namespace CoffeeShop.Models
{
    public class MenuItemRecipe
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public MenuItem MenuItem { get; set; }
        public int InventoryItemId { get; set; }
        public InventoryItem InventoryItem { get; set; }
        public decimal QuantityRequired { get; set; } // Số lượng nguyên liệu cần cho 1 món
    }
}