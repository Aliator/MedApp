using MedApp.Client.Components.Modals;
using Microsoft.AspNetCore.Components;

namespace MedApp.Client.Components.Modals;

public partial class SearchModal : ComponentBase
{
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public PatientSearchCriteria Criteria { get; set; } = new(string.Empty, string.Empty, null, null);
    [Parameter] public EventCallback<PatientSearchCriteria> OnSearch { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private ElementReference _firstInputRef;

    private string _name = string.Empty;
    private string _email = string.Empty;
    private int? _ageMin;
    private int? _ageMax;

    protected override async Task OnParametersSetAsync()
    {
        if (IsVisible)
        {
            _name = Criteria.Name ?? string.Empty;
            _email = Criteria.Email ?? string.Empty;
            _ageMin = Criteria.AgeMin;
            _ageMax = Criteria.AgeMax;

            await Task.Yield();
            try { await _firstInputRef.FocusAsync(); } catch { }
        }
    }

    private async Task Apply()
    {
        await OnSearch.InvokeAsync(new PatientSearchCriteria(_name, _email, _ageMin, _ageMax));
        await OnClose.InvokeAsync();
    }

    private async Task Clear()
    {
        _name = string.Empty;
        _email = string.Empty;
        _ageMin = null;
        _ageMax = null;
        await OnSearch.InvokeAsync(new PatientSearchCriteria(string.Empty, string.Empty, null, null));
        await OnClose.InvokeAsync();
    }
}

public record PatientSearchCriteria(string Name, string Email, int? AgeMin, int? AgeMax);