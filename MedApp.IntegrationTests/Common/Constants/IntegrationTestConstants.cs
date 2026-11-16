namespace MedApp.IntegrationTests.Common.Constants;

/// <summary>Synthetic data for the integration tests. Never realistic patient details.</summary>
public static class IntegrationTestConstants
{
    /// <summary>The admin every test starts with, made by the deploy's own first-admin setup.</summary>
    public const string AdminUsername = "it-admin";

    public const string AdminPassword = "ItAdminPassw0rd";

    /// <summary>The password a new account chooses when it changes its generated one.</summary>
    public const string ChosenPassword = "ItChosenPassw0rd";

    public const string AnotherChosenPassword = "ItAnotherPassw0rd";

    public const string WrongPassword = "NotThePassw0rd";

    public const string AdminRole = "Admin";

    public const string UserRole = "User";

    public const string PatientEmailDomain = "example.test";

    /// <summary>A date of birth comfortably in the past.</summary>
    public static readonly DateOnly DateOfBirth = new(1980, 5, 17);

    public const string StageNotes = "<p>Synthetic stage notes.</p>";

    public const string BlockReason = "Waiting on a synthetic test result.";
}
