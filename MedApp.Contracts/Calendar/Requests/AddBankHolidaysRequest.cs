namespace MedApp.Contracts.Calendar.Requests;

public sealed class AddBankHolidaysRequest
{
    /// <summary>The year whose England and Wales bank holidays to add, 2000 to 2100.</summary>
    public int Year { get; set; }
}
