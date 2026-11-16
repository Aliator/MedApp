using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.Repositories;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Queries.GetPatientProcedureLabels;

/// <summary>Procedures' titles and patients' names for the side nav's pins. Records nothing: a pin's label isn't a view of the record.</summary>
public sealed class GetPatientProcedureLabelsHandler(IPatientProcedureRepository repository)
    : IRequestHandler<GetPatientProcedureLabelsQuery, IReadOnlyList<PatientProcedureLabelResponse>>
{
    public async Task<IReadOnlyList<PatientProcedureLabelResponse>> Handle(GetPatientProcedureLabelsQuery request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var labels = (await repository.GetLabelsAsync(ids, cancellationToken)).ToDictionary(l => l.Id);
        return ids.Where(labels.ContainsKey).Select(id => ProcedureMapper.ToLabel(labels[id])).ToList();
    }
}
