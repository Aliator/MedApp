using System.Text;

namespace MedApp.Application.Common.Email;

/// <summary>
/// An email to one person: plain HTML and the same words as text. Made by <see cref="EmailTemplates"/>, which never
/// puts clinical information or a password in one.
/// </summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody)
{
    // Never prints the address or the body (which may hold a link with a token), so logging a message can't leak them.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Subject)} = {Subject}");
        return true;
    }
}
