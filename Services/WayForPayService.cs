using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DesignerStore.Services;

public class WayForPayService
{
    public const string PaymentUrl = "https://secure.wayforpay.com/pay";

    private readonly IConfiguration _config;
    private readonly ILogger<WayForPayService> _logger;

    public WayForPayService(IConfiguration config, ILogger<WayForPayService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string MerchantAccount    => _config["WayForPay:MerchantAccount"]    ?? "";
    public string MerchantSecretKey  => _config["WayForPay:MerchantSecretKey"]  ?? "";
    public string MerchantDomainName => _config["WayForPay:MerchantDomainName"] ?? "localhost";

    // HMAC-MD5 signature
    public string Sign(params string[] parts)
    {
        var message = string.Join(";", parts);
        _logger.LogInformation("🔐 WayForPay підпис рядок: {Message}", message);
        using var hmac = new HMACMD5(Encoding.UTF8.GetBytes(MerchantSecretKey));
        var result = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).ToLower();
        _logger.LogInformation("🔐 WayForPay підпис результат: {Sig}", result);
        return result;
    }

    // Verify return signature from WayForPay redirect
    public bool VerifyReturn(string merchantAccount, string orderReference,
        string amount, string currency, string authCode,
        string cardPan, string transactionStatus, string reasonCode, string signature)
    {
        var expected = Sign(merchantAccount, orderReference, amount, currency,
                            authCode, cardPan, transactionStatus, reasonCode);
        return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
    }

    // Форматує число без зайвих нулів: 1500.00 → "1500", 99.90 → "99.9"
    private static string Fmt(decimal v) =>
        v.ToString("0.##", CultureInfo.InvariantCulture);

    // Build list of form fields for the redirect page
    public List<(string Name, string Value)> BuildFormFields(
        DesignerStore.Models.Order order, string domain, List<DesignerStore.Models.CartItem> items)
    {
        var orderRef  = $"ELARIUM-{order.Id}";
        var orderDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var amount    = Fmt(order.TotalAmount);
        var currency  = "UAH";

        // Лапки і крапка з комою ламають підпис WayForPay — прибираємо
        var productNames  = items.Select(i => i.Name.Replace("\"", "").Replace("'", "").Replace(";", "")).ToList();
        var productPrices = items.Select(i => Fmt(i.Price)).ToList();
        var productCounts = items.Select(i => i.Quantity.ToString()).ToList();

        // Signature: merchantAccount;domain;orderRef;orderDate;amount;currency;names...;prices...;counts...
        var sigParts = new List<string>
            { MerchantAccount, MerchantDomainName, orderRef, orderDate, amount, currency };
        sigParts.AddRange(productNames);
        sigParts.AddRange(productPrices);
        sigParts.AddRange(productCounts);
        var signature = Sign(sigParts.ToArray());

        var fields = new List<(string, string)>
        {
            ("merchantAccount",              MerchantAccount),
            ("merchantAuthType",             "simpleSignature"),
            ("merchantDomainName",           MerchantDomainName),
            ("orderReference",               orderRef),
            ("orderDate",                    orderDate),
            ("amount",                       amount),
            ("currency",                     currency),
            ("orderLifetime",                "86400"),
            ("clientEmail",                  order.CustomerEmail),
            ("clientPhone",                  order.CustomerPhone),
            ("returnUrl",                    $"{domain}/Order/WayForPayReturn"),
            ("serviceUrl",                   $"{domain}/Order/WayForPayCallback"),
            ("defaultPaymentSystem",         "card"),
            ("merchantSignature",            signature),
        };

        // Array fields
        for (int i = 0; i < items.Count; i++)
        {
            fields.Add(("productName[]",  productNames[i]));
            fields.Add(("productPrice[]", productPrices[i]));
            fields.Add(("productCount[]", productCounts[i]));
        }

        return fields;
    }
}
