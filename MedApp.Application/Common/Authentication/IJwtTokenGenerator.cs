namespace MedApp.Application.Common.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateToken(
        Guid userId,
        string username,
        IEnumerable<string> roles);
}