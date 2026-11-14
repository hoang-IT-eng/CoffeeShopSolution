using CoffeeShop.Models;
using CoffeeShop.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Data
{
    public static class SeedData
    {
        private const string SampleCustomer = "Mẫu - Minh Anh";

        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var roleName in new[] { "Admin", "Waiter", "Cashier" })
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole(roleName));
            }

            // Stable demo accounts used by the React admin login screen.
            await EnsureUser(userManager, "newadmin@coffee.com", "NewAdmin@123", "Admin An Roastery", "Admin");
            await EnsureUser(userManager, "admin@coffee.com", "Admin@123", "Quản trị viên", "Admin");
            await EnsureUser(userManager, "waiter@coffee.com", "Waiter@123", "Lê Quốc Bảo", "Waiter");
            await EnsureUser(userManager, "cashier@coffee.com", "Cashier@123", "Nguyễn Thu Hà", "Cashier");

            var tables = new Dictionary<string, Table>();
            foreach (var spec in new (string Name, string Area, string Status)[]
            {
                ("T1-01", "Trong nhà", "Empty"),
                ("T1-02", "Trong nhà", "Occupied"),
                ("T1-03", "Trong nhà", "Occupied"),
                ("T1-04", "Trong nhà", "Reserved"),
                ("T2-01", "Sân vườn", "Empty"),
                ("T2-02", "Sân vườn", "Occupied"),
                ("T2-03", "Sân vườn", "Empty"),
                ("OT-01", "Ngoài trời", "Empty")
            })
            {
                var table = await context.Tables.FirstOrDefaultAsync(t => t.Name == spec.Name);
                if (table == null)
                {
                    table = new Table { Name = spec.Name, Area = spec.Area, Status = spec.Status };
                    context.Tables.Add(table);
                    await context.SaveChangesAsync();
                }
                tables[spec.Name] = table;
            }

            var menu = new Dictionary<string, MenuItem>();
            foreach (var spec in new (string Name, decimal Price, string Category, string Description)[]
            {
                ("Cà phê Muối", 45000, "Cà phê", "Espresso, sữa đặc, muối biển"),
                ("Bạc xỉu", 42000, "Cà phê", "Sữa nhiều, cà phê vừa"),
                ("Espresso", 35000, "Cà phê", "Đậm, ngắn"),
                ("Cappuccino", 45000, "Cà phê", "Espresso + sữa nóng"),
                ("Trà đào cam sả", 52000, "Trà", "Đào ngâm, cam, sả"),
                ("Cold brew", 49000, "Cà phê", "Ủ lạnh 12 giờ"),
                ("Croissant bơ", 38000, "Bánh", "Bơ Pháp, nướng trong ngày")
            })
            {
                var item = await context.MenuItems.FirstOrDefaultAsync(m => m.Name == spec.Name);
                if (item == null)
                {
                    item = new MenuItem
                    {
                        Name = spec.Name,
                        Price = spec.Price,
                        Category = spec.Category,
                        Description = spec.Description,
                        ImageUrl = "",
                        IsAvailable = true
                    };
                    context.MenuItems.Add(item);
                    await context.SaveChangesAsync();
                }
                menu[spec.Name] = item;
            }

            foreach (var spec in new (string Name, decimal Qty, decimal Min, string Unit, int ExpiryDays)[]
            {
                ("Hạt Arabica Cầu Đất", 4, 8, "kg", 40),
                ("Sữa tươi không đường", 2, 6, "lít", 3),
                ("Đào ngâm", 1.2m, 2, "kg", -2),
                ("Cam tươi", 8, 3, "kg", 20),
                ("Bơ lạt", 0.4m, 1, "kg", 5),
                ("Đường mía", 12, 3, "kg", 180)
            })
            {
                if (await context.InventoryItems.AnyAsync(i => i.Name == spec.Name))
                    continue;
                context.InventoryItems.Add(new InventoryItem
                {
                    Name = spec.Name,
                    Quantity = spec.Qty,
                    MinimumThreshold = spec.Min,
                    Unit = spec.Unit,
                    ExpirationDate = DateTime.Today.AddDays(spec.ExpiryDays)
                });
            }
            await context.SaveChangesAsync();

            var beans = await context.InventoryItems.FirstOrDefaultAsync(i => i.Name == "Hạt Arabica Cầu Đất");
            var milk = await context.InventoryItems.FirstOrDefaultAsync(i => i.Name == "Sữa tươi không đường");
            if (beans != null && menu.TryGetValue("Espresso", out var espresso) &&
                !await context.MenuItemRecipes.AnyAsync(r => r.MenuItemId == espresso.Id && r.InventoryItemId == beans.Id))
            {
                context.MenuItemRecipes.Add(new MenuItemRecipe
                {
                    MenuItemId = espresso.Id,
                    InventoryItemId = beans.Id,
                    QuantityRequired = 0.018m
                });
            }
            if (beans != null && milk != null && menu.TryGetValue("Cappuccino", out var cappuccino) &&
                !await context.MenuItemRecipes.AnyAsync(r => r.MenuItemId == cappuccino.Id))
            {
                context.MenuItemRecipes.AddRange(
                    new MenuItemRecipe { MenuItemId = cappuccino.Id, InventoryItemId = beans.Id, QuantityRequired = 0.018m },
                    new MenuItemRecipe { MenuItemId = cappuccino.Id, InventoryItemId = milk.Id, QuantityRequired = 0.15m }
                );
            }
            await context.SaveChangesAsync();

            await EnsureCustomer(context, "Minh Anh", "0903100001", "minhanh@example.com", "Gold", 320);
            await EnsureCustomer(context, "Thu Hà", "0903100002", "thuha@example.com", "Silver", 140);
            await EnsureCustomer(context, "Hoàng Nam", "0903100003", "hoangnam@example.com", "Bronze", 40);
            await EnsureCustomer(context, "Tuấn Kiệt", "0903100004", "tuankiet@example.com", "Bronze", 15);

            if (await context.Orders.AnyAsync(o => o.CustomerName == SampleCustomer))
                return;

            var today = DateTime.Today;
            var samples = new (int DayOffset, int Hour, string Table, string Customer, string Status, (string Item, int Qty)[] Lines)[]
            {
                (0, 8, "T1-02", SampleCustomer, "Processing", new[] { ("Cà phê Muối", 1), ("Croissant bơ", 1) }),
                (0, 9, "T1-03", "Mẫu - Thu Hà", "Ready", new[] { ("Bạc xỉu", 2) }),
                (0, 10, "T2-02", "Mẫu - Hoàng Nam", "New", new[] { ("Cold brew", 1), ("Trà đào cam sả", 1) }),
                (0, 11, "OT-01", "Mẫu - Tuấn Kiệt", "Completed", new[] { ("Espresso", 2) }),
                (1, 15, "T1-01", "Mẫu - Minh Anh", "Completed", new[] { ("Cappuccino", 1), ("Croissant bơ", 2) }),
                (2, 9, "T2-01", "Mẫu - Thu Hà", "Completed", new[] { ("Cà phê Muối", 2), ("Bạc xỉu", 1) }),
                (3, 16, "T1-04", "Mẫu - Hoàng Nam", "Completed", new[] { ("Trà đào cam sả", 3) }),
                (4, 8, "T2-03", "Mẫu - Tuấn Kiệt", "Completed", new[] { ("Cold brew", 2) }),
                (5, 14, "T1-02", "Mẫu - Minh Anh", "Completed", new[] { ("Espresso", 1), ("Cappuccino", 1) }),
                (6, 10, "OT-01", "Mẫu - Thu Hà", "Completed", new[] { ("Bạc xỉu", 2), ("Croissant bơ", 1) })
            };

            foreach (var sample in samples)
            {
                if (!tables.TryGetValue(sample.Table, out var table))
                    continue;
                var lines = sample.Lines
                    .Where(line => menu.ContainsKey(line.Item))
                    .Select(line => (Item: menu[line.Item], line.Qty))
                    .ToList();
                if (lines.Count == 0)
                    continue;

                var created = today.AddDays(-sample.DayOffset).AddHours(sample.Hour);
                var order = new Order
                {
                    TableId = table.Id,
                    CreatedAt = created,
                    Status = sample.Status,
                    CustomerName = sample.Customer,
                    Total = lines.Sum(line => line.Item.Price * line.Qty)
                };
                context.Orders.Add(order);
                await context.SaveChangesAsync();

                context.OrderDetails.AddRange(lines.Select(line => new OrderDetail
                {
                    OrderId = order.Id,
                    MenuItemId = line.Item.Id,
                    Quantity = line.Qty,
                    Price = line.Item.Price
                }));

                if (sample.Status == "Completed")
                {
                    context.Payments.Add(new Payment
                    {
                        OrderId = order.Id,
                        Amount = order.Total,
                        PaymentMethod = sample.DayOffset % 2 == 0 ? "Cash" : "Mobile",
                        PaymentDate = created.AddMinutes(20),
                        Status = "Completed",
                        CustomerInfo = sample.Customer,
                        TransactionId = $"MAU-{order.Id}",
                        Notes = ""
                    });
                }
            }

            // Align floor status with open sample tickets.
            foreach (var table in tables.Values)
            {
                var hasOpen = await context.Orders.AnyAsync(o =>
                    o.TableId == table.Id &&
                    (o.Status == "New" || o.Status == "Processing" || o.Status == "Ready" || o.Status == "PendingMobilePayment"));
                table.Status = hasOpen ? "Occupied" : (table.Status == "Reserved" ? "Reserved" : "Empty");
            }

            await context.SaveChangesAsync();
        }

        private static async Task EnsureUser(UserManager<ApplicationUser> users, string email, string password, string fullName, string role)
        {
            var user = await users.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    Role = role,
                    EmailConfirmed = true
                };
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded)
                    return;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(user.FullName))
                {
                    user.FullName = fullName;
                    await users.UpdateAsync(user);
                }
            }

            if (!await users.IsInRoleAsync(user, role))
                await users.AddToRoleAsync(user, role);
        }

        private static async Task EnsureCustomer(ApplicationDbContext context, string name, string phone, string email, string level, int points)
        {
            if (await context.Customers.AnyAsync(c => c.PhoneNumber == phone))
                return;
            context.Customers.Add(new Customer
            {
                Name = name,
                PhoneNumber = phone,
                Email = email,
                Address = "Quận 1, TP. Hồ Chí Minh",
                MembershipLevel = level,
                LoyaltyPoints = points,
                JoinDate = DateTime.Today.AddMonths(-2),
                LastUpdated = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }
}
