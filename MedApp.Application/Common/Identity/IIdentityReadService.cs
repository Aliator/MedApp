namespace MedApp.Application.Common.Identity;

public interface IIdentityReadService
{
    Task<IReadOnlyList<string>> GetUsernamesAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(CancellationToken ct);
}