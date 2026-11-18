using MedApp.Application.Common.Identity;
using MedApp.Contracts.Authentication.Responses;

namespace MedApp.Application.Authentication.Users.Common;

/// <summary>A member of staff's full name and job title, as the API returns them.</summary>
public static class StaffDetailsMapper
{
    public static StaffDetailsResponse ToResponse(StaffDetails details) => new()
    {
        FullName = details.FullName,
        JobTitle = details.JobTitle
    };
}
