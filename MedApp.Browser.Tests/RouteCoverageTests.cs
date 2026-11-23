using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using MedApp.Browser.Hosting;

namespace MedApp.Browser.Tests;

/// <summary>
/// Every operation in the web app's API schema (<c>MedApp.Web/src/api/schema.d.ts</c>, generated from the API's Swagger
/// document) reaches exactly one action in the browser's route table, and the router picks that one, so an endpoint
/// added to the API without the browser's build compiling it fails here.
/// </summary>
[TestFixture]
public sealed partial class RouteCoverageTests
{
    private static readonly IReadOnlyList<ApiRoute> Routes = ApiRoute.All(typeof(ApiRoute).Assembly);

    [Test]
    public void Schema_EveryOperation_MatchesExactlyOneBrowserRoute()
    {
        var operations = SchemaOperations();
        operations.Should().HaveCountGreaterThan(300);

        var problems = new List<string>();
        foreach (var (method, path) in operations)
        {
            var matches = Routes.Where(route => route.Method == method && SameTemplate(route, path)).ToList();
            if (matches.Count != 1)
            {
                problems.Add($"{method} {path}: {matches.Count} routes");
                continue;
            }

            var (found, _) = ApiRoute.Find(Routes, method, SamplePath(matches[0]));
            if (found != matches[0])
                problems.Add($"{method} {path}: the router picks {found?.Method} {found?.Template}");
        }

        problems.Should().BeEmpty();
    }

    [Test]
    public void Routes_EveryBrowserRoute_IsInTheSchema()
    {
        var operations = SchemaOperations();
        var missing = Routes
            .Where(route => !operations.Any(op => op.Method == route.Method && SameTemplate(route, op.Path)))
            .Select(route => $"{route.Method} /{route.Template}")
            .ToList();

        missing.Should().BeEmpty();
    }

    private static bool SameTemplate(ApiRoute route, string schemaPath)
    {
        var segments = schemaPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == route.Segments.Length
               && segments.Zip(route.Segments).All(pair => pair.Second.IsLiteral
                   ? string.Equals(pair.First, pair.Second.Text, StringComparison.OrdinalIgnoreCase)
                   : string.Equals(pair.First, $"{{{pair.Second.Text}}}", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> SamplePath(ApiRoute route) =>
        route.Segments.Select(segment => segment.IsLiteral
            ? segment.Text
            : segment.Constraint switch
            {
                "guid" => "6f1d7c2e-0000-4000-8000-000000000001",
                "int" or "long" => "1",
                "bool" => "true",
                _ => "sample",
            }).ToList();

    /// <summary>Each path's operations in the schema: a method with a body, not <c>get?: never</c>.</summary>
    private static List<(string Method, string Path)> SchemaOperations()
    {
        var operations = new List<(string, string)>();
        string? path = null;
        foreach (var line in File.ReadLines(SchemaFile()))
        {
            if (line == "}")
                break;
            if (PathLine().Match(line) is { Success: true } pathMatch)
                path = pathMatch.Groups[1].Value;
            else if (path is not null && OperationLine().Match(line) is { Success: true } operation)
                operations.Add((operation.Groups[1].Value.ToUpperInvariant(), path));
        }

        return operations;
    }

    private static string SchemaFile()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName, "MedApp.Web", "src", "api", "schema.d.ts");
            if (File.Exists(file))
                return file;
        }

        throw new FileNotFoundException("MedApp.Web/src/api/schema.d.ts wasn't found above the tests' folder.");
    }

    [GeneratedRegex("""^    "(/api/[^"]*)": \{$""")]
    private static partial Regex PathLine();

    [GeneratedRegex("^        (get|put|post|delete|patch|options|head|trace): \\{$")]
    private static partial Regex OperationLine();
}
