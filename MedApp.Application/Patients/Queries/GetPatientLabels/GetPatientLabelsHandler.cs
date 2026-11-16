using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Queries.GetPatientLabels;

/// <summary>Patients' names for the side nav's pins. Records nothing: a pin's label isn't a view of the record.</summary>
public sealed class GetPatientLabelsHandler(IPatientRepository repository)
    : IRequestHandler<GetPatientLabelsQuery, IReadOnlyList<PatientLabelResponse>>
{
    public async Task<IReadOnlyList<PatientLabelResponse>> Handle(GetPatientLabelsQuery request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var names = await repository.GetNamesAsync(ids, cancellationToken);
        return ids.Where(names.ContainsKey).Select(id => PatientMapper.ToLabel(id, names[id])).ToList();
    }
}
