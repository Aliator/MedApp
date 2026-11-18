namespace MedApp.Application.Common.Email;

/// <summary>
/// An email address with most of its name hidden (<c>j•••@example.com</c>), to say where a code went without telling
/// someone who has only the password the whole address.
/// </summary>
public static class EmailAddressMask
{
    private const string Hidden = "\u2022\u2022\u2022";

    /// <summary>The first character of the name, three dots, and the domain; just the dots for anything without an <c>@</c>.</summary>
    public static string Mask(string address)
    {
        var at = address.LastIndexOf('@');
        if (at <= 0)
            return Hidden;

        // The first character as a whole text element, so a surrogate pair isn't split.
        var first = System.Globalization.StringInfo.GetNextTextElementLength(address);
        return string.Concat(address.AsSpan(0, Math.Min(first, at)), Hidden, address.AsSpan(at));
    }
}
