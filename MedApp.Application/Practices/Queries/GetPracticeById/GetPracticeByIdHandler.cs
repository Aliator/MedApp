using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Queries.GetPracticeById;

public sealed class GetPracticeByIdHandler(IPracticeRepository repository)
    : IRequestHandler<GetPracticeByIdQuery, PracticeResponse>
{
    public async Task<PracticeResponse> Handle(GetPracticeByIdQuery request, CancellationToken cancellationToken)
    {
        var practice = await repository.GetOrThrowAsync(request.PracticeId, cancellationToken);
        return await PracticeMapper.ToResponseAsync(practice, repository, cancellationToken);
    }
}
