using MedApp.Domain.Referrals;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The free text staff type on referrals in the story: the reason for referral (from the letter), the referrer, and
/// the details given with a triage decision, a withdrawal or a pause. All invented: the practices aren't real ones
/// (none is in the NHS Organisation Data Service's directory), the referring clinicians are made-up names, and no ODS
/// codes are given. Nothing here decides anything clinical: it's what a member of staff "typed".
/// </summary>
public static class ReferralTexts
{
    /// <summary>Invented GP practices, none of them a real organisation's name.</summary>
    public static readonly string[] Practices =
    [
        "Brackenmoor Road Surgery",
        "Kestrel Lane Medical Practice",
        "Fallowfield Green Health Centre",
        "Wrenhurst Family Practice",
        "Pellowick Lane Surgery",
        "Thornyvale Medical Centre",
        "Quillbrook Surgery",
        "Brindlecote Health Centre",
    ];

    /// <summary>Invented referring GPs; about one referral in five names nobody.</summary>
    private static readonly string[] Clinicians =
    [
        "Dr A. Pemberley", "Dr S. Okonjo", "Dr L. Fairweather", "Dr R. Castellan", "Dr H. Moorcroft", "Dr J. Varga",
        "Dr P. Lindqvist", "Dr N. Ashworth-Bell", "Dr E. Trelawney", "Dr K. Obi-Marsh",
    ];

    /// <summary>Why someone is referred to the new service, which has no pathway kinds of its own yet.</summary>
    private static readonly string[] RespiratoryIndications =
    [
        "breathlessness on exertion for three months",
        "a cough that has lasted eight weeks",
        "an assessment for home oxygen asked for by the GP",
        "wheeze that hasn't settled with inhalers",
    ];

    private static readonly string[] Asks =
    [
        "Please see and advise.",
        "The GP asks for assessment and management.",
        "Recent blood results are with the letter.",
        "Seen twice in the practice; no better.",
        "The patient is keen to be seen.",
        "Medication list attached to the letter.",
    ];

    /// <summary>A practice for one referral.</summary>
    public static string Practice(Random random) => Pick(random, Practices);

    /// <summary>The referring clinician, or none (the letter didn't say).</summary>
    public static string? Clinician(Random random) => random.NextDouble() < 0.2 ? null : Pick(random, Clinicians);

    /// <summary>
    /// The reason for referral as typed from the letter: why (one of the kind's indications), and what the GP asks.
    /// </summary>
    public static string Reason(ProcedureKind? kind, Random random)
    {
        var why = kind is null ? Pick(random, RespiratoryIndications) : Pick(random, kind.Indications);
        return $"Referred with {why}. {Pick(random, Asks)}";
    }

    /// <summary>The detail with a rejection: required for Other, given about half the time otherwise.</summary>
    public static string? RejectionDetail(ReferralRejectionReason reason, Random random) => reason switch
    {
        ReferralRejectionReason.NotOfferedHere => random.NextDouble() < 0.5 ? null : Pick(random,
            "We don't offer this here; the GP has been told who does.",
            "Needs the hospital's service: not something the centre treats."),
        ReferralRejectionReason.DoesNotMeetCriteria => random.NextDouble() < 0.5 ? null : Pick(random,
            "Can be managed in primary care: advice sent to the GP.",
            "Doesn't meet the service's referral criteria; advice and guidance given to the GP.",
            "Symptoms described are mild; the GP can treat first and refer again if no better."),
        ReferralRejectionReason.InsufficientInformation => Pick(random,
            "The letter doesn't say how long it has been there or whether it has changed.",
            "No recent blood results or medication list: the GP is asked for them.",
            "Photographs mentioned in the letter weren't sent."),
        ReferralRejectionReason.Duplicate => "Already referred: the earlier referral is being seen.",
        _ => Pick(random,
            "The patient has moved out of the area.",
            "The patient is already under the hospital team for this."),
    };

    /// <summary>Where a referral redirected elsewhere went (another provider), and why.</summary>
    public static (string Elsewhere, string Detail) Elsewhere(string specialty, Random random) => specialty switch
    {
        DemoSpecialties.Dermatology => Pick(random,
            ("The hospital's urgent skin cancer clinic", "Features need the hospital's two-week clinic."),
            ("The hospital's paediatric dermatology clinic", "Children under five are seen at the hospital.")),
        DemoSpecialties.Gynaecology => ("The hospital's gynaecology clinic", "Needs the hospital's hysteroscopy service."),
        DemoSpecialties.Urology => ("The hospital's urology team", "Visible blood in the urine: the hospital's haematuria clinic."),
        DemoSpecialties.Diabetes => ("The hospital's foot protection service", "An active foot ulcer needs the hospital's team."),
        _ => ("Community podiatry (another provider)", "Nail care and corns are the community podiatry team's."),
    };

    /// <summary>Why a referral was moved to another specialty here, by the triager who read it.</summary>
    public static string RedirectDetail(string from, string to) => (from, to) switch
    {
        (DemoSpecialties.Urology, DemoSpecialties.Gynaecology) =>
            "The bleeding described is vaginal, not in the urine: post-menopausal bleeding, for gynaecology.",
        (DemoSpecialties.Diabetes, DemoSpecialties.Biomechanics) =>
            "Foot pain on walking with healthy skin and good feeling: biomechanical, for the podiatrist.",
        (DemoSpecialties.Gynaecology, DemoSpecialties.Dermatology) =>
            "A skin lesion on the thigh, not a gynaecological problem: for the skin clinic.",
        _ => $"Better seen in {to}.",
    };

    public static string WithdrawalDetail(Random random) => Pick(random,
        "The GP withdrew the referral: the symptoms have settled.",
        "The patient asked the GP to withdraw it; they'll be seen privately.",
        "The patient doesn't want to be seen; the GP has been told.");

    public const string EnteredInErrorDetail =
        "Registered on the wrong patient (the same surname); registered again on the right one.";

    /// <summary>The detail with a pause: required for Other, often given otherwise.</summary>
    public static string? PauseDetail(ClockPauseReason reason, Random random) => reason switch
    {
        ClockPauseReason.PatientChoseToWait => random.NextDouble() < 0.4 ? null : Pick(random,
            "Wants to wait until after a family wedding.",
            "Asked to wait until after the school holidays.",
            "Chose to wait for an appointment with the consultant rather than the next clinic."),
        ClockPauseReason.PatientUnavailable => Pick(random,
            "Abroad for six weeks; back at the end of next month.",
            "In hospital for another reason; the ward will let us know.",
            "Recovering from a hip operation; can't travel yet."),
        _ => Pick(random,
            "Caring for a relative full time until the end of the month.",
            "Asked to wait while they arrange transport."),
    };

    private static T Pick<T>(Random random, params T[] items) => items[random.Next(items.Length)];
}
