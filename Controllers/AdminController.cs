using DesignerStore.Data;
using DesignerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DesignerStore.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    private static readonly string[] UkrMonths =
    {
        "Січ", "Лют", "Бер", "Кві", "Тра", "Чер",
        "Лип", "Сер", "Вер", "Жов", "Лис", "Гру"
    };

    private static readonly OrderStatus[] PaidStatuses =
    {
        OrderStatus.Paid, OrderStatus.Processing,
        OrderStatus.Shipped, OrderStatus.Delivered
    };

    public AdminController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Dashboard()
    {
        var now = DateTime.UtcNow;

        // Load all orders + items in one query
        var allOrders = await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync();

        var paidOrders = allOrders.Where(o => PaidStatuses.Contains(o.Status)).ToList();

        // KPIs 
        var totalRevenue    = paidOrders.Sum(o => o.TotalAmount);
        var paidCount       = paidOrders.Count;
        var avgOrderValue   = paidCount > 0 ? Math.Round(paidOrders.Average(o => o.TotalAmount), 0) : 0m;
        var uniqueCustomers = allOrders
            .Where(o => !string.IsNullOrEmpty(o.CustomerEmail))
            .Select(o => o.CustomerEmail.ToLower())
            .Distinct()
            .Count();
        var totalProducts   = await _context.Products.CountAsync();

        // Revenue: поточний рік (Січ–Гру) 
        var revenueByMonth = new List<MonthlyRevenueDto>();
        for (int month = 1; month <= 12; month++)
        {
            var slice = paidOrders
                .Where(o => o.CreatedAt.Year == now.Year && o.CreatedAt.Month == month)
                .ToList();
            revenueByMonth.Add(new MonthlyRevenueDto
            {
                Label  = UkrMonths[month - 1],
                Amount = slice.Sum(o => o.TotalAmount),
                Count  = slice.Count
            });
        }

        // Revenue: останні 12 місяців  
        var revenueByMonthRolling = new List<MonthlyRevenueDto>();
        for (int i = 11; i >= 0; i--)
        {
            var m = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var slice = paidOrders
                .Where(o => o.CreatedAt.Year == m.Year && o.CreatedAt.Month == m.Month)
                .ToList();
            revenueByMonthRolling.Add(new MonthlyRevenueDto
            {
                Label  = $"{UkrMonths[m.Month - 1]} {m.Year}",
                Amount = slice.Sum(o => o.TotalAmount),
                Count  = slice.Count
            });
        }

        // Top products by quantity sold 
        var topProducts = paidOrders
            .SelectMany(o => o.Items)
            .GroupBy(i => new { i.ProductId, i.ProductName, i.ImageUrl })
            .Select(g => new TopProductDto
            {
                ProductId    = g.Key.ProductId,
                Name         = g.Key.ProductName,
                ImageUrl     = g.Key.ImageUrl,
                QuantitySold = g.Sum(i => i.Quantity),
                Revenue      = g.Sum(i => i.Price * i.Quantity)
            })
            .OrderByDescending(p => p.QuantitySold)
            .Take(6)
            .ToList();

        // Status breakdown 
        var statusBreakdown = allOrders
            .GroupBy(o => o.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        // Recent orders 
        var recentOrders = allOrders
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToList();

        var vm = new DashboardViewModel
        {
            TotalRevenue    = totalRevenue,
            TotalOrders     = allOrders.Count,
            PaidOrders      = paidCount,
            AvgOrderValue   = avgOrderValue,
            UniqueCustomers = uniqueCustomers,
            TotalProducts   = totalProducts,
            RevenueByMonth        = revenueByMonth,
            RevenueByMonthRolling = revenueByMonthRolling,
            TopProducts     = topProducts,
            StatusBreakdown = statusBreakdown,
            RecentOrders    = recentOrders
        };

        return View(vm);
    }
}
