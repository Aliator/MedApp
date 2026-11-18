using System.Text.RegularExpressions;
using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Sites;

namespace MedApp.Application.Sites.Common;

/// <summary>What sites and rooms must be, and what can change about them.</summary>
public static partial class SiteRules
{
    public const string LastOpenSite =
        "MedApp needs at least one site that isn't archived. Add or restore another site first.";

    public static string NameTaken(string name) => $"A site called '{name}' already exists.";

    public static string RoomNameTaken(string siteName, string name) => $"{siteName} already has a room called '{name}'.";

    public static string SiteArchived(Site site) => $"{site.Name} is archived. Restore it first.";

    public static string HasOpenRooms(Site site, IEnumerable<Room> rooms) =>
        $"{site.Name} has rooms that aren't archived ({string.Join(", ", rooms.Select(r => r.Name))}). Archive them first.";

    public static string SiteInUse(Site site) =>
        $"{site.Name} has rooms, closed days or staff working hours, so it can't be deleted. Archive it instead.";

    public static string HasStaffHours(Site site) =>
        $"Staff work at {site.Name} (their working hours name it). Change their hours first.";

    public static string StaffOutsideHours(Site site, string username, DayOfWeek day) =>
        $"{username} works at {site.Name} on {day}s outside those hours. Change their hours first.";

    public static string RoomInUse(Room room) =>
        $"{room.Name} has been used, so it can't be deleted. Archive it instead.";

    /// <summary>The address's lines as stored: trimmed, blank ones left out, joined by line breaks; null for none.</summary>
    public static string? JoinAddress(IEnumerable<string?>? lines)
    {
        var kept = (lines ?? []).Select(l => l?.Trim() ?? string.Empty).Where(l => l.Length > 0).ToList();
        return kept.Count == 0 ? null : string.Join('\n', kept);
    }

    /// <summary>The stored address's lines (none for none).</summary>
    public static List<string> SplitAddress(string? address) =>
        string.IsNullOrEmpty(address) ? [] : [.. address.Split('\n')];

    /// <summary>Text trimmed, or null when it's empty.</summary>
    public static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>A postcode as stored (<see cref="UkPostcodes.Normalise"/>), or null for none.</summary>
    public static string? Postcode(string? text) => Optional(text) is { } postcode ? UkPostcodes.Normalise(postcode) : null;

    /// <summary>Whether address lines are at most three non-blank lines of 100 characters, none holding a line break.</summary>
    public static bool AreAddressLines(IReadOnlyList<string?>? lines) =>
        lines is null
        || (lines.Count(l => !string.IsNullOrWhiteSpace(l)) <= SiteLimits.MaxAddressLines
            && lines.All(l => l is null || (l.Trim().Length <= SiteLimits.MaxAddressLineLength && !l.Any(char.IsControl))));

    /// <summary>Whether a postcode is empty, or a UK postcode in format.</summary>
    public static bool IsPostcode(string? postcode) => string.IsNullOrWhiteSpace(postcode) || UkPostcodes.IsValid(postcode);

    /// <summary>Whether a phone number is empty, or at most 30 characters of digits, spaces and + ( ) -, with 6 digits or more.</summary>
    public static bool IsPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone)
        || (phone.Trim().Length <= SiteLimits.MaxPhoneLength
            && PhonePattern().IsMatch(phone.Trim())
            && phone.Count(char.IsAsciiDigit) >= 6);

    public const string AddressLinesMessage =
        "An address has at most 3 lines of 100 characters each, and no line breaks within a line.";

    public const string PostcodeMessage = "Postcode must be a UK postcode, such as ZZ99 3AA.";

    public const string PhoneMessage = "Phone must be a phone number of at most 30 characters: digits, spaces and + ( ) -.";

    /// <summary>The check on a site's or room's name, shared by adding and editing one.</summary>
    public static IRuleBuilderOptions<T, string> Name<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule
            .Must(n => !string.IsNullOrWhiteSpace(n))
            .WithMessage("Name is required.")
            .MaximumLength(maxLength)
            .WithMessage($"Name must not exceed {maxLength} characters.");

    /// <summary>A 409 if this is the last site that isn't archived (archiving or deleting it would leave none).</summary>
    public static void EnsureNotLastOpenSite(Site site, int openSites)
    {
        if (!site.IsArchived && openSites <= 1)
            throw new ConflictException(LastOpenSite);
    }

    [GeneratedRegex(@"^[0-9+()\- ]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
}
