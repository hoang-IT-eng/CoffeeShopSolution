// Models/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace CoffeeShop.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string Role { get; set; } // Admin, Cashier, Waiter
    }
}