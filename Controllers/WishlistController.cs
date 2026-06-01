using DesignerStore.Data;
using DesignerStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DesignerStore.Controllers;

public class WishlistController : Controller
{
    private readonly ApplicationDbContext _context;

    public WishlistController(ApplicationDbContext context) => _context = context;

    // Сторінка обраного 
    public async Task<IActionResult> Index()
    {
        var ids      = GetIds();
        var products = ids.Count == 0
            ? new List<Product>()
            : await _context.Products
                .Include(p => p.Images)
                .Include(p => p.SizeStocks)
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();

        ViewBag.WishlistIds = ids.ToHashSet();
        return View(products);
    }

    // додати / прибрати 
    [HttpPost]
    public IActionResult Toggle(int productId)
    {
        var ids   = GetIds();
        bool added;

        if (ids.Contains(productId))
        {
            ids.Remove(productId);
            added = false;
        }
        else
        {
            ids.Add(productId);
            added = true;
        }

        SaveIds(ids);
        return Json(new { added, count = ids.Count });
    }

    // helpers
    private List<int> GetIds()
    {
        var json = HttpContext.Session.GetString("Wishlist");
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<List<int>>(json)!;
    }

    private void SaveIds(List<int> ids) =>
        HttpContext.Session.SetString("Wishlist", JsonSerializer.Serialize(ids));
}
