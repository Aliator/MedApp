using MedApp.Application.Common.Authentication;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Infrastructure.Common.Authentication;

public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager)
    : IAuthenticationService
{
    public async Task<(Guid, string, IEnumerable<string>)>
        ValidateCredentialsAsync(string username, string password)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
            throw new InvalidCredentialsException();

        var valid = await userManager.CheckPasswordAsync(user, password);
        if (!valid)
            throw new InvalidCredentialsException();

        var roles = await userManager.GetRolesAsync(user);

        return (user.Id, user.UserName!, roles);
    }
}
