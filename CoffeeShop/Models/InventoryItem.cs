// Models/InventoryItem.cs
namespace CoffeeShop.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; } // Số lượng (ví dụ: kg, lít)
        public string Unit { get; set; } // Đơn vị (kg, lít, gói)
    }
}