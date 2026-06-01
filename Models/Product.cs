using System.ComponentModel.DataAnnotations;

namespace DesignerStore.Models;

public class Product
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Назва товару обов'язкова")]
    [MaxLength(100)]
    [NoHtml]
    public string Name { get; set; } = string.Empty;

    [NoHtml]
    public string? Description { get; set; }

    [Required]
    public decimal Price { get; set; }

    public string? ImageUrl { get; set; } = string.Empty;
    public List<ProductImage> Images { get; set; } = new();
    public bool IsAvailable { get; set; } = true; 
    public string? Sizes    { get; set; } = "S, M, L, XL";
    [NoHtml]
    public string? Category { get; set; }
    [NoHtml]
    public string? Gender   { get; set; }
    public int     Stock    { get; set; } = 0;  // загальний залишок (legacy / для зворотної сумісності)

    public List<ProductSizeStock> SizeStocks { get; set; } = new();
}