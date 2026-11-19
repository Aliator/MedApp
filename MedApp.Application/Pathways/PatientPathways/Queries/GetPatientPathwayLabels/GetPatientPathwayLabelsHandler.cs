using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Queries.GetPatientPathwayLabels;

/// <summary>Pathways' titles and patients' names for the side nav's pins. Records nothing: a pin's label isn't a view of the record.</summary>
public sealed class GetPatientPathwayLabelsHandler(IPatientPathwayRepository repository)
    : IRequestHandler<GetPatientPathwayLabelsQuery, IReadOnlyList<PatientPathwayLabelResponse>>
{
    public async Task<IReadOnlyList<PatientPathwayLabelResponse>> Handle(GetPatientPathwayLabelsQuery request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var labels = (await repository.GetLabelsAsync(ids, request.Teams, cancellationToken)).ToDictionary(l => l.Id);
        return ids.Where(labels.ContainsKey).Select(id => PathwayMapper.ToLabel(labels[id])).ToList();
    }
}
