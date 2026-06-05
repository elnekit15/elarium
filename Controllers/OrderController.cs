using DesignerStore.Data;
using DesignerStore.Models;
using DesignerStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
using System.Security.Claims;
using System.Text.Json;

namespace DesignerStore.Controllers;

public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration       _config;
    private readonly WayForPayService     _wfp;
    private readonly ILogger<OrderController> _logger;

    public OrderController(ApplicationDbContext context, IConfiguration config,
                           WayForPayService wfp, ILogger<OrderController> logger)
    {
        _context = context;
        _config  = config;
        _wfp     = wfp;
        _logger  = logger;
    }

    public IActionResult Checkout()
    {
        var cart = GetCart();
        if (!cart.Any()) return RedirectToAction("Index", "Cart");
        ViewBag.Cart = cart;
        return View(new CheckoutViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var cart = GetCart();
        if (!cart.Any()) return RedirectToAction("Index", "Cart");

        if (!ModelState.IsValid)
        {
            ViewBag.Cart = cart;
            return View(model);
        }

        var order = new Order
        {
            CustomerName  = model.Name,
            CustomerEmail = model.Email,
            CustomerPhone = model.Phone,
            City          = model.City,
            Address       = model.Address,
            TotalAmount   = cart.Sum(i => i.Price * i.Quantity),
            Status        = OrderStatus.Pending,
            Items         = cart.Select(c => new OrderItem
            {
                ProductId   = c.ProductId,
                ProductName = c.Name,
                ImageUrl    = c.ImageUrl,
                Size        = c.Size,
                Price       = c.Price,
                Quantity    = c.Quantity
            }).ToList()
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var domain = $"{Request.Scheme}://{Request.Host}";

        var lineItems = cart.Select(item => new SessionLineItemOptions
        {
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency   = "uah",
                UnitAmount = (long)(item.Price * 100),
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = $"{item.Name} / {item.Size}"
                }
            },
            Quantity = item.Quantity
        }).ToList();

        // ── WayForPay ──────────────────────────────────────────────
        if (model.PaymentMethod == "wayforpay")
        {
            var fields = _wfp.BuildFormFields(order, domain, cart);
            return View("WayForPayForm", fields);
        }

        // ── Stripe (default) ───────────────────────────────────────
        var sessionOptions = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems          = lineItems,
            Mode               = "payment",
            CustomerEmail      = model.Email,
            SuccessUrl         = $"{domain}/Order/Success?session_id={{CHECKOUT_SESSION_ID}}&order_id={order.Id}",
            CancelUrl          = $"{domain}/Cart",
            Metadata           = new Dictionary<string, string> { { "order_id", order.Id.ToString() } }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(sessionOptions);

        order.StripeSessionId = session.Id;
        await _context.SaveChangesAsync();

        return Redirect(session.Url);
    }

    // ─── WayForPay return (redirect after payment) ────────────────

    [HttpGet]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> WayForPayReturn()
    {
        // Діагностика — логуємо все що прийшло від WayForPay
        if (Request.HasFormContentType)
            _logger.LogInformation("↩️ WFP Return FORM: {Keys}",
                string.Join(" | ", Request.Form.Select(kv => $"{kv.Key}={kv.Value}")));
        if (Request.Query.Any())
            _logger.LogInformation("↩️ WFP Return QUERY: {Q}", Request.QueryString.Value);

        // Гнучке читання параметрів — WayForPay шле POST-форму, але fallback на query
        string Param(string key) =>
            (Request.HasFormContentType && Request.Form.ContainsKey(key)) ? Request.Form[key].ToString()
            : Request.Query.ContainsKey(key)                              ? Request.Query[key].ToString()
            : "";

        var orderReference    = Param("orderReference");
        var transactionStatus = Param("transactionStatus");
        var authCode          = Param("authCode");

        if (string.IsNullOrEmpty(orderReference)
            || !orderReference.StartsWith("ELARIUM-")
            || !int.TryParse(orderReference["ELARIUM-".Length..], out var orderId))
        {
            _logger.LogWarning("↩️ WFP Return: невалідний orderReference='{Ref}'", orderReference);
            return RedirectToAction("Index", "Cart");
        }

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null) return NotFound();

        // Оновлюємо БД (сток + статус) — працює незалежно від cookie/сесії
        if (transactionStatus != "Declined" && transactionStatus != "Expired"
            && transactionStatus != "Refunded")
        {
            await MarkOrderPaidAsync(order, authCode);
        }

        // Redirect на first-party GET — браузер піде з cookie користувача,
        // тоді зможемо почистити кошик з його сесії
        return RedirectToAction("WayForPaySuccess", new { id = order.Id });
    }

    // First-party GET після оплати WayForPay — тут є сесія користувача
    [HttpGet]
    public async Task<IActionResult> WayForPaySuccess(int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Тепер ми у first-party GET — cookie сесії доступний, чистимо кошик
        HttpContext.Session.Remove("Cart");

        return View("Success", order);
    }

    // WayForPay server-to-server callback (service URL)
    // Має повернути JSON-підтвердження з підписом, інакше WayForPay ретраїть запит
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> WayForPayCallback()
    {
        string orderReference     = "";
        string transactionStatus  = "";
        string authCode           = "";
        try
        {
            if (Request.HasFormContentType)
            {
                orderReference    = Request.Form["orderReference"].ToString();
                transactionStatus = Request.Form["transactionStatus"].ToString();
                authCode          = Request.Form["authCode"].ToString();
            }
            else
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("orderReference", out var or))    orderReference    = or.GetString() ?? "";
                    if (root.TryGetProperty("transactionStatus", out var ts)) transactionStatus = ts.GetString() ?? "";
                    if (root.TryGetProperty("authCode", out var ac))          authCode          = ac.ToString();
                }
            }
        }
        catch { /* ігноруємо помилки парсингу — все одно відповідаємо accept */ }

        _logger.LogInformation("📥 WayForPayCallback: orderRef={Ref}, status={Status}",
            orderReference, transactionStatus);

        // Оновлюємо сток/статус (ідемпотентно) — найнадійніше місце, server-to-server
        if (transactionStatus == "Approved"
            && orderReference.StartsWith("ELARIUM-")
            && int.TryParse(orderReference["ELARIUM-".Length..], out var orderId))
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order != null) await MarkOrderPaidAsync(order, authCode);
        }

        var time      = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var status    = "accept";
        var signature = _wfp.Sign(orderReference, status, time.ToString());

        return Json(new { orderReference, status, time, signature });
    }

    // Ідемпотентно: позначає замовлення оплаченим і зменшує сток лише раз
    private async Task MarkOrderPaidAsync(Order order, string? authCode)
    {
        if (order.Status != OrderStatus.Pending) return;   // вже оброблено — не дублюємо

        order.Status          = OrderStatus.Paid;
        order.PaymentIntentId = authCode;
        await ReduceStockAsync(order);

        // Якщо всі розміри товару розпродані → IsAvailable = false
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        foreach (var pid in productIds)
        {
            var productStocks = await _context.ProductSizeStocks
                .Where(s => s.ProductId == pid)
                .ToListAsync();
            if (productStocks.Any() && productStocks.All(s => s.Quantity == 0))
            {
                var product = await _context.Products.FindAsync(pid);
                if (product != null) product.IsAvailable = false;
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("✅ Замовлення {OrderId} позначено оплаченим, сток оновлено", order.Id);
    }

    public async Task<IActionResult> Success(string session_id, int order_id)
    {
        if (string.IsNullOrEmpty(session_id))
            return RedirectToAction("Index", "Cart");

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == order_id && o.StripeSessionId == session_id);

        if (order == null) return NotFound();

        if (order.Status == OrderStatus.Pending)
        {
            var service = new SessionService();
            var session = await service.GetAsync(session_id);

            if (session.PaymentStatus == "paid")
            {
                order.Status          = OrderStatus.Paid;
                order.PaymentIntentId = session.PaymentIntentId;

                // Зменшення залишку по конкретному розміру
                foreach (var item in order.Items)
                {
                    var sizeStock = await _context.ProductSizeStocks
                        .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.Size == item.Size);

                    if (sizeStock != null)
                    {
                        sizeStock.Quantity = Math.Max(0, sizeStock.Quantity - item.Quantity);
                    }
                    else
                    {
                        // Fallback: старий механізм без per-size стоку
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                            product.Stock = Math.Max(0, product.Stock - item.Quantity);
                    }
                }

                // Перевіряємо чи всі розміри товару розпродані → IsAvailable = false
                var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
                foreach (var pid in productIds)
                {
                    var productStocks = await _context.ProductSizeStocks
                        .Where(s => s.ProductId == pid)
                        .ToListAsync();

                    if (productStocks.Any() && productStocks.All(s => s.Quantity == 0))
                    {
                        var product = await _context.Products.FindAsync(pid);
                        if (product != null) product.IsAvailable = false;
                    }
                }

                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("Cart");
            }
            else
            {
                TempData["Error"] = "Оплату не підтверджено. Спробуйте ще раз.";
                return RedirectToAction("Index", "Cart");
            }
        }

        return View(order);
    }

    // ─── My Orders (для авторизованих) ───────────────────────────

    [Authorize]
    public async Task<IActionResult> MyOrders()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
                    ?? User.FindFirstValue(ClaimTypes.Name)
                    ?? User.Identity?.Name;

        if (string.IsNullOrEmpty(email))
            return RedirectToAction("Track");

        var orders = await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerEmail.ToLower() == email.ToLower())
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    public IActionResult Track() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track(int orderId, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError("", "Введіть email");
            return View();
        }

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId &&
                o.CustomerEmail.ToLower() == email.Trim().ToLower());

        if (order == null)
        {
            ModelState.AddModelError("", "Замовлення не знайдено. Перевірте номер та email.");
            return View();
        }

        return View("TrackResult", order);
    }

    private List<CartItem> GetCart()
    {
        var json = HttpContext.Session.GetString("Cart");
        return string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<List<CartItem>>(json)!;
    }

    // Зменшення залишків по розміру після оплати
    private async Task ReduceStockAsync(Order order)
    {
        foreach (var item in order.Items)
        {
            var sizeStock = await _context.ProductSizeStocks
                .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.Size == item.Size);

            if (sizeStock != null)
            {
                sizeStock.Quantity = Math.Max(0, sizeStock.Quantity - item.Quantity);
            }
            else
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                    product.Stock = Math.Max(0, product.Stock - item.Quantity);
            }
        }

        // Якщо всі розміри = 0 → товар недоступний
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        foreach (var pid in productIds)
        {
            var productStocks = await _context.ProductSizeStocks
                .Where(s => s.ProductId == pid)
                .ToListAsync();

            if (productStocks.Any() && productStocks.All(s => s.Quantity == 0))
            {
                var product = await _context.Products.FindAsync(pid);
                if (product != null) product.IsAvailable = false;
            }
        }
    }
}
