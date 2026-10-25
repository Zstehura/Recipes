using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;

namespace RecipesApp.Shared;

/// <summary>
/// A number box for whole numbers that must be at least 1 (cooking time, servings). A plain
/// InputNumber shows a new recipe's 0, which has to be deleted before typing. Here 0 is shown
/// as an empty box, and an empty box means 0, so the model's "at least 1" rule reports it.
/// Because 0 is never valid, nothing a user could enter is lost by showing it as empty.
/// </summary>
public class InputPositiveNumber : InputNumber<int>
{
    protected override string? FormatValueAsString(int value) =>
        value == 0 ? string.Empty : base.FormatValueAsString(value);

    protected override bool TryParseValueFromString(
        string? value, out int result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = 0;
            validationErrorMessage = null;
            return true;
        }
        return base.TryParseValueFromString(value, out result, out validationErrorMessage);
    }
}
