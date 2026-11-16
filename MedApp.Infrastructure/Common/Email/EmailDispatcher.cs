using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// Sends the queued emails (<see cref="EmailQueue"/>) one at a time, in the background. A failure is logged at Warning,
/// with the subject and the error but never the address, and the message is not retried: every email MedApp sends is a
/// notice or a link that can be asked for again.
/// </summary>
public sealed class EmailDispatcher(
    EmailQueue queue,
    IEmailTransport transport,
    ILogger<EmailDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await transport.SendAsync(message, stoppingToken);
                    logger.LogInformation("Email sent: {Subject}", message.Subject);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Email not sent: {Subject}", message.Subject);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: anything still queued is lost, as documented.
        }
    }
}
