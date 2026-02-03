namespace MedApp.UnitTests.Common;

public static class TestConstants
{
    public static class Patients
    {
        public const string ValidFirstName = "FirstName";
        public const string ValidLastName = "LastName";
        public const string ValidEmail = "test@test.com";
        public static readonly DateOnly ValidDateOfBirth = new(1111, 1, 1);
        public const int MaxNameLength = 100;
    }

    public static class Auth
    {
        public const string AdminRole = "Admin";
        public const string UserRole = "User";
    }
}