namespace MedApp.Application.Authentication.TwoFactor.Repositories;

/// <summary>
/// How long wrong two-factor codes lock an account: the usual lockout the first time, then twice as long each time
/// after, up to a day, until the account next signs in completely. At five codes a lockout, guessing a six-digit code
/// (three valid at any moment) then takes years rather than months.
/// </summary>
public static class CodeLockouts
{
    /// <summary>The longest a lockout from codes lasts.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromDays(1);

    /// <param name="baseLockout">The usual lockout (Identity's <c>Lockout:DefaultLockoutTimeSpan</c>).</param>
    /// <param name="previousLockouts">Lockouts from codes since the last complete sign-in.</param>
    public static TimeSpan Duration(TimeSpan baseLockout, int previousLockouts)
    {
        var doublings = Math.Clamp(previousLockouts, 0, 20);
        var ticks = baseLockout.Ticks * (1L << doublings);
        return ticks <= 0 || ticks >= Longest.Ticks ? Longest : TimeSpan.FromTicks(ticks);
    }
}
