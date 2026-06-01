using DesignerStore.Data;
using DesignerStore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<DesignerStore.Services.WayForPayService>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// В розробці сайт працює по HTTP → SameAsRequest; на продакшені (HTTPS) → Always
var cookieSecure = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout          = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly      = true;
    options.Cookie.IsEssential   = true;
    options.Cookie.SecurePolicy  = cookieSecure;
    options.Cookie.SameSite      = SameSiteMode.Lax;   // Lax — сумісний з OAuth-редиректами
    options.Cookie.Name          = ".ELARIUM.Session";
});
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddTransient<IEmailSender<IdentityUser>, EmailSender>();

// Захист auth-cookie (Identity)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly     = true;
    options.Cookie.SecurePolicy = cookieSecure;
    options.Cookie.SameSite     = SameSiteMode.Lax;
    options.Cookie.Name         = ".ELARIUM.Auth";
    options.ExpireTimeSpan      = TimeSpan.FromDays(14);
    options.SlidingExpiration   = true;
});

var googleId     = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId     = googleId;
            options.ClientSecret = googleSecret;
            options.CallbackPath = "/signin-google"; 
        });
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers 
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";           // не вгадувати MIME-тип файлу
    h["X-Frame-Options"]        = "SAMEORIGIN";        // захист від clickjacking
    h["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    h["Permissions-Policy"]     = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseStaticFiles();
app.UseStatusCodePagesWithReExecute("/Home/AppStatusCode", "?code={0}");

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // ── Автоміграція (для Railway та будь-якого деплою) ───────────────
    var db = services.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // ── Seed ролей та admin-акаунту ───────────────────────────────────
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    if (await userManager.FindByEmailAsync("admin@store.com") == null)
    {
        var admin = new IdentityUser
        {
            UserName = "admin@store.com",
            Email = "admin@store.com",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, "Admin_12345!");
        await userManager.AddToRoleAsync(admin, "Admin");
    }
}

// ── Railway: слухаємо на PORT якщо задано ────────────────────────────
var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(railwayPort))
    app.Urls.Add($"http://0.0.0.0:{railwayPort}");

app.Run();
