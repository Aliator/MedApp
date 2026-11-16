using MedApp.Application.Common.Email;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>Hands one message to the mail server. Throws if it can't; <see cref="EmailDispatcher"/> logs it.</summary>
public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
