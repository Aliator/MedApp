using MedApp.Client.Models;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Forms.FormGroups;

public partial class PartialDatePickerFormGroup : ComponentBase
{
    [Parameter] public PartialDate Value { get; set; } = PartialDate.Empty;
    [Parameter] public EventCallback<PartialDate> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "Date of Birth";
    [Parameter] public bool Disabled { get; set; }

    private int _selectedMonth;
    private int _selectedDay;
    private int _selectedYear;
    private string? _errorMessage;

    protected override void OnParametersSet()
    {
        _selectedMonth = Value.Month ?? 0;
        _selectedDay = Value.Day ?? 0;
        _selectedYear = Value.Year ?? 0;
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

        if (_selectedYear > 0 && _selectedMonth > 0 && _selectedDay > 0)
        {
            try
            {
                _ = new DateOnly(_selectedYear, _selectedMonth, _selectedDay);
            }
            catch (ArgumentOutOfRangeException)
            {
                _errorMessage = "Invalid date combination";
                return;
            }
        }

        await ValueChanged.InvokeAsync(PartialDate.From(_selectedYear, _selectedMonth, _selectedDay));
    }
}