using MedApp.Application.Common.Email;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// How many emails may go out in each clock hour (<see cref="EmailDeliveryOptions.MaxPerHour"/>), so a flood of reset
/// requests can't use up the mail provider's quota (Azure's managed domain allows 10 an hour). Links may use all but a
/// share kept for notices (<see cref="NoticeReserve"/>), so the notice that would reveal a takeover still goes out when
/// someone is asking for links in bulk. Counted by this copy of the API.
/// </summary>
public sealed class EmailBudget(int maxPerHour, TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private DateTimeOffset _hour;
    private int _sent;

    public int MaxPerHour { get; } = Math.Max(1, maxPerHour);

    /// <summary>Kept for notices: a fifth of the hour's allowance, at least one.</summary>
    public int NoticeReserve => Math.Max(1, MaxPerHour / 5);

    /// <summary>Takes one email's place in this hour's allowance; false if there's none left for its category.</summary>
    public bool TryTake(EmailCategory category)
    {
        var now = timeProvider.GetUtcNow();
        var hour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        lock (_gate)
        {
            if (hour != _hour)
            {
                _hour = hour;
                _sent = 0;
            }

            var limit = category == EmailCategory.Notice ? MaxPerHour : MaxPerHour - NoticeReserve;
            if (_sent >= limit)
                return false;

            _sent++;
            return true;
        }
    }
}
