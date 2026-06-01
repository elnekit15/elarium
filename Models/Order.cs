using System.ComponentModel.DataAnnotations;

namespace DesignerStore.Models;

public class Order
{
    public int Id { get; set; }

    [MaxLength(100)] public string CustomerName  { get; set; } = string.Empty;
    [MaxLength(200)] public string CustomerEmail { get; set; } = string.Empty;
    [MaxLength(30)]  public string CustomerPhone { get; set; } = string.Empty;
    [MaxLength(100)] public string City          { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    public DateTime    CreatedAt     { get; set; } = DateTime.UtcNow;
    public OrderStatus Status        { get; set; } = OrderStatus.Pending;
    public decimal     TotalAmount   { get; set; }

    public string? StripeSessionId  { get; set; }
    public string? PaymentIntentId  { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}

public enum OrderStatus
{
    Pending    = 0,
    Paid       = 1,
    Processing = 2,
    Shipped    = 3,
    Delivered  = 4,
    Cancelled  = 5
}
