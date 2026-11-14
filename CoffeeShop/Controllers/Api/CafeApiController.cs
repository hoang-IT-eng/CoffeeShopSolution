using CoffeeShop.Data.UnitOfWork;
using CoffeeShop.Hubs;
using CoffeeShop.Models;
using CoffeeShop.Services;
using CoffeeShop.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CoffeeShop.Controllers.Api
{
    [ApiController]
    [Route("api")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class CafeApiController : ControllerBase
    {
        private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Admin", "Cashier", "Waiter"
        };

        private static readonly Dictionary<string, string[]> StatusTransitions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["New"] = new[] { "Processing", "Cancelled" },
            ["Processing"] = new[] { "Ready", "Cancelled" },
            ["Ready"] = new[] { "Cancelled" }
        };

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderService _orderService;
        private readonly IInventoryService _inventoryService;
        private readonly IPaymentService _paymentService;
        private readonly ICustomerService _customerService;
        private readonly IShiftService _shiftService;
        private readonly IHubContext<KitchenHub> _kitchenHub;

        public CafeApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IUnitOfWork unitOfWork,
            IOrderService orderService,
            IInventoryService inventoryService,
            IPaymentService paymentService,
            ICustomerService customerService,
            IShiftService shiftService,
            IHubContext<KitchenHub> kitchenHub)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
            _inventoryService = inventoryService;
            _paymentService = paymentService;
            _customerService = customerService;
            _shiftService = shiftService;
            _kitchenHub = kitchenHub;
        }

        [AllowAnonymous]
        [HttpPost("auth/login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email và mật khẩu là bắt buộc." });
            }

            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user == null)
            {
                return BadRequest(new { message = "Email không tồn tại." });
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                request.Password,
                request.RememberMe,
                lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                return BadRequest(new { message = "Mật khẩu không đúng." });
            }

            return Ok(await Profile(user));
        }

        [HttpPost("auth/logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { ok = true });
        }

        [AllowAnonymous]
        [HttpGet("auth/me")]
        public async Task<IActionResult> Me()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Ok(new { authenticated = false });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Ok(new { authenticated = false });
            }

            return Ok(await Profile(user));
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard([FromQuery] string? range)
        {
            var key = NormalizeRange(range);

            var today = DateTime.Today;
            var end = today.AddDays(1);
            var start = key switch
            {
                "week" => today.AddDays(-6),
                "month" => today.AddDays(-29),
                "year" => new DateTime(today.Year, 1, 1),
                _ => today
            };
            var spanDays = Math.Max(1, (end - start).Days);
            var prevStart = start.AddDays(-spanDays);
            var weekStart = today.AddDays(-6);
            var fetchFrom = prevStart < weekStart ? prevStart : weekStart;

            var orders = (await _unitOfWork.Orders.FindWithIncludeAsync(
                o => o.CreatedAt >= fetchFrom && o.CreatedAt < end,
                o => o.Table)).ToList();
            var current = orders.Where(o => o.CreatedAt >= start && o.CreatedAt < end).ToList();
            var previous = orders.Where(o => o.CreatedAt >= prevStart && o.CreatedAt < start).ToList();
            var todayOrders = orders.Where(o => o.CreatedAt >= today && o.CreatedAt < end).ToList();

            var open = (await _unitOfWork.Orders.FindWithIncludeAsync(
                o => o.Status == "New" || o.Status == "Processing" || o.Status == "Ready" || o.Status == "PendingMobilePayment",
                o => o.Table)).ToList();
            var tables = (await _unitOfWork.Tables.GetAllAsync())
                .OrderBy(t => t.Area)
                .ThenBy(t => t.Name)
                .ToList();

            var lowStock = (await _inventoryService.GetLowStockItemsAsync()).ToList();
            var nearExpiry = (await _inventoryService.GetExpiringItemsAsync(7)).ToList();
            var expired = (await _inventoryService.GetItemsByExpirationAsync(today))
                .Where(i => i.ExpirationDate < today)
                .ToList();

            var points = new List<object>();
            if (key == "today")
            {
                for (var hour = 7; hour <= 22; hour++)
                    points.Add(ChartPoint($"{hour}:00", current.Where(o => o.CreatedAt.Hour == hour)));
            }
            else if (key == "year")
            {
                for (var month = 1; month <= today.Month; month++)
                    points.Add(ChartPoint($"T{month}", current.Where(o => o.CreatedAt.Month == month && o.CreatedAt.Year == today.Year)));
            }
            else
            {
                for (var day = start.Date; day < end; day = day.AddDays(1))
                {
                    var next = day.AddDays(1);
                    points.Add(ChartPoint(day.ToString("dd/MM"), current.Where(o => o.CreatedAt >= day && o.CreatedAt < next)));
                }
            }

            var week = new List<object>();
            var weekLabels = new List<string>();
            var weekTotals = new List<decimal>();
            for (var day = weekStart; day < end; day = day.AddDays(1))
            {
                var next = day.AddDays(1);
                var total = orders.Where(o => o.CreatedAt >= day && o.CreatedAt < next).Sum(o => o.Total);
                week.Add(new { label = day.ToString("dd/MM"), revenue = total, isToday = day == today });
                weekLabels.Add(day.ToString("dd/MM"));
                weekTotals.Add(total);
            }

            var todayHours = Enumerable.Range(7, 16).Select(hour => new
            {
                label = hour.ToString(),
                revenue = todayOrders.Where(o => o.CreatedAt.Hour == hour).Sum(o => o.Total)
            });

            var hoursOpen = DateTime.Now <= today.AddHours(7)
                ? 1
                : Math.Max(1, (DateTime.Now - today.AddHours(7)).TotalHours);

            var recentSource = open.Count > 0
                ? open.OrderByDescending(o => o.CreatedAt)
                : current.OrderByDescending(o => o.CreatedAt);

            return Ok(new
            {
                range = key,
                revenue = current.Sum(o => o.Total),
                previousRevenue = previous.Sum(o => o.Total),
                orderCount = current.Count,
                previousOrderCount = previous.Count,
                openNew = open.Count(o => o.Status == "New"),
                openMaking = open.Count(o => o.Status == "Processing"),
                openReady = open.Count(o => o.Status is "Ready" or "PendingMobilePayment"),
                doneInRange = current.Count(o => o.Status == "Completed"),
                todayRevenue = todayOrders.Sum(o => o.Total),
                todayOrders = todayOrders.Count,
                ordersPerHour = Math.Round(todayOrders.Count / hoursOpen, 1),
                tablesInUse = tables.Count(t => t.Status == "Occupied"),
                tablesEmpty = tables.Count(t => t.Status is "Empty" or "Trống"),
                tablesReserved = tables.Count(t => t.Status == "Reserved"),
                tableCount = tables.Count,
                tables = tables.Select(t => new { id = t.Id, name = t.Name, area = t.Area, status = t.Status }),
                totalWarnings = lowStock.Count + nearExpiry.Count + expired.Count,
                lowStock = lowStock.Select(MapInventory),
                nearExpiry = nearExpiry.Select(MapInventory),
                expired = expired.Select(MapInventory),
                points,
                week,
                todayHours,
                recentKind = open.Count > 0 ? "open" : "range",
                recentOrders = recentSource.Take(6).Select(MapOrder),
                dailyRevenue = todayOrders.Sum(o => o.Total),
                newOrders = open.Count(o => o.Status == "New"),
                processingOrders = open.Count(o => o.Status == "Processing"),
                chartLabels = weekLabels,
                chartData = weekTotals
            });
        }

        [HttpGet("home")]
        public async Task<IActionResult> Home()
        {
            var tables = (await _unitOfWork.Tables.GetAllAsync()).ToList();
            var pending = await _orderService.GetPendingOrdersAsync();
            return Ok(new
            {
                emptyTables = tables.Count(t => t.Status == "Empty"),
                occupiedTables = tables.Count(t => t.Status == "Occupied"),
                reservedTables = tables.Count(t => t.Status == "Reserved"),
                orders = pending.Select(MapOrder)
            });
        }

        [Authorize(Roles = "Waiter,Admin,Cashier")]
        [HttpGet("orders")]
        public async Task<IActionResult> Orders()
        {
            var since = DateTime.Today.AddDays(-60);
            var orders = (await _unitOfWork.Orders.FindWithIncludeAsync(
                o => o.CreatedAt >= since,
                o => o.Table))
                .OrderByDescending(o => o.CreatedAt)
                .Take(80)
                .ToList();

            var ids = orders.Select(o => o.Id).ToList();
            var details = ids.Count == 0
                ? new List<OrderDetail>()
                : (await _unitOfWork.OrderDetails.FindAsync(d => ids.Contains(d.OrderId))).ToList();
            var menuIds = details.Select(d => d.MenuItemId).Distinct().ToList();
            var menus = menuIds.Count == 0
                ? new Dictionary<int, MenuItem>()
                : (await _unitOfWork.MenuItems.FindAsync(m => menuIds.Contains(m.Id))).ToDictionary(m => m.Id);

            foreach (var order in orders)
            {
                order.OrderDetails = details.Where(d => d.OrderId == order.Id).ToList();
                foreach (var detail in order.OrderDetails)
                {
                    if (menus.TryGetValue(detail.MenuItemId, out var item))
                        detail.MenuItem = item;
                }
            }

            return Ok(orders.Select(MapOrder));
        }

        [Authorize(Roles = "Waiter,Admin,Cashier")]
        [HttpGet("orders/{id:int}")]
        public async Task<IActionResult> Order(int id)
        {
            var order = await _orderService.GetOrderWithDetailsAsync(id);
            return order == null ? NotFound(new { message = "Không tìm thấy đơn hàng." }) : Ok(MapOrder(order));
        }

        [Authorize(Roles = "Waiter,Admin,Cashier")]
        [HttpGet("orders/form")]
        public async Task<IActionResult> OrderForm()
        {
            var tables = await _unitOfWork.Tables.GetAllAsync();
            var menu = await _unitOfWork.MenuItems.GetAllAsync();
            return Ok(new
            {
                tables = tables
                    .OrderBy(t => t.Area)
                    .ThenBy(t => t.Name)
                    .Select(t => new { id = t.Id, name = t.Name, area = t.Area, status = t.Status }),
                menuItems = menu.Where(m => m.IsAvailable).Select(MapMenu)
            });
        }

        [Authorize(Roles = "Waiter,Admin,Cashier")]
        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request.TableId <= 0 || string.IsNullOrWhiteSpace(request.CustomerName) || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { message = "Cần chọn bàn, tên khách và ít nhất một món." });
            }

            var table = await _unitOfWork.Tables.GetByIdAsync(request.TableId);
            if (table == null)
            {
                return BadRequest(new { message = "Bàn không tồn tại." });
            }

            var details = request.Items
                .Where(i => i.MenuItemId > 0 && i.Quantity > 0)
                .Select(i => new OrderDetail { MenuItemId = i.MenuItemId, Quantity = i.Quantity })
                .ToList();
            if (details.Count == 0)
            {
                return BadRequest(new { message = "Chi tiết đơn hàng không hợp lệ." });
            }

            var (available, missing) = await _inventoryService.CheckAvailabilityWithDetailsAsync(details);
            if (!available)
            {
                return BadRequest(new { message = string.Join(" ", missing) });
            }

            decimal total = 0;
            foreach (var detail in details)
            {
                var menuItem = await _unitOfWork.MenuItems.GetByIdAsync(detail.MenuItemId);
                if (menuItem == null || !menuItem.IsAvailable)
                {
                    return BadRequest(new { message = $"Món {detail.MenuItemId} không khả dụng." });
                }
                detail.Price = menuItem.Price;
                total += detail.Quantity * menuItem.Price;
            }

            var order = new Order
            {
                TableId = request.TableId,
                CustomerName = request.CustomerName.Trim(),
                Total = total,
                CreatedAt = DateTime.Now,
                Status = "New"
            };
            await _unitOfWork.Orders.AddAsync(order);
            await _unitOfWork.SaveChangesAsync();

            foreach (var detail in details)
            {
                detail.OrderId = order.Id;
                await _unitOfWork.OrderDetails.AddAsync(detail);
            }
            await _unitOfWork.SaveChangesAsync();

            if (table.Status != "Occupied")
            {
                table.Status = "Occupied";
                _unitOfWork.Tables.Update(table);
                await _unitOfWork.SaveChangesAsync();
            }

            var created = await _orderService.GetOrderWithDetailsAsync(order.Id);
            if (created != null)
            {
                await BroadcastKitchenAsync("orderCreated", created);
            }

            return Ok(new { id = order.Id, message = $"Đã tạo đơn hàng #{order.Id}." });
        }

        [Authorize(Roles = "Waiter,Admin,Cashier")]
        [HttpPost("orders/{id:int}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] StatusRequest request)
        {
            var order = await _orderService.GetOrderWithDetailsAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn hàng." });
            }

            var next = (request.Status ?? "").Trim();
            if (!StatusTransitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(next, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = $"Không chuyển được từ {order.Status} sang {next}." });
            }

            if (string.Equals(next, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return await CancelOrder(id);
            }

            if (string.Equals(next, "Ready", StringComparison.OrdinalIgnoreCase) && order.Status != "Ready")
            {
                var (available, missing) = await _inventoryService.CheckAvailabilityWithDetailsAsync(order.OrderDetails.ToList());
                if (!available)
                {
                    return BadRequest(new { message = "Không đủ nguyên liệu: " + string.Join(", ", missing) });
                }
                await _inventoryService.DeductStockAsync(order.OrderDetails.ToList());
            }

            await _orderService.UpdateStatusAsync(id, next);
            var updated = await _orderService.GetOrderWithDetailsAsync(id);
            if (updated != null)
            {
                await BroadcastKitchenAsync("orderStatusChanged", updated);
            }

            return Ok(new { message = $"Đơn #{id} → {next}." });
        }

        [Authorize(Roles = "Waiter,Admin,Cashier")]
        [HttpPost("orders/{id:int}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _orderService.GetOrderWithDetailsAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn hàng." });
            }

            if (order.Status is "Completed" or "Cancelled")
            {
                return BadRequest(new { message = $"Không hủy được đơn trạng thái {order.Status}." });
            }

            if (order.Status is "Ready" or "PendingMobilePayment")
            {
                await _inventoryService.RestockFromOrderAsync(order.OrderDetails.ToList());
            }

            await _orderService.UpdateStatusAsync(id, "Cancelled");
            await ReleaseTableIfIdleAsync(order.TableId, id);
            var cancelled = await _orderService.GetOrderWithDetailsAsync(id);
            if (cancelled != null)
            {
                await BroadcastKitchenAsync("orderCancelled", cancelled);
            }

            return Ok(new { message = $"Đơn #{id} đã được hủy." });
        }

        [HttpGet("tables")]
        public async Task<IActionResult> Tables()
        {
            var tables = (await _unitOfWork.Tables.GetAllAsync())
                .OrderBy(t => t.Area)
                .ThenBy(t => t.Name)
                .ToList();
            var openOrders = (await _orderService.GetPendingOrdersAsync()).ToList();

            return Ok(tables.Select(t =>
            {
                var open = openOrders
                    .Where(o => o.TableId == t.Id)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefault();
                return new
                {
                    id = t.Id,
                    name = t.Name,
                    area = t.Area,
                    status = t.Status,
                    openOrderId = open?.Id,
                    openOrderStatus = open?.Status,
                    openOrderTotal = open?.Total
                };
            }));
        }

        [Authorize(Roles = "Waiter,Admin,Cashier")]
        [HttpPost("tables/{id:int}/status")]
        public async Task<IActionResult> UpdateTable(int id, [FromBody] StatusRequest request)
        {
            if (request.Status is not ("Empty" or "Occupied" or "Reserved"))
            {
                return BadRequest(new { message = "Trạng thái không hợp lệ." });
            }

            var table = await _unitOfWork.Tables.GetByIdAsync(id);
            if (table == null)
            {
                return NotFound(new { message = "Không tìm thấy bàn." });
            }

            if (request.Status == "Empty")
            {
                var open = await _unitOfWork.Orders.FindAsync(o =>
                    o.TableId == id && OrderService.IsOpenStatus(o.Status));
                if (open.Any())
                {
                    return BadRequest(new { message = "Bàn còn đơn đang mở — không thể đặt Trống." });
                }
            }

            table.Status = request.Status;
            _unitOfWork.Tables.Update(table);
            await _unitOfWork.SaveChangesAsync();
            return Ok(new { ok = true, id = table.Id, status = table.Status });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("menu")]
        public async Task<IActionResult> Menu()
        {
            var items = await _unitOfWork.MenuItems.GetAllAsync();
            return Ok(items.OrderBy(m => m.Category).ThenBy(m => m.Name).Select(MapMenu));
        }

        // ingredients before {id} so "ingredients" is never ambiguous.
        [Authorize(Roles = "Admin")]
        [HttpGet("menu/ingredients")]
        public async Task<IActionResult> Ingredients()
        {
            var inventory = await _unitOfWork.InventoryItems.GetAllAsync();
            return Ok(inventory.OrderBy(i => i.Name).Select(i => new { id = i.Id, name = i.Name, unit = i.Unit }));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("menu/{id:int}")]
        public async Task<IActionResult> MenuItem(int id)
        {
            var item = await _unitOfWork.MenuItems.GetByIdWithIncludesAsync(id, m => m.Recipes);
            if (item == null)
            {
                return NotFound(new { message = "Không tìm thấy món." });
            }

            var inventory = (await _unitOfWork.InventoryItems.GetAllAsync()).ToDictionary(i => i.Id, i => i.Name);
            return Ok(new
            {
                item = MapMenu(item),
                recipes = item.Recipes.Select(r => new
                {
                    inventoryItemId = r.InventoryItemId,
                    inventoryItemName = inventory.GetValueOrDefault(r.InventoryItemId, ""),
                    quantityRequired = r.QuantityRequired
                }),
                inventory = inventory.Select(pair => new { id = pair.Key, name = pair.Value })
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("menu")]
        public async Task<IActionResult> CreateMenu([FromBody] MenuWriteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price <= 0)
            {
                return BadRequest(new { message = "Tên và giá món là bắt buộc." });
            }

            var menuItem = new MenuItem
            {
                Name = request.Name.Trim(),
                Price = request.Price,
                Description = request.Description ?? "",
                ImageUrl = request.ImageUrl ?? "",
                Category = string.IsNullOrWhiteSpace(request.Category) ? "Uncategorized" : request.Category.Trim(),
                IsAvailable = request.IsAvailable
            };
            await _unitOfWork.MenuItems.AddAsync(menuItem);
            await _unitOfWork.SaveChangesAsync();
            await SaveRecipes(menuItem.Id, request.Recipes);
            return Ok(new { id = menuItem.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("menu/{id:int}")]
        public async Task<IActionResult> UpdateMenu(int id, [FromBody] MenuWriteRequest request)
        {
            var menuItem = await _unitOfWork.MenuItems.GetByIdWithIncludesAsync(id, m => m.Recipes);
            if (menuItem == null)
            {
                return NotFound(new { message = "Không tìm thấy món." });
            }

            if (string.IsNullOrWhiteSpace(request.Name) || request.Price <= 0)
            {
                return BadRequest(new { message = "Tên và giá món là bắt buộc." });
            }

            menuItem.Name = request.Name.Trim();
            menuItem.Price = request.Price;
            menuItem.Description = request.Description ?? "";
            menuItem.ImageUrl = request.ImageUrl ?? "";
            menuItem.Category = string.IsNullOrWhiteSpace(request.Category) ? "Uncategorized" : request.Category.Trim();
            menuItem.IsAvailable = request.IsAvailable;
            _unitOfWork.MenuItems.Update(menuItem);
            foreach (var recipe in menuItem.Recipes.ToList())
            {
                _unitOfWork.MenuItemRecipes.Remove(recipe);
            }
            await _unitOfWork.SaveChangesAsync();
            await SaveRecipes(menuItem.Id, request.Recipes);
            return Ok(new { id = menuItem.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("menu/{id:int}")]
        public async Task<IActionResult> DeleteMenu(int id)
        {
            var menuItem = await _unitOfWork.MenuItems.GetByIdWithIncludesAsync(id, m => m.Recipes);
            if (menuItem == null)
            {
                return NotFound(new { message = "Không tìm thấy món." });
            }

            var used = await _unitOfWork.OrderDetails.FindAsync(d => d.MenuItemId == id);
            if (used.Any())
            {
                // Soft-retire so historical orders stay intact.
                menuItem.IsAvailable = false;
                _unitOfWork.MenuItems.Update(menuItem);
                await _unitOfWork.SaveChangesAsync();
                return Ok(new { ok = true, retired = true, message = "Món đã từng bán — đã ẩn khỏi thực đơn." });
            }

            foreach (var recipe in menuItem.Recipes.ToList())
            {
                _unitOfWork.MenuItemRecipes.Remove(recipe);
            }
            _unitOfWork.MenuItems.Remove(menuItem);
            await _unitOfWork.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("inventory")]
        public async Task<IActionResult> Inventory([FromQuery] DateTime? beforeDate)
        {
            var items = await _inventoryService.GetItemsByExpirationAsync(beforeDate);
            return Ok(items.OrderBy(i => i.Name).Select(MapInventory));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("inventory")]
        public async Task<IActionResult> CreateInventory([FromBody] InventoryWriteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Unit))
            {
                return BadRequest(new { message = "Tên và đơn vị là bắt buộc." });
            }
            if (request.Quantity < 0 || request.MinimumThreshold < 0)
            {
                return BadRequest(new { message = "Số lượng / ngưỡng không hợp lệ." });
            }

            var item = new InventoryItem
            {
                Name = request.Name.Trim(),
                Quantity = request.Quantity,
                Unit = request.Unit.Trim(),
                MinimumThreshold = request.MinimumThreshold,
                ExpirationDate = request.ExpirationDate == default
                    ? DateTime.Today.AddDays(30)
                    : request.ExpirationDate.Date
            };
            await _unitOfWork.InventoryItems.AddAsync(item);
            await _unitOfWork.SaveChangesAsync();
            return Ok(new { id = item.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("inventory/{id:int}/stock")]
        public async Task<IActionResult> AdjustStock(int id, [FromBody] StockRequest request)
        {
            var item = await _inventoryService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound(new { message = "Không tìm thấy nguyên liệu." });
            }
            if (request.Quantity <= 0)
            {
                return BadRequest(new { message = "Số lượng phải lớn hơn 0." });
            }

            var action = (request.Action ?? "add").Trim().ToLowerInvariant();
            if (action == "deduct" && item.Quantity < request.Quantity)
            {
                return BadRequest(new { message = $"Không đủ hàng. Hiện có {item.Quantity} {item.Unit}." });
            }

            if (action == "add")
            {
                var expiry = request.ExpirationDate ?? item.ExpirationDate;
                await _inventoryService.AddStockAsync(id, request.Quantity, expiry);
            }
            else if (action == "deduct")
            {
                await _inventoryService.AddStockAsync(id, -request.Quantity, item.ExpirationDate);
            }
            else
            {
                return BadRequest(new { message = "Hành động không hợp lệ (add|deduct)." });
            }
            return Ok(new { ok = true });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("inventory/{id:int}")]
        public async Task<IActionResult> DeleteInventory(int id)
        {
            var item = await _inventoryService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound(new { message = "Không tìm thấy nguyên liệu." });
            }

            var recipes = await _unitOfWork.MenuItemRecipes.FindAsync(r => r.InventoryItemId == id);
            if (recipes.Any())
            {
                return BadRequest(new { message = "Nguyên liệu đang dùng trong công thức món — gỡ recipe trước khi xóa." });
            }

            await _inventoryService.DeleteAsync(id);
            return Ok(new { ok = true });
        }

        [Authorize(Roles = "Admin,Cashier")]
        [HttpGet("customers")]
        public async Task<IActionResult> Customers([FromQuery] string? search)
        {
            var customers = string.IsNullOrWhiteSpace(search)
                ? await _customerService.GetAllCustomersAsync()
                : await _customerService.SearchCustomersAsync(search);
            return Ok(customers.OrderByDescending(c => c.TotalSpent).Select(MapCustomer));
        }

        [Authorize(Roles = "Admin,Cashier")]
        [HttpGet("customers/{id:int}")]
        public async Task<IActionResult> Customer(int id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(id);
            return customer == null ? NotFound(new { message = "Không tìm thấy khách hàng." }) : Ok(MapCustomer(customer));
        }

        [Authorize(Roles = "Admin,Cashier")]
        [HttpPost("customers")]
        public async Task<IActionResult> CreateCustomer([FromBody] CustomerWriteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { message = "Tên và số điện thoại là bắt buộc." });
            }

            try
            {
                var created = await _customerService.CreateCustomerAsync(new Customer
                {
                    Name = request.Name.Trim(),
                    PhoneNumber = request.PhoneNumber.Trim(),
                    Email = request.Email?.Trim() ?? "",
                    Address = request.Address?.Trim() ?? "",
                    MembershipLevel = string.IsNullOrWhiteSpace(request.MembershipLevel) ? "Bronze" : request.MembershipLevel
                });
                return Ok(MapCustomer(created));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Admin,Cashier")]
        [HttpPut("customers/{id:int}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] CustomerWriteRequest request)
        {
            var existing = await _customerService.GetCustomerByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "Không tìm thấy khách hàng." });
            }
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                return BadRequest(new { message = "Tên và số điện thoại là bắt buộc." });
            }

            existing.Name = request.Name.Trim();
            existing.PhoneNumber = request.PhoneNumber.Trim();
            existing.Email = request.Email?.Trim() ?? "";
            existing.Address = request.Address?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(request.MembershipLevel))
            {
                existing.MembershipLevel = request.MembershipLevel;
            }
            await _customerService.UpdateCustomerAsync(existing);
            return Ok(MapCustomer(existing));
        }

        [Authorize(Roles = "Admin,Cashier")]
        [HttpDelete("customers/{id:int}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var existing = await _customerService.GetCustomerByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "Không tìm thấy khách hàng." });
            }
            await _customerService.DeleteCustomerAsync(id);
            return Ok(new { ok = true });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("users")]
        public async Task<IActionResult> Users()
        {
            var users = _userManager.Users.ToList();
            var result = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new { id = user.Id, fullName = user.FullName, email = user.Email, roles });
            }
            return Ok(new
            {
                users = result,
                roles = _roleManager.Roles.Select(r => r.Name).Where(n => n != null).OrderBy(n => n).ToList()
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email và mật khẩu là bắt buộc." });
            }

            var role = string.IsNullOrWhiteSpace(request.Role) ? "Waiter" : request.Role.Trim();
            if (!AllowedRoles.Contains(role))
            {
                return BadRequest(new { message = "Vai trò không hợp lệ." });
            }

            var user = new ApplicationUser
            {
                FullName = request.FullName?.Trim() ?? "",
                Email = request.Email.Trim(),
                UserName = request.Email.Trim(),
                Role = role,
                EmailConfirmed = true
            };
            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            }
            await _userManager.AddToRoleAsync(user, role);
            return Ok(new { id = user.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("users/{id}/role")]
        public async Task<IActionResult> AssignRole(string id, [FromBody] StatusRequest request)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "Không tìm thấy người dùng." });
            }

            var role = (request.Status ?? "").Trim();
            if (!AllowedRoles.Contains(role))
            {
                return BadRequest(new { message = "Vai trò không hợp lệ." });
            }
            if (!await _roleManager.RoleExistsAsync(role))
            {
                return BadRequest(new { message = $"Vai trò {role} chưa được khởi tạo." });
            }

            var current = await _userManager.GetRolesAsync(user);
            if (current.Count > 0)
            {
                await _userManager.RemoveFromRolesAsync(user, current);
            }
            await _userManager.AddToRoleAsync(user, role);
            user.Role = role;
            await _userManager.UpdateAsync(user);
            return Ok(new { ok = true, roles = new[] { role } });
        }

        [Authorize(Roles = "Cashier,Admin,Waiter")]
        [HttpGet("shift")]
        public async Task<IActionResult> GetShift()
        {
            var shift = await _shiftService.GetCurrentAsync();
            if (shift == null)
            {
                return Ok(new { open = false, shift = (object?)null });
            }

            return Ok(new { open = true, shift = await _shiftService.MapAsync(shift) });
        }

        [Authorize(Roles = "Cashier,Admin")]
        [HttpPost("shift/open")]
        public async Task<IActionResult> OpenShift([FromBody] ShiftOpenRequest? request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var shift = await _shiftService.OpenAsync(
                    user?.Email ?? user?.UserName ?? "staff",
                    request?.OpeningFloat ?? 0,
                    request?.Notes);
                return Ok(await _shiftService.MapAsync(shift));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Cashier,Admin")]
        [HttpPost("shift/close")]
        public async Task<IActionResult> CloseShift([FromBody] ShiftCloseRequest? request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var shift = await _shiftService.CloseAsync(
                    user?.Email ?? user?.UserName ?? "staff",
                    request?.Notes);
                return Ok(await _shiftService.MapAsync(shift));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Cashier,Admin,Waiter")]
        [HttpGet("payments/ready")]
        public async Task<IActionResult> ReadyPayments()
        {
            var orders = await _orderService.GetReadyOrdersAsync();
            return Ok(orders.Select(MapOrder));
        }

        [Authorize(Roles = "Cashier,Admin,Waiter")]
        [HttpPost("payments")]
        public async Task<IActionResult> Pay([FromBody] PayRequest request)
        {
            try
            {
                var method = NormalizePaymentMethod(request.PaymentMethod);
                Payment payment = method switch
                {
                    PaymentService.MethodCash => await _paymentService.ProcessCashPaymentAsync(
                        request.OrderId, request.CustomerInfo, request.CustomerPhone, request.CustomerId),
                    PaymentService.MethodCard => await _paymentService.ProcessCardPaymentAsync(
                        request.OrderId, request.CustomerInfo, request.CustomerPhone, request.CustomerId),
                    PaymentService.MethodMobileSim => await _paymentService.InitiateSimulatedPaymentAsync(
                        request.OrderId, request.CustomerInfo, request.CustomerPhone, request.CustomerId),
                    _ => throw new InvalidOperationException("Phương thức thanh toán không hợp lệ. Dùng Cash, Card hoặc MobileSim.")
                };

                return Ok(MapPaymentResult(payment, includeCheckout: method == PaymentService.MethodMobileSim));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Cashier,Admin,Waiter")]
        [HttpPost("payments/{id:int}/confirm")]
        public async Task<IActionResult> ConfirmPayment(int id, [FromBody] ConfirmRequest? request)
        {
            try
            {
                var success = request?.Success ?? true;
                var payment = await _paymentService.CompleteSimulatedPaymentAsync(id, success, "api-confirm");
                return Ok(MapPaymentResult(payment, includeCheckout: false));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Webhook-like callback for MobileSim (also used by /pay/sim page via service).</summary>
        [AllowAnonymous]
        [HttpPost("payments/{id:int}/simulate-callback")]
        public async Task<IActionResult> SimulateCallback(int id, [FromBody] SimulateCallbackRequest? request)
        {
            try
            {
                bool success;
                if (request?.Success != null)
                {
                    success = request.Success.Value;
                }
                else if (!string.IsNullOrWhiteSpace(request?.Result))
                {
                    if (string.Equals(request.Result, "Paid", StringComparison.OrdinalIgnoreCase))
                        success = true;
                    else if (string.Equals(request.Result, "Failed", StringComparison.OrdinalIgnoreCase))
                        success = false;
                    else
                        return BadRequest(new { message = "result phải là Paid hoặc Failed." });
                }
                else
                {
                    // Empty body → treat as successful webhook.
                    success = true;
                }

                var payment = await _paymentService.CompleteSimulatedPaymentAsync(id, success, "simulate-callback");
                return Ok(MapPaymentResult(payment, includeCheckout: false));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Roles = "Cashier,Admin,Waiter")]
        [HttpGet("payments/{id:int}")]
        public async Task<IActionResult> Payment(int id)
        {
            var payment = await _paymentService.GetPaymentWithDetailsAsync(id);
            if (payment == null)
            {
                return NotFound(new { message = "Không tìm thấy thanh toán." });
            }

            return Ok(MapPaymentReceipt(payment));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("reports/sales")]
        public async Task<IActionResult> Sales([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            var start = startDate?.Date;
            var endExclusive = endDate?.Date.AddDays(1);

            var orders = (await _unitOfWork.Orders.GetAllAsync()).AsEnumerable();
            var payments = (await _unitOfWork.Payments.GetAllAsync())
                .Where(p => IPaymentService.IsPaid(p.Status))
                .AsEnumerable();

            if (start.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt >= start.Value);
                payments = payments.Where(p => p.PaymentDate >= start.Value);
            }
            if (endExclusive.HasValue)
            {
                orders = orders.Where(o => o.CreatedAt < endExclusive.Value);
                payments = payments.Where(p => p.PaymentDate < endExclusive.Value);
            }

            var orderList = orders.Where(o => o.Status != "Cancelled").ToList();
            var paymentList = payments.ToList();

            return Ok(new
            {
                startDate = start,
                endDate = endDate?.Date,
                orderCount = orderList.Count,
                revenue = orderList.Sum(o => o.Total),
                paidAmount = paymentList.Sum(p => p.Amount),
                dailySales = orderList
                    .GroupBy(o => o.CreatedAt.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new { date = g.Key, total = g.Sum(o => o.Total), count = g.Count() }),
                paymentMethods = paymentList
                    .GroupBy(p => p.PaymentMethod)
                    .Select(g => new { method = g.Key, total = g.Sum(p => p.Amount), count = g.Count() }),
                orderTotal = orderList.Sum(o => o.Total),
                paymentTotal = paymentList.Sum(p => p.Amount)
            });
        }

        private async Task ReleaseTableIfIdleAsync(int tableId, int exceptOrderId)
        {
            if (tableId <= 0) return;
            var table = await _unitOfWork.Tables.GetByIdAsync(tableId);
            if (table == null) return;

            var others = await _unitOfWork.Orders.FindAsync(o =>
                o.TableId == tableId &&
                o.Id != exceptOrderId &&
                OrderService.IsOpenStatus(o.Status));

            if (!others.Any() && table.Status != "Empty")
            {
                table.Status = "Empty";
                _unitOfWork.Tables.Update(table);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        private async Task SaveRecipes(int menuItemId, List<RecipeWrite>? recipes)
        {
            if (recipes == null) return;

            foreach (var recipe in recipes.Where(r => r.InventoryItemId > 0 && r.QuantityRequired > 0))
            {
                var inventory = await _unitOfWork.InventoryItems.GetByIdAsync(recipe.InventoryItemId);
                if (inventory == null) continue;

                await _unitOfWork.MenuItemRecipes.AddAsync(new MenuItemRecipe
                {
                    MenuItemId = menuItemId,
                    InventoryItemId = recipe.InventoryItemId,
                    QuantityRequired = recipe.QuantityRequired
                });
            }
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<object> Profile(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return new
            {
                authenticated = true,
                name = string.IsNullOrWhiteSpace(user.FullName) ? user.Email : user.FullName,
                email = user.Email,
                roles
            };
        }

        private static string NormalizeRange(string? range)
        {
            var key = (range ?? "today").Trim().ToLowerInvariant();
            return key switch
            {
                "7d" or "7" or "week" => "week",
                "30d" or "30" or "month" => "month",
                "year" or "ytd" => "year",
                "today" or "day" => "today",
                _ => "today"
            };
        }

        private static object ChartPoint(string label, IEnumerable<Order> rows)
        {
            var list = rows.ToList();
            decimal Sum(params string[] statuses) =>
                list.Where(o => o.Status != null && statuses.Contains(o.Status)).Sum(o => o.Total);
            var opened = Sum("New");
            var making = Sum("Processing", "Ready", "PendingMobilePayment");
            var done = Sum("Completed");
            var total = list.Sum(o => o.Total);
            return new
            {
                label,
                revenue = total,
                opened,
                making,
                done,
                other = total - opened - making - done,
                count = list.Count
            };
        }

        private static object MapOrder(Order order)
        {
            var details = order.OrderDetails?.ToList() ?? new List<OrderDetail>();
            var elapsed = Math.Max(0, (int)(DateTime.Now - order.CreatedAt).TotalMinutes);
            return new
            {
                id = order.Id,
                tableId = order.TableId,
                tableName = order.Table?.Name,
                tableArea = order.Table?.Area,
                createdAt = order.CreatedAt,
                status = order.Status,
                total = order.Total,
                customerName = order.CustomerName,
                itemCount = details.Sum(d => d.Quantity),
                elapsedMinutes = elapsed,
                isKitchen = order.Status is "New" or "Processing",
                details = details.Select(d => new
                {
                    id = d.Id,
                    menuItemId = d.MenuItemId,
                    name = d.MenuItem?.Name,
                    quantity = d.Quantity,
                    price = d.Price,
                    lineTotal = d.Price * d.Quantity,
                    imageUrl = d.MenuItem?.ImageUrl,
                    category = d.MenuItem?.Category
                })
            };
        }

        private static object MapMenu(MenuItem item) => new
        {
            id = item.Id,
            name = item.Name,
            price = item.Price,
            description = item.Description,
            imageUrl = item.ImageUrl,
            category = item.Category,
            isAvailable = item.IsAvailable
        };

        private static object MapInventory(InventoryItem item) => new
        {
            id = item.Id,
            name = item.Name,
            quantity = item.Quantity,
            unit = item.Unit,
            minimumThreshold = item.MinimumThreshold,
            expirationDate = item.ExpirationDate
        };

        private static object MapCustomer(Customer customer) => new
        {
            id = customer.Id,
            name = customer.Name,
            phoneNumber = customer.PhoneNumber,
            email = customer.Email,
            address = customer.Address,
            loyaltyPoints = customer.LoyaltyPoints,
            membershipLevel = customer.MembershipLevel,
            joinDate = customer.JoinDate,
            totalSpent = customer.TotalSpent,
            totalOrders = customer.TotalOrders
        };

        private object MapPaymentResult(Payment payment, bool includeCheckout)
        {
            var status = IPaymentService.CanonicalStatus(payment.Status);
            var checkoutUrl = BuildSimCheckoutUrl(payment.Id);
            var payload = new Dictionary<string, object?>
            {
                ["id"] = payment.Id,
                ["orderId"] = payment.OrderId,
                ["amount"] = payment.Amount,
                ["status"] = status,
                ["paymentMethod"] = payment.PaymentMethod,
                ["method"] = payment.PaymentMethod,
                ["transactionId"] = payment.TransactionId,
                ["pointsEarned"] = payment.PointsEarned,
                // FE ReceiptPage still checks Mobile + !== Completed — expose aliases.
                ["legacyStatus"] = status switch
                {
                    "Paid" => "Completed",
                    "Pending" => "PendingMobile",
                    _ => status
                }
            };

            if (includeCheckout || status == "Pending")
            {
                payload["checkoutUrl"] = checkoutUrl;
                payload["qrPayload"] = $"anroastery://pay/sim/{payment.Id}?tx={payment.TransactionId}";
                payload["pollUrl"] = $"/api/payments/{payment.Id}";
            }

            return payload;
        }

        private object MapPaymentReceipt(Payment payment)
        {
            var status = IPaymentService.CanonicalStatus(payment.Status);
            var isPaid = status == "Paid";
            var checkoutUrl = BuildSimCheckoutUrl(payment.Id);

            return new
            {
                id = payment.Id,
                orderId = payment.OrderId,
                amount = payment.Amount,
                // Pending MobileSim → "Mobile" so React ReceiptPage still shows confirm buttons.
                // After Paid/Failed → "MobileSim" so those buttons hide.
                paymentMethod = payment.PaymentMethod == PaymentService.MethodMobileSim && status == "Pending"
                    ? "Mobile"
                    : payment.PaymentMethod,
                simMethod = payment.PaymentMethod,
                paymentDate = payment.PaymentDate,
                customerInfo = payment.CustomerInfo,
                status,
                // FE Receipt historically checked !== "Completed"
                legacyStatus = status switch
                {
                    "Paid" => "Completed",
                    "Pending" => "PendingMobile",
                    _ => status
                },
                isPaid,
                isPending = status == "Pending",
                isFailed = status == "Failed",
                transactionId = payment.TransactionId,
                notes = payment.Notes,
                pointsEarned = payment.PointsEarned,
                customerName = payment.Customer?.Name,
                tableName = payment.Order?.Table?.Name,
                orderStatus = payment.Order?.Status,
                orderTotal = payment.Order?.Total ?? payment.Amount,
                checkoutUrl = status == "Pending" ? checkoutUrl : null,
                qrPayload = status == "Pending" ? $"anroastery://pay/sim/{payment.Id}?tx={payment.TransactionId}" : null,
                receipt = isPaid
                    ? new
                    {
                        title = "An Roastery",
                        subtitle = "Phiếu thanh toán",
                        paidAt = payment.PaymentDate,
                        method = payment.PaymentMethod,
                        amount = payment.Amount,
                        transactionId = payment.TransactionId
                    }
                    : null,
                details = payment.Order?.OrderDetails?.Select(d => new
                {
                    name = d.MenuItem?.Name,
                    quantity = d.Quantity,
                    price = d.Price,
                    lineTotal = d.Price * d.Quantity
                })
            };
        }

        private string BuildSimCheckoutUrl(int paymentId)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/pay/sim/{paymentId}";
        }

        private static string NormalizePaymentMethod(string? method)
        {
            var key = (method ?? "").Trim();
            return key.ToLowerInvariant() switch
            {
                "cash" => PaymentService.MethodCash,
                "card" => PaymentService.MethodCard,
                "mobilesim" or "mobile" or "momo" or "vietqr" or "qr" => PaymentService.MethodMobileSim,
                _ => key
            };
        }

        private async Task BroadcastKitchenAsync(string eventName, Order order)
        {
            await _kitchenHub.Clients.Group(KitchenHub.GroupName).SendAsync(eventName, MapOrder(order));
        }
    }


    public class LoginRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool RememberMe { get; set; }
    }

    public class CreateOrderRequest
    {
        public int TableId { get; set; }
        public string? CustomerName { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class StatusRequest
    {
        public string Status { get; set; } = "";
    }

    public class MenuWriteRequest
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public bool IsAvailable { get; set; } = true;
        public List<RecipeWrite>? Recipes { get; set; }
    }

    public class RecipeWrite
    {
        public int InventoryItemId { get; set; }
        public decimal QuantityRequired { get; set; }
    }

    public class InventoryWriteRequest
    {
        public string Name { get; set; } = "";
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = "";
        public decimal MinimumThreshold { get; set; }
        public DateTime ExpirationDate { get; set; }
    }

    public class StockRequest
    {
        public decimal Quantity { get; set; }
        public string Action { get; set; } = "add";
        public DateTime? ExpirationDate { get; set; }
    }

    public class CreateUserRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
    }

    public class CustomerWriteRequest
    {
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? MembershipLevel { get; set; }
    }

    public class PayRequest
    {
        public int OrderId { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? CustomerInfo { get; set; }
        public string? CustomerPhone { get; set; }
        public int? CustomerId { get; set; }
    }

    public class ConfirmRequest
    {
        public bool Success { get; set; } = true;
    }

    public class SimulateCallbackRequest
    {
        public bool? Success { get; set; }
        /// <summary>Paid or Failed</summary>
        public string? Result { get; set; }
    }

    public class ShiftOpenRequest
    {
        public decimal OpeningFloat { get; set; }
        public string? Notes { get; set; }
    }

    public class ShiftCloseRequest
    {
        public string? Notes { get; set; }
    }
}

