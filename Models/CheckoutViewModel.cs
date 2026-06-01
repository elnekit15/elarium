using System.ComponentModel.DataAnnotations;

namespace DesignerStore.Models;

public class CheckoutViewModel
{
    [Required(ErrorMessage = "Введіть ПІБ")]
    [MaxLength(100)]
    [NoHtml]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть email")]
    [EmailAddress(ErrorMessage = "Невірний формат email")]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть телефон")]
    [MaxLength(30)]
    [NoHtml]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть місто")]
    [MaxLength(100)]
    [NoHtml]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введіть адресу або відділення Нової Пошти")]
    [NoHtml]
    public string Address { get; set; } = string.Empty;

    // "stripe" | "wayforpay"
    public string PaymentMethod { get; set; } = "stripe";
}
