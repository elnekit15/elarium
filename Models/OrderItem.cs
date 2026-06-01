namespace DesignerStore.Models;

public class OrderItem
{
    public int    Id          { get; set; }
    public int    OrderId     { get; set; }
    public Order  Order       { get; set; } = null!;

    public int     ProductId   { get; set; }
    public string  ProductName { get; set; } = string.Empty;
    public string? ImageUrl    { get; set; }
    public string  Size        { get; set; } = string.Empty;
    public decimal Price       { get; set; }
    public int     Quantity    { get; set; }
}
