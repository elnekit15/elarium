using DesignerStore.Data;
using DesignerStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;

namespace DesignerStore.Controllers;

[Authorize(Roles = "Admin")]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Products.Include(p => p.SizeStocks).ToListAsync());
    }

    // ─── Create ──────────────────────────────────────────────────

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Product product,
        IFormFile? imageFile,
        List<IFormFile>? uploadImages,
        List<string>? SizesInput,
        List<int>? QuantitiesInput)
    {
        // Validate main image before ModelState check so error shows in form
        if (imageFile != null && imageFile.Length > 0)
        {
            var imgError = await ValidateImageFileAsync(imageFile);
            if (imgError != null) ModelState.AddModelError("imageFile", imgError);
        }

        if (ModelState.IsValid)
        {
            // Головне фото
            if (imageFile != null && imageFile.Length > 0)
                product.ImageUrl = await SaveImageAsync(imageFile);

            // Оновлюємо рядок Sizes (для сумісності з фільтром)
            product.Sizes = BuildSizesString(SizesInput);

            _context.Add(product);
            await _context.SaveChangesAsync();

            // Зберігаємо SizeStocks
            await SaveSizeStocksAsync(product.Id, SizesInput, QuantitiesInput);

            // Додаткові фото галереї
            if (uploadImages?.Count > 0)
                await SaveGalleryImagesAsync(product.Id, uploadImages);

            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    // ─── Edit ────────────────────────────────────────────────────

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.SizeStocks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null) return NotFound();
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        Product product,
        IFormFile? imageFile,
        List<IFormFile>? uploadImages,
        List<string>? SizesInput,
        List<int>? QuantitiesInput)
    {
        if (id != product.Id) return NotFound();

        // Validate main image before ModelState check
        if (imageFile != null && imageFile.Length > 0)
        {
            var imgError = await ValidateImageFileAsync(imageFile);
            if (imgError != null) ModelState.AddModelError("imageFile", imgError);
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Зберігаємо старе фото якщо нове не завантажено
                if (imageFile != null && imageFile.Length > 0)
                    product.ImageUrl = await SaveImageAsync(imageFile);

                // Оновлюємо рядок Sizes (для сумісності з фільтром)
                product.Sizes = BuildSizesString(SizesInput);

                _context.Update(product);
                await _context.SaveChangesAsync();

                // Оновлюємо SizeStocks (видаляємо старі, записуємо нові)
                var existing = await _context.ProductSizeStocks
                    .Where(s => s.ProductId == product.Id)
                    .ToListAsync();
                _context.ProductSizeStocks.RemoveRange(existing);
                await _context.SaveChangesAsync();

                await SaveSizeStocksAsync(product.Id, SizesInput, QuantitiesInput);

                // Автоматично оновлюємо IsAvailable якщо є розміри
                if (SizesInput?.Any(s => !string.IsNullOrWhiteSpace(s)) == true)
                {
                    var totalStock = QuantitiesInput?.Sum() ?? 0;
                    var p = await _context.Products.FindAsync(product.Id);
                    if (p != null)
                    {
                        p.Stock = totalStock;
                        if (totalStock > 0) p.IsAvailable = true;
                        await _context.SaveChangesAsync();
                    }
                }

                // Додаткові фото галереї
                if (uploadImages?.Count > 0)
                    await SaveGalleryImagesAsync(product.Id, uploadImages);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        product = await _context.Products
            .Include(p => p.Images)
            .Include(p => p.SizeStocks)
            .FirstOrDefaultAsync(p => p.Id == id) ?? product;

        return View(product);
    }

    // ─── Delete ──────────────────────────────────────────────────

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var product = await _context.Products.FirstOrDefaultAsync(m => m.Id == id);
        if (product == null) return NotFound();
        return View(product);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null) _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        // Прибрати товар зі списку обраного (session), щоб бейдж не лишався
        var wishJson = HttpContext.Session.GetString("Wishlist");
        if (!string.IsNullOrEmpty(wishJson))
        {
            var ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(wishJson)!;
            if (ids.Remove(id))
                HttpContext.Session.SetString("Wishlist",
                    System.Text.Json.JsonSerializer.Serialize(ids));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteImage(int imageId, int productId)
    {
        var image = await _context.ProductImages.FindAsync(imageId);
        if (image != null)
        {
            var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, image.Url.TrimStart('/'));
            if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);

            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Edit), new { id = productId });
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private bool ProductExists(int id) => _context.Products.Any(e => e.Id == id);

    // Allowed image extensions and their magic-byte signatures
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    private static readonly (string ext, byte[] magic)[] MagicBytes =
    {
        (".jpg",  new byte[] { 0xFF, 0xD8, 0xFF }),
        (".jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        (".png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        (".gif",  new byte[] { 0x47, 0x49, 0x46 }),
        (".webp", new byte[] { 0x52, 0x49, 0x46, 0x46 }),   // "RIFF"; bytes 8-11 = "WEBP"
    };

    /// <summary>
    /// Returns null if valid; otherwise returns a human-readable error message.
    /// </summary>
    private static async Task<string?> ValidateImageFileAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(ext))
            return $"Недозволений формат «{ext}». Дозволено: {string.Join(", ", AllowedExtensions)}.";

        if (file.Length > 10 * 1024 * 1024)   // 10 MB cap
            return "Файл занадто великий (максимум 10 МБ).";

        // Read first 12 bytes for magic-byte check
        var header = new byte[12];
        using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

        var signature = MagicBytes.FirstOrDefault(m => m.ext == ext);
        if (signature != default)
        {
            var magic = signature.magic;
            bool matches = read >= magic.Length &&
                           header.Take(magic.Length).SequenceEqual(magic);

            // Extra check for WebP: bytes 8..11 must be "WEBP"
            if (ext == ".webp" && matches)
                matches = read >= 12 &&
                          header[8] == 'W' && header[9] == 'E' &&
                          header[10] == 'B' && header[11] == 'P';

            if (!matches)
                return "Вміст файлу не відповідає вказаному розширенню.";
        }

        return null;   // valid
    }

    private async Task<string> SaveImageAsync(IFormFile file)
    {
        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var ext        = Path.GetExtension(file.FileName).ToLowerInvariant();
        string safeName  = Path.GetFileName(file.FileName);        // strip any path traversal
        string uniqueName = $"{Guid.NewGuid()}_{safeName}";
        string fullPath   = Path.Combine(uploadsFolder, uniqueName);

        using var src  = file.OpenReadStream();
        using var dest = new FileStream(fullPath, FileMode.Create);
        await src.CopyToAsync(dest);

        return "/images/products/" + uniqueName;
    }

    private async Task SaveGalleryImagesAsync(int productId, List<IFormFile> files)
    {
        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        foreach (var file in files)
        {
            if (file.Length <= 0) continue;

            var error = await ValidateImageFileAsync(file);
            if (error != null) continue;   // skip invalid files silently in gallery batch

            string safeName   = Path.GetFileName(file.FileName);
            string uniqueName = $"{Guid.NewGuid()}_{safeName}";
            using var src  = file.OpenReadStream();
            using var dest = new FileStream(Path.Combine(uploadsFolder, uniqueName), FileMode.Create);
            await src.CopyToAsync(dest);

            _context.ProductImages.Add(new ProductImage
            {
                ProductId = productId,
                Url       = "/images/products/" + uniqueName
            });
        }
        await _context.SaveChangesAsync();
    }

    private async Task SaveSizeStocksAsync(int productId, List<string>? sizes, List<int>? quantities)
    {
        if (sizes == null) return;
        for (int i = 0; i < sizes.Count; i++)
        {
            var size = sizes[i]?.Trim();
            if (string.IsNullOrEmpty(size)) continue;
            _context.ProductSizeStocks.Add(new ProductSizeStock
            {
                ProductId = productId,
                Size      = size,
                Quantity  = (quantities != null && i < quantities.Count) ? Math.Max(0, quantities[i]) : 0
            });
        }
        await _context.SaveChangesAsync();
    }

    private static string BuildSizesString(List<string>? sizes) =>
        sizes == null ? string.Empty
        : string.Join(",", sizes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
}
