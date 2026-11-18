using FluentValidation;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.Roles.Common;

/// <summary>The validation rules shared by the role commands.</summary>
public static class RoleValidation
{
    /// <summary>A role's name: required, at most <see cref="RoleRules.MaxNameLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string> RoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required.")
            .Must(name => name is null || name.Trim().Length <= RoleRules.MaxNameLength)
            .WithMessage($"Name must not exceed {RoleRules.MaxNameLength} characters.");

    /// <summary>A role's description: optional, at most <see cref="RoleRules.MaxDescriptionLength"/> characters once trimmed.</summary>
    public static IRuleBuilderOptions<T, string?> RoleDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(description => description is null || description.Trim().Length <= RoleRules.MaxDescriptionLength)
            .WithMessage($"Description must not exceed {RoleRules.MaxDescriptionLength} characters.");

    /// <summary>
    /// One of a role's permissions: a permission's exact name, and not one of the Administrator-only ones, which only
    /// the built-in Admin role has (decision 56: so a role editor can't make admins by another name).
    /// </summary>
    public static IRuleBuilderOptions<T, string> RolePermission<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(name => Permissions.TryParse(name?.Trim(), out _))
            .WithMessage((_, name) => RoleRules.UnknownPermissionMessage(name ?? string.Empty))
            .Must(name => !Permissions.TryParse(name?.Trim(), out var permission) || !Permissions.Describe(permission).IsAdminOnly)
            .WithMessage((_, name) => RoleRules.AdminOnlyMessage(name?.Trim() ?? string.Empty));
}
