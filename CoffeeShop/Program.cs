   using CoffeeShop.Data;
   using CoffeeShop.Data.UnitOfWork;
   using CoffeeShop.Models;
   using CoffeeShop.Services;
   using Microsoft.AspNetCore.Identity;
   using Microsoft.EntityFrameworkCore;

   var builder = WebApplication.CreateBuilder(args);

   // Add services to the container.
   builder.Services.AddControllersWithViews()
       .AddJsonOptions(options =>
       {
           options.JsonSerializerOptions.PropertyNamingPolicy = null; // Giữ nguyên tên thuộc tính
       });

   // Configure DbContext
   builder.Services.AddDbContext<ApplicationDbContext>(options =>
       options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

   // Configure Identity with ApplicationUser
   builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
   {
       options.Password.RequireDigit = true;
       options.Password.RequiredLength = 6;
       options.Password.RequireNonAlphanumeric = false;
       options.Password.RequireUppercase = false;
       options.Password.RequireLowercase = false;
   })
   .AddEntityFrameworkStores<ApplicationDbContext>()
   .AddDefaultTokenProviders();

   // Configure cookie authentication
   builder.Services.ConfigureApplicationCookie(options =>
   {
       options.LoginPath = "/Account/Login";
       options.LogoutPath = "/Account/Logout";
       options.AccessDeniedPath = "/Account/AccessDenied";
   });
builder.Services.AddControllersWithViews();

// Add application services
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
   builder.Services.AddScoped<IOrderService, OrderService>();
   builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
var app = builder.Build();

   // Configure the HTTP request pipeline.
   if (app.Environment.IsDevelopment())
   {
       app.UseDeveloperExceptionPage();
   }
   else
   {
       app.UseExceptionHandler("/Home/Error");
       app.UseHsts();
   }

   app.UseHttpsRedirection();
   app.UseStaticFiles();

   app.UseRouting();

   app.UseAuthentication();
   app.UseAuthorization();

   app.MapControllerRoute(
       name: "default",
       pattern: "{controller=Home}/{action=Index}/{id?}");

   // Seed data
   using (var scope = app.Services.CreateScope())
   {
       var services = scope.ServiceProvider;
       try
       {
           var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
           var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // Seed roles
        string[] roles = { "Admin", "Cashier", "Waiter" };
        foreach (var role in roles)
           {
               if (!await roleManager.RoleExistsAsync(role))
               {
                   await roleManager.CreateAsync(new IdentityRole(role));
               }
           }

           // Seed new admin user
           var newAdmin = await userManager.FindByEmailAsync("newadmin@coffee.com");
           if (newAdmin == null)
           {
               newAdmin = new ApplicationUser { UserName = "newadmin@coffee.com", Email = "newadmin@coffee.com" };
               var result = await userManager.CreateAsync(newAdmin, "NewAdmin@123");
               if (result.Succeeded)
               {
                   await userManager.AddToRoleAsync(newAdmin, "Admin");
               }
           }

           // Gọi SeedData nếu có
           await SeedData.Initialize(services);
       }
       catch (Exception ex)
       {
           var logger = services.GetRequiredService<ILogger<Program>>();
           logger.LogError(ex, "An error occurred seeding the DB.");
       }
   }

   app.Run();