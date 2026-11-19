namespace MedApp.Domain.Letters;

/// <summary>
/// What a letter (and the template it's written from) is for. Stored by number, so never renumber it
/// (docs/centre-phases-3-5-plan.md, section 5).
/// </summary>
public enum LetterKind
{
    ClinicLetter = 0,
    DischargeSummary = 1,
    ReferralResponse = 2,
    InformationRequest = 3,
    AppointmentLetter = 4,
    Other = 5,

    // 6 is the lab's report letter (MC20).

    /// <summary>A referral on to the hospital (MC28): what a stage needing a referral letter to hospital waits for.</summary>
    OnwardReferral = 7
}
