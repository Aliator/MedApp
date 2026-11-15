namespace MedApp.Application.Common.Authentication;

public interface IAuthenticationService
{
    Task<(Guid UserId, string Username, IEnumerable<string> Roles)> 
        ValidateCredentialsAsync(string username, string password);
}