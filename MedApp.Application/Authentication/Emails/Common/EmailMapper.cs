using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Application.Authentication.Emails.Common;

/// <summary>An account's email addresses as the API returns them to their owner.</summary>
public static class EmailMapper
{
    public static EmailResponse ToResponse(AccountEmail account) =>
        new(account.Email, account.PendingEmail);
}
