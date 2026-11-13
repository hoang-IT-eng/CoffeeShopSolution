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
       if (builder.Environment.IsDevelopment())
       {
           options.Cookie.SameSite = SameSiteMode.Lax;
       }
       else
       {
           options.Cookie.SameSite = SameSiteMode.None;
           options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
       }
       options.Events.OnRedirectToLogin = context =>
       {
           if (context.Request.Path.StartsWithSegments("/api"))
           {
               context.Response.StatusCode = StatusCodes.Status401Unauthorized;
               return Task.CompletedTask;
           }
           context.Response.Redirect(context.RedirectUri);
           return Task.CompletedTask;
       };
       options.Events.OnRedirectToAccessDenied = context =>
       {
           if (context.Request.Path.StartsWithSegments("/api"))
           {
               context.Response.StatusCode = StatusCodes.Status403Forbidden;
               return Task.CompletedTask;
           }
           context.Response.Redirect(context.RedirectUri);
           return Task.CompletedTask;
       };
   });

   var frontendOrigins = (builder.Configuration["Frontend:Origins"] ?? "http://localhost:5173")
       .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("Frontend", policy =>
           policy.WithOrigins(frontendOrigins)
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials());
   });

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
       app.UseExceptionHandler(errorApp =>
       {
           errorApp.Run(async context =>
           {
               context.Response.StatusCode = StatusCodes.Status500InternalServerError;
               context.Response.ContentType = "application/json";
               await context.Response.WriteAsync("{\"message\":\"Có lỗi trên máy chủ.\"}");
           });
       });
       app.UseHsts();
   }

   app.UseHttpsRedirection();
   app.UseStaticFiles();

   app.UseRouting();

   app.UseCors("Frontend");
   app.UseAuthentication();
   app.UseAuthorization();

   app.MapControllers();

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
               newAdmin = new ApplicationUser
               {
                   UserName = "newadmin@coffee.com",
                   Email = "newadmin@coffee.com",
                   FullName = "Admin",
                   Role = "Admin",
                   EmailConfirmed = true
               };
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