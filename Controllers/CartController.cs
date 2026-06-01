using DesignerStore.Data;
using DesignerStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DesignerStore.Controllers;

public class CartController : Controller
{
    private readonly ApplicationDbContext _context;

    public CartController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View(GetCart());
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(int productId, string size)
    {
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        var product = await _context.Products
            .Include(p => p.SizeStocks)
            .FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null)
            return isAjax ? Json(new { success = false, error = "Товар не знайдено" }) : NotFound();

        // Перевірка залишку по розміру
        var sizeStock = product.SizeStocks.FirstOrDefault(s => s.Size == size);
        if (sizeStock != null)
        {
            var cart   = GetCart();
            var inCart = cart.Where(c => c.ProductId == productId && c.Size == size).Sum(c => c.Quantity);

            if (sizeStock.Quantity == 0)
            {
                if (isAjax) return Json(new { success = false, error = $"Розмір {size} — розпродано" });
                TempData["CartError"] = $"Розмір {size} — SOLD OUT";
                return RedirectToAction("Details", "Home", new { id = productId });
            }
            if (inCart >= sizeStock.Quantity)
            {
                if (isAjax) return Json(new { success = false, error = $"Більше {sizeStock.Quantity} шт. розміру {size} немає в наявності" });
                TempData["CartError"] = $"Більше {sizeStock.Quantity} шт. розміру {size} немає в наявності";
                return RedirectToAction("Details", "Home", new { id = productId });
            }

            var existing = cart.FirstOrDefault(c => c.ProductId == productId && c.Size == size);
            if (existing != null) existing.Quantity++;
            else cart.Add(new CartItem
            {
                ProductId = product.Id,
                Name      = product.Name,
                Price     = product.Price,
                ImageUrl  = product.ImageUrl,
                Size      = size,
                Quantity  = 1
            });

            SaveCart(cart);

            if (isAjax)
                return Json(new { success = true, cartCount = cart.Sum(c => c.Quantity), productName = product.Name });
            return RedirectToAction("Index");
        }

        // Якщо SizeStocks не налаштовано — стара логіка
        {
            var cart     = GetCart();
            var existing = cart.FirstOrDefault(c => c.ProductId == productId && c.Size == size);
            if (existing != null) existing.Quantity++;
            else cart.Add(new CartItem
            {
                ProductId = product.Id,
                Name      = product.Name,
                Price     = product.Price,
                ImageUrl  = product.ImageUrl,
                Size      = size ?? "Один розмір",
                Quantity  = 1
            });
            SaveCart(cart);

            if (isAjax)
                return Json(new { success = true, cartCount = cart.Sum(c => c.Quantity), productName = product.Name });
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    public IActionResult RemoveItem(int productId, string size)
    {
        var cart = GetCart();
        cart.RemoveAll(c => c.ProductId == productId && c.Size == size);
        SaveCart(cart);
        return RedirectToAction("Index");
    }

    public IActionResult ClearCart()
    {
        HttpContext.Session.Remove("Cart");
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Count()
    {
        var cart = GetCart();
        return Json(new { count = cart.Sum(c => c.Quantity) });
    }

    private List<CartItem> GetCart()
    {
        var json = HttpContext.Session.GetString("Cart");
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<List<CartItem>>(json)!;
    }

    private void SaveCart(List<CartItem> cart) =>
        HttpContext.Session.SetString("Cart", JsonSerializer.Serialize(cart));
}
