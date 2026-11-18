using MedApp.Domain.Letters;

namespace MedApp.Application.Letters.Common;

/// <summary>Letter kinds in words, as the PDF's title and messages name them.</summary>
public static class LetterKinds
{
    public static string Names => string.Join(", ", Enum.GetNames<LetterKind>());

    public static string Describe(LetterKind kind) => kind switch
    {
        LetterKind.ClinicLetter => "Clinic letter",
        LetterKind.DischargeSummary => "Discharge summary",
        LetterKind.ReferralResponse => "Referral response",
        LetterKind.InformationRequest => "Request for more information",
        LetterKind.AppointmentLetter => "Appointment letter",
        _ => "Letter",
    };
}
