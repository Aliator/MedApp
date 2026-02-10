using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Shared.Components.FormGroups;

public partial class DatePickerFormGroup
{
    [Parameter] public DateOnly Value { get; set; }
    [Parameter] public EventCallback<DateOnly> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "Date of Birth";
    [Parameter] public string SectionTitle { get; set; } = "Personal Information";
    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ShowAge { get; set; } = true;

    private int _selectedMonth;
    private int _selectedDay;
    private int _selectedYear;
    private string? _errorMessage;

    public string? ErrorMessage => _errorMessage;

    protected override void OnParametersSet()
    {
        if (Value != default && (_selectedYear == 0 || _selectedMonth == 0 || _selectedDay == 0))
        {
            InitializeDateDropdowns(Value);
        }
    }

    private int SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            _selectedMonth = value;
            UpdateDateOfBirth();
        }
    }

    private int SelectedDay
    {
        get => _selectedDay;
        set
        {
            _selectedDay = value;
            UpdateDateOfBirth();
        }
    }

    private int SelectedYear
    {
        get => _selectedYear;
        set
        {
            _selectedYear = value;
            UpdateDateOfBirth();
        }
    }

    private void InitializeDateDropdowns(DateOnly date)
    {
        _selectedYear = date.Year;
        _selectedMonth = date.Month;
        _selectedDay = date.Day;
    }

    private async void UpdateDateOfBirth()
    {
        _errorMessage = null;

        if (_selectedYear > 0 && _selectedMonth > 0 && _selectedDay > 0)
        {
            try
            {
                var date = new DateOnly(_selectedYear, _selectedMonth, _selectedDay);
                
                if (date > DateOnly.FromDateTime(DateTime.Today))
                {
                    _errorMessage = "Date of birth cannot be in the future";
                    return;
                }

                Value = date;
                await ValueChanged.InvokeAsync(Value);
            }
            catch (ArgumentOutOfRangeException)
            {
                _errorMessage = "Invalid date combination";
            }
        }
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        if (dateOfBirth == default)
            return 0;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }
        
        return age;
    }
}