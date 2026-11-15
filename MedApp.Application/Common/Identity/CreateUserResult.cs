using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace MedApp.Application.Common.Identity;

/// <summary>
/// The outcome of creating an account: Identity's result and, exactly when it succeeded, the first password the
/// server generated for it. That password works until the user changes it. It isn't stored anywhere in plain text, so
/// this is the only time it can be shown. Make one with <see cref="Created"/> or <see cref="Failed"/>, so a success
/// always carries a password and a failure never does.
/// </summary>
public sealed record CreateUserResult
{
    private CreateUserResult(IdentityResult result, string? generatedPassword)
    {
        Result = result;
        GeneratedPassword = generatedPassword;
    }

    public IdentityResult Result { get; }

    /// <summary>The generated first password, when the account was created; otherwise null.</summary>
    public string? GeneratedPassword { get; }

    /// <summary>Whether the account was created, in which case <see cref="GeneratedPassword"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(GeneratedPassword))]
    public bool Succeeded => GeneratedPassword is not null;

    /// <summary>The account was created with this generated password.</summary>
    public static CreateUserResult Created(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return new CreateUserResult(IdentityResult.Success, password);
    }

    /// <summary>Identity refused to create the account, for the reasons in <paramref name="result"/>.</summary>
    public static CreateUserResult Failed(IdentityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Succeeded)
            throw new ArgumentException("A failed result can't be a success.", nameof(result));

        return new CreateUserResult(result, null);
    }

    // Never prints the password, so logging this result can't leak it.
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"{nameof(Result)} = {Result}, {nameof(GeneratedPassword)} = {(GeneratedPassword is null ? "null" : "***")}");
        return true;
    }
}
