// Models/Table.cs
namespace CoffeeShop.Models
{
    public class Table
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Area { get; set; } // Garden, Indoor
        public string Status { get; set; } // Empty, Occupied, Reserved
    }
}