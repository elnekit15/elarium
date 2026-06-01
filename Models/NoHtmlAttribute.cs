using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace DesignerStore.Models;

/// <summary>
/// Rejects any value that contains HTML tags or common injection patterns
/// (script injection, event handlers, dangerous protocols).
/// Razor encodes output automatically, but this keeps stored data clean.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NoHtmlAttribute : ValidationAttribute
{
    // Matches any HTML-like tag: <div>, </p>, <br/>, <!-- -->, <!DOCTYPE …>
    private static readonly Regex TagPattern =
        new(@"<[a-zA-Z/!][^>]*>", RegexOptions.Compiled);

    // Dangerous inline patterns: javascript:, on*= event handlers, data: URI
    private static readonly Regex DangerPattern =
        new(@"(javascript\s*:|on\w+\s*=|data\s*:)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public NoHtmlAttribute()
        : base("Поле не може містити HTML-теги або скрипти") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return ValidationResult.Success;

        if (TagPattern.IsMatch(s) || DangerPattern.IsMatch(s))
            return new ValidationResult(ErrorMessage);

        return ValidationResult.Success;
    }
}
