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

    /// <summary>Someone opened a procedure's page.</summary>
    ProcedureViewed = 13,

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

    RoleCreated = 22,
    RoleDeleted = 23,

    /// <summary>An admin ended every session an account had ("Sign out everywhere").</summary>
    SignedOutEverywhere = 24
}
