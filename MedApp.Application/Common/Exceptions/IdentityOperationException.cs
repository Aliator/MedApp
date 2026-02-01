namespace MedApp.Application.Common.Exceptions;

public sealed class IdentityOperationException(IDictionary<string, string[]> errors)
    : Exception("One or more identity errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>(errors);

    public IdentityOperationException(string error)
        : this(new Dictionary<string, string[]>
        {
            ["SingleIdentityError"] = [error]
        })
    {
    }
}