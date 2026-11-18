using MedApp.Application.Common.Email;

namespace MedApp.Infrastructure.Common.Email;

/// <summary>
/// How many emails may go out in each clock hour (<see cref="EmailDeliveryOptions.MaxPerHour"/>), so a flood of reset
/// requests can't use up the mail provider's quota (Azure's managed domain allows 10 an hour). Two shares are kept that
/// links can't use:
/// <list type="bullet">
/// <item>for notices (<see cref="NoticeReserve"/>), so the notice that would reveal a takeover still goes out when
/// someone is asking for links or codes in bulk;</item>
/// <item>for two-factor codes (<see cref="CodeReserve"/>), so reset links, which anyone can ask for without a password,
/// can't stop people signing in with codes by email.</item>
/// </list>
/// Links may use what's left after both; codes may use their own share and anything links leave; notices anything. One
/// count of everything sent, so each share is "this much must still be free". Counted by this copy of the API.
/// </summary>
public sealed class EmailBudget(int maxPerHour, TimeProvider timeProvider)
{
    private readonly object _gate = new();
    private DateTimeOffset _hour;
    private int _sent;

    public int MaxPerHour { get; } = Math.Max(1, maxPerHour);

    /// <summary>Kept for notices: a fifth of the hour's allowance, at least one.</summary>
    public int NoticeReserve => Math.Max(1, MaxPerHour / 5);

    /// <summary>Kept for two-factor codes, beyond the notices': a fifth of the hour's allowance, at least one.</summary>
    public int CodeReserve => Math.Max(1, MaxPerHour / 5);

    /// <summary>Takes one email's place in this hour's allowance; false if there's none left for its category.</summary>
    public bool TryTake(EmailCategory category)
    {
        lock (_gate)
        {
            StartHour();
            if (_sent >= Limit(category))
                return false;

            _sent++;
            return true;
        }
    }

    /// <summary>Whether an email of <paramref name="category"/> would get a place now (a look: nothing is taken).</summary>
    public bool HasRoom(EmailCategory category)
    {
        lock (_gate)
        {
            StartHour();
            return _sent < Limit(category);
        }
    }

    /// <summary>How many may have been sent this hour, of every category, for one more of <paramref name="category"/> to go.</summary>
    private int Limit(EmailCategory category) => category switch
    {
        EmailCategory.Notice => MaxPerHour,
        EmailCategory.Code => MaxPerHour - NoticeReserve,
        _ => Math.Max(0, MaxPerHour - NoticeReserve - CodeReserve),
    };

    private void StartHour()
    {
        var now = timeProvider.GetUtcNow();
        var hour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        if (hour == _hour)
            return;

        _hour = hour;
        _sent = 0;
    }
}
