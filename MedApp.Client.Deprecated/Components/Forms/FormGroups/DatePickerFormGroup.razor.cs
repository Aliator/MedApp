using MedApp.Client.Models;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Forms.FormGroups;

public partial class DatePickerFormGroup : ComponentBase
{
    [Parameter] public DateOnly Value { get; set; }
    [Parameter] public EventCallback<DateOnly> ValueChanged { get; set; }
    [Parameter] public PartialDate PartialValue { get; set; } = PartialDate.Empty;
    [Parameter] public EventCallback<PartialDate> PartialValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "Date of Birth";
    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ShowAge { get; set; } = true;
    [Parameter] public bool Partial { get; set; }

    private int _selectedMonth;
    private int _selectedDay;
    private int _selectedYear;
    private string? _errorMessage;

    public string? ErrorMessage => _errorMessage;

    protected override void OnParametersSet()
    {
        if (Partial)
        {
            _selectedMonth = PartialValue.Month ?? 0;
            _selectedDay = PartialValue.Day ?? 0;
            _selectedYear = PartialValue.Year ?? 0;
        }
        else if (Value != default && (_selectedYear == 0 || _selectedMonth == 0 || _selectedDay == 0))
        {
            _selectedYear = Value.Year;
            _selectedMonth = Value.Month;
            _selectedDay = Value.Day;
        }
    }

    private int SelectedMonth
    {
        get => _selectedMonth;
        set { _selectedMonth = value; Notify(); }
    }

    private int SelectedDay
    {
        get => _selectedDay;
        set { _selectedDay = value; Notify(); }
    }

    private int SelectedYear
    {
        get => _selectedYear;
        set { _selectedYear = value; Notify(); }
    }

    private async void Notify()
    {
        _errorMessage = null;

        if (Partial)
        {
            if (_selectedYear > 0 && _selectedMonth > 0 && _selectedDay > 0)
            {
                try { _ = new DateOnly(_selectedYear, _selectedMonth, _selectedDay); }
                catch (ArgumentOutOfRangeException)
                {
                    _errorMessage = "Invalid date combination";
                    return;
                }
            }
            await PartialValueChanged.InvokeAsync(PartialDate.From(_selectedYear, _selectedMonth, _selectedDay));
        }
        else
        {
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
    }

    private static int CalculateAge(DateOnly dateOfBirth)
    {
        if (dateOfBirth == default) return 0;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age;
    }
}