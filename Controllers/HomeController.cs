using DesignerStore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DesignerStore.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Landing()
    {
        var recent = await _context.Products
            .Include(p => p.Images)
            .Where(p => p.IsAvailable)
            .OrderByDescending(p => p.Id)
            .Take(8)
            .ToListAsync();
        return View(recent);
    }

    public async Task<IActionResult> Index(
        string? category,
        string? searchString,
        string? sort,
        decimal? priceMin,
        decimal? priceMax,
        string? stockFilter,
        string? size,
        string? gender)
    {
        var products = _context.Products
                       .Include(p => p.Images)
                       .AsQueryable();

        if (stockFilter == "unavailable")
            products = products.Where(p => !p.IsAvailable);
        else if (stockFilter == "all")
            { }
        else
            products = products.Where(p => p.IsAvailable);

        if (!string.IsNullOrEmpty(category))
        {
            // Групові фільтри — охоплюють кілька категорій одразу
            if (category == "Денім")
            {
                var denimCats = new[] { "Джинси", "Денім" };
                products = products.Where(p => p.Category != null && denimCats.Contains(p.Category));
            }
            else if (category == "Верхній одяг")
            {
                var outerCats = new[] { "Куртки", "Пальто", "Верхній одяг" };
                products = products.Where(p => p.Category != null && outerCats.Contains(p.Category));
            }
            else
            {
                products = products.Where(p => p.Category == category);
            }

            ViewData["CurrentCategory"] = category;
        }

        if (!string.IsNullOrEmpty(searchString))
        {
            var q = searchString.ToLower();
            products = products.Where(p =>
                p.Name.ToLower().Contains(q) ||
                (p.Description != null && p.Description.ToLower().Contains(q)));
        }

        if (priceMin.HasValue)
            products = products.Where(p => p.Price >= priceMin.Value);
        if (priceMax.HasValue)
            products = products.Where(p => p.Price <= priceMax.Value);

        if (!string.IsNullOrEmpty(size))
            products = products.Where(p => p.SizeStocks.Any(s => s.Size == size && s.Quantity > 0));

        if (!string.IsNullOrEmpty(gender))
            products = products.Where(p => p.Gender == gender);

        products = sort switch
        {
            "price_asc"  => products.OrderBy(p => p.Price),
            "price_desc" => products.OrderByDescending(p => p.Price),
            "date_asc"   => products.OrderBy(p => p.Id),
            _            => products.OrderByDescending(p => p.Id),
        };

        ViewBag.CurrentSort    = sort;
        ViewBag.CurrentPriceMin = priceMin;
        ViewBag.CurrentPriceMax = priceMax;
        ViewBag.CurrentStock   = stockFilter;
        ViewBag.CurrentSize    = size;
        ViewBag.CurrentGender  = gender;
        ViewBag.CurrentSearch  = searchString;
        ViewBag.FilterActive   = sort != null || priceMin.HasValue || priceMax.HasValue
                               || !string.IsNullOrEmpty(stockFilter) || !string.IsNullOrEmpty(size)
                               || !string.IsNullOrEmpty(gender);
        ViewBag.WishlistIds    = GetWishlistIds();

        return View(await products.ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.SizeStocks)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (product == null) return NotFound();

        ViewBag.WishlistIds = GetWishlistIds();
        return View(product);
    }

    [Route("Home/AppStatusCode")]
    public IActionResult AppStatusCode(int code = 404)
    {
        Response.StatusCode = code;
        ViewData["StatusCode"] = code;
        return View("NotFound");
    }

    private HashSet<int> GetWishlistIds()
    {
        var json = HttpContext.Session.GetString("Wishlist");
        if (string.IsNullOrEmpty(json)) return new();
        return JsonSerializer.Deserialize<List<int>>(json)!.ToHashSet();
    }
}
