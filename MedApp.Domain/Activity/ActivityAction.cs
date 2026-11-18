namespace MedApp.Domain.Activity;

/// <summary>What an activity log entry records. Stored as a number, so values never change.</summary>
public enum ActivityAction
{
    /// <summary>Someone opened a patient's record.</summary>
    PatientViewed = 0,

    /// <summary>A patient was in the results of someone's patient search.</summary>
    PatientListed = 1,

    /// <summary>Someone searched the patients: which filters they used and how many matched, never the terms.</summary>
    PatientsSearched = 2,

    PatientCreated = 3,

    /// <summary>A patient's details were edited: which fields, with no values.</summary>
    PatientUpdated = 4,

    PatientDeleted = 5,

    /// <summary>An admin exported everything held about a patient (a subject access export).</summary>
    PatientExported = 6,

    /// <summary>An admin replaced a patient's identifying details and free text with placeholders.</summary>
    PatientAnonymised = 7,

    PatientRestricted = 8,
    PatientRestrictionLifted = 9,
    PatientDisputed = 10,
    PatientDisputeCleared = 11,

    /// <summary>An admin decided to keep a record past its retention period, with a reason and a date to review it again.</summary>
    RetentionKept = 12,

    /// <summary>Someone opened a pathway's page.</summary>
    PathwayViewed = 13,

    SignedIn = 14,

    /// <summary>A sign-in was refused: a wrong username or password, or a locked account.</summary>
    SignInFailed = 15,

    SignedOut = 16,
    AccountCreated = 17,
    AccountDeleted = 18,

    /// <summary>An admin gave an account a new generated password.</summary>
    PasswordReset = 19,

    /// <summary>Someone changed their own password.</summary>
    PasswordChanged = 20,

    /// <summary>An account's roles were changed (from and to).</summary>
    RolesChanged = 21,

    /// <summary>An admin made a role: its name and the permissions it grants.</summary>
    RoleCreated = 22,

    /// <summary>An admin deleted a role nobody held: its name and the permissions it granted.</summary>
    RoleDeleted = 23,

    /// <summary>An admin ended every session an account had ("Sign out everywhere").</summary>
    SignedOutEverywhere = 24,

    /// <summary>
    /// Someone looked a person up by NHS number in the demographics service (PDS): whether anyone was found, never the
    /// number.
    /// </summary>
    DemographicsLookedUp = 25,

    /// <summary>Someone listed the files attached to a patient, a pathway or a stage (with the pathway, if any).</summary>
    AttachmentsViewed = 26,

    /// <summary>Someone downloaded a file attached to a patient's record (with the pathway, if any).</summary>
    AttachmentDownloaded = 27,

    /// <summary>
    /// A right password for an account with two-factor authentication: a code was asked for. The second step records
    /// <see cref="SignedIn"/>, or <see cref="SignInFailed"/> for a wrong code.
    /// </summary>
    TwoFactorChallenged = 28,

    TwoFactorEnabled = 29,
    TwoFactorDisabled = 30,

    /// <summary>An admin turned an account's two-factor authentication off, so its user can set it up again.</summary>
    TwoFactorReset = 31,

    RecoveryCodesRegenerated = 32,

    /// <summary>Someone signed in with a recovery code: how many are left, never the code.</summary>
    RecoveryCodeUsed = 33,

    /// <summary>
    /// Someone asked for a password reset link: the account when one with a confirmed address matched, otherwise no
    /// account. Never what was typed.
    /// </summary>
    PasswordResetRequested = 34,

    /// <summary>A password was reset with an emailed link (an admin's reset is <see cref="PasswordReset"/>).</summary>
    PasswordResetByEmail = 35,

    /// <summary>Someone asked to use a new email address. Never the address.</summary>
    EmailChangeRequested = 36,

    EmailConfirmed = 37,
    EmailRemoved = 38,

    /// <summary>An admin changed whether admin accounts must use two-factor authentication.</summary>
    TwoFactorRequirementChanged = 39,

    /// <summary>Someone turned two-factor authentication on with codes by email. Never the code or the address.</summary>
    EmailCodesEnabled = 40,

    /// <summary>Someone turned off two-factor authentication that used codes by email.</summary>
    EmailCodesDisabled = 41,

    /// <summary>An admin changed which specialties someone belongs to: the specialties' IDs and names before and after.</summary>
    StaffSpecialtiesChanged = 42,

    /// <summary>Someone opened a referral's page: the referral's ID in its details.</summary>
    ReferralViewed = 43,

    /// <summary>
    /// An admin changed a role: renamed it, changed its description (never the text), added or removed permissions (by
    /// name), or archived or restored it. Giving people roles is <see cref="RolesChanged"/>.
    /// </summary>
    RoleChanged = 44,

    /// <summary>
    /// Someone opened a specimen's page or printed its label or request sheet, or found it by its number: the specimen's
    /// ID in its details. (46 and 47 are kept for results and letters: <c>docs/centre-phases-3-5-plan.md</c>, section 5.)
    /// </summary>
    SpecimenViewed = 45,

    /// <summary>
    /// Someone's full name or job title was changed, by themselves or by an admin: the names of the fields changed, never
    /// their values.
    /// </summary>
    StaffDetailsChanged = 48
}
