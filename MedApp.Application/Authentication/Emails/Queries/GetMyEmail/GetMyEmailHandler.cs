using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;
using MediatR;

namespace MedApp.Application.Authentication.Emails.Queries.GetMyEmail;

public sealed class GetMyEmailHandler(IAccountEmailService emails)
    : IRequestHandler<GetMyEmailQuery, EmailResponse>
{
    public async Task<EmailResponse> Handle(GetMyEmailQuery request, CancellationToken ct)
    {
        var account = await emails.GetAsync(request.UserId, ct)
                      ?? throw new NotFoundException("Account not found.");

        return EmailMapper.ToResponse(account);
    }
}
