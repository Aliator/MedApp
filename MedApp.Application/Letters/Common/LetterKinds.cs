using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Common;

/// <summary>
/// Letter kinds in words, as the PDF's title and messages name them (the light demo's <c>KIND_WORDS</c>): no
/// "discharge" in visible text, so a <see cref="LetterKind.DischargeSummary"/> is an end of care letter.
/// </summary>
public static class LetterKinds
{
    public static string Names => string.Join(", ", Enum.GetNames<LetterKind>());

    public static string Describe(LetterKind kind) => kind switch
    {
        LetterKind.ClinicLetter => "Clinic letter",
        LetterKind.DischargeSummary => "End of care letter",
        LetterKind.ReferralResponse => "Referral response",
        LetterKind.InformationRequest => "Request for more information",
        LetterKind.AppointmentLetter => "Appointment letter",
        LetterKind.OnwardReferral => "Onward referral",
        _ => "Letter",
    };
}
