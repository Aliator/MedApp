using MedApp.Domain.Practices;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// The referring practice as typed, or, when it's left empty and a practice is chosen from the directory, the
/// directory's: the letter's words stay as staff typed them, and the directory fills only what's missing.
/// </summary>
public static class ReferralPractices
{
    public static string TypedOrDirectoryName(string? typed, Practice? practice) =>
        string.IsNullOrWhiteSpace(typed) && practice is not null ? practice.Name : typed ?? string.Empty;

    public static string? TypedOrDirectoryOdsCode(string? typed, Practice? practice) =>
        string.IsNullOrWhiteSpace(typed) && practice is not null ? practice.OdsCode : typed;
}
