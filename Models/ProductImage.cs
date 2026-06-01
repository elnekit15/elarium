namespace DesignerStore.Models;

public class ProductImage
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    
    // Зв'язок з товаром
    public int ProductId { get; set; }
    public Product? Product { get; set; }
}