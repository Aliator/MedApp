using System.Text;

namespace MedApp.Application.Common.Email;

/// <summary>
/// An email to one person: plain HTML and the same words as text. Made by <see cref="EmailTemplates"/>, which never
/// puts clinical information, a password, an authenticator key or recovery codes in one (the only secret any email holds
/// is a short-lived, single-use two-factor code: <see cref="EmailCategory.Code"/>).
/// </summary>
/// <param name="Category">
/// Whether it's a notice of a change to the account, a two-factor code, or a link someone asked for: notices go first,
/// and keep a share of the hourly limit links and codes can't use; codes go before links. The only thing about a message
/// that's logged.
/// </param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    EmailCategory Category = EmailCategory.Link)
{
    // Never prints the address, the subject or the body (which may hold a link with a token).
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Category)} = {Category}");
        return true;
    }
}

/// <summary>What kind of email a message is, for ordering, the hourly limit and logs.</summary>
public enum EmailCategory
{
    /// <summary>A link someone asked for (to confirm an address, or to choose a new password).</summary>
    Link,

    /// <summary>A notice of a change to the account, which may be the only sign of a takeover: sent first.</summary>
    Notice,

    /// <summary>
    /// A two-factor code someone is waiting for to sign in: sent before links (with notices), but within the share of the
    /// hourly limit links have, so notices keep theirs.
    /// </summary>
    Code,
}
