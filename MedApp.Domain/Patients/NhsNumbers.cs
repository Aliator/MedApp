namespace MedApp.Domain.Patients;

/// <summary>
/// The NHS number: ten digits, the last a Modulus 11 check digit (NHS Data Dictionary, "NHS Number"). MedApp stores it
/// as the ten digits alone, and shows it as <c>XXX XXX XXXX</c>.
/// <para>
/// The check: multiply each of the first nine digits by its weight (10 for the first, down to 2 for the ninth), add
/// them, and take the remainder after dividing by 11. Eleven minus the remainder is the check digit, except that 11
/// becomes 0, and 10 means the number isn't valid (no NHS number is issued with it).
/// </para>
/// Numbers starting 999 are never issued to real people, and are kept for testing; MedApp's tests and demo data use
/// only those.
/// </summary>
public static class NhsNumbers
{
    public const int Length = 10;

    /// <summary>
    /// The number as typed, without its spaces (people write it <c>XXX XXX XXXX</c>), or null when nothing was typed.
    /// Doesn't check it: see <see cref="IsValid"/>.
    /// </summary>
    public static string? Normalise(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        return string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
    }

    /// <summary>Whether the text, spaces left out, is ten digits whose last is the right check digit.</summary>
    public static bool IsValid(string? input)
    {
        var digits = Normalise(input);
        if (digits is null || digits.Length != Length || !digits.All(char.IsAsciiDigit))
            return false;

        return CheckDigit(digits) is { } check && check == digits[9] - '0';
    }

    /// <summary>Whether the text, spaces left out, is ten digits (whatever the check digit).</summary>
    public static bool IsTenDigits(string? input) =>
        Normalise(input) is { Length: Length } digits && digits.All(char.IsAsciiDigit);

    /// <summary>
    /// The check digit the first nine digits call for, or null when they call for 10 (no valid number starts with
    /// them). Needs at least nine ASCII digits.
    /// </summary>
    public static int? CheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (digits[i] - '0') * (10 - i);

        var check = 11 - sum % 11;
        return check switch
        {
            11 => 0,
            10 => null,
            _ => check
        };
    }

    /// <summary>
    /// The number as people read it, <c>XXX XXX XXXX</c>. Anything that isn't ten digits once its spaces are gone is
    /// returned as it was.
    /// </summary>
    public static string Format(string number)
    {
        var digits = Normalise(number);
        return digits is { Length: Length } && digits.All(char.IsAsciiDigit)
            ? $"{digits[..3]} {digits[3..6]} {digits[6..]}"
            : number;
    }
}
