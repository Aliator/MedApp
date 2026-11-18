using MailKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// Sends the queued emails (<see cref="EmailQueue"/>) one at a time, in the background, notices before links, within the
/// hourly limit (<see cref="EmailBudget"/>): one over it is dropped and logged. A failure is logged at Warning with the
/// email's category, the exception's type and the SMTP status code only: a mail server's message often quotes the
/// address. Nothing is retried: every email MedApp sends is a notice or a link that can be asked for again.
/// </summary>
public sealed class EmailDispatcher(
    EmailQueue queue,
    IEmailTransport transport,
    ILogger<EmailDispatcher> logger)
    : BackgroundService
{
    private readonly EmailBudget _budget = queue.Budget;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (queue.TryTakeNext() is not { } message)
                {
                    await queue.WaitAsync(stoppingToken);
                    continue;
                }

                var category = message.Category;
                if (!_budget.TryTake(category))
                {
                    logger.LogWarning(
                        "Email dropped ({EmailCategory}): this hour's {MaxPerHour} have been sent.", category, _budget.MaxPerHour);
                    continue;
                }

                try
                {
                    await transport.SendAsync(message, stoppingToken);
                    logger.LogInformation("Email sent ({EmailCategory}).", category);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "Email not sent ({EmailCategory}): {ExceptionType}, SMTP status {SmtpStatus}.",
                        category, exception.GetType().Name, SmtpStatus(exception));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: anything still queued is lost, as documented.
        }
    }

    /// <summary>The SMTP status code a refusal carried, or "none": never the server's message, which may quote the address.</summary>
    public static string SmtpStatus(Exception exception) => exception switch
    {
        SmtpCommandException command => ((int)command.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture),
        SmtpProtocolException => "protocol",
        ServiceNotAuthenticatedException => "not-authenticated",
        _ => "none",
    };
}
