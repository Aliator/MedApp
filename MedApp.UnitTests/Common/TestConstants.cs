namespace MedApp.UnitTests.Common;

public static class TestConstants
{
    public static class Patients
    {
        public const string ValidFirstName = "John";
        public const string ValidLastName = "Smith";
        public const string ValidEmail = "john.smith@test.com";
        public static readonly DateOnly ValidDateOfBirth =
            new(1990, 1, 1);
    }

    public static class Auth
    {
        public const string AdminRole = "Admin";
        public const string UserRole = "User";
    }
}