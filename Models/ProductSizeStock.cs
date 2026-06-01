namespace DesignerStore.Models;

public class ProductSizeStock
{
    public int     Id        { get; set; }
    public int     ProductId { get; set; }
    public Product Product   { get; set; } = null!;
    public string  Size      { get; set; } = string.Empty;
    public int     Quantity  { get; set; } = 0;
}
