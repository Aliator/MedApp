using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MedApp.Browser.Hosting;

/// <summary>One controller action at one route: its HTTP method and template, as MVC's attribute routing makes them.</summary>
public sealed class ApiRoute
{
    private ApiRoute(string method, string template, Type controller, MethodInfo action)
    {
        Method = method;
        Template = template;
        Controller = controller;
        Action = action;
        Segments = template.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(RouteSegment.Parse).ToArray();
    }

    public string Method { get; }
    public string Template { get; }
    public Type Controller { get; }
    public MethodInfo Action { get; }
    public RouteSegment[] Segments { get; }

    public IEnumerable<string> ParameterNames => Segments.Where(s => !s.IsLiteral).Select(s => s.Text);

    /// <summary>Every action of every controller in the assembly, most specific first (MVC's route precedence).</summary>
    public static IReadOnlyList<ApiRoute> All(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && type.IsSubclassOf(typeof(ControllerBase)))
            .SelectMany(ForController)
            .OrderBy(route => route, PrecedenceComparer.Instance)
            .ToList();

    private static IEnumerable<ApiRoute> ForController(Type controller)
    {
        var prefixes = controller.GetCustomAttributes<RouteAttribute>(inherit: true).Select(r => r.Template).DefaultIfEmpty(string.Empty).ToList();
        foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
            {
                foreach (var prefix in prefixes)
                    yield return new ApiRoute(http.Method, Combine(prefix, http.Template), controller, action);
            }
        }
    }

    private static string Combine(string prefix, string? template) =>
        template switch
        {
            null or "" => prefix,
            _ when template.StartsWith("~/", StringComparison.Ordinal) => template[2..],
            _ when template.StartsWith('/') => template[1..],
            _ => $"{prefix.TrimEnd('/')}/{template}",
        };

    /// <summary>The first route, in precedence order, for the method and the path's segments (decoded), with its values.</summary>
    public static (ApiRoute? Route, Dictionary<string, string>? Values) Find(IEnumerable<ApiRoute> routes, string method, IReadOnlyList<string> path)
    {
        foreach (var route in routes)
        {
            if (string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase) && route.Match(path) is { } values)
                return (route, values);
        }

        return (null, null);
    }

    /// <summary>The route values when the path's segments (decoded) match the template, or null.</summary>
    public Dictionary<string, string>? Match(IReadOnlyList<string> path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Segments.Length; i++)
        {
            var segment = Segments[i];
            if (segment.IsCatchAll)
            {
                values[segment.Text] = string.Join('/', path.Skip(i));
                return values;
            }

            if (i >= path.Count)
                return segment.IsOptional ? values : null;

            if (segment.IsLiteral)
            {
                if (!string.Equals(segment.Text, path[i], StringComparison.OrdinalIgnoreCase))
                    return null;
            }
            else
            {
                if (!segment.Accepts(path[i]))
                    return null;
                values[segment.Text] = path[i];
            }
        }

        return path.Count == Segments.Length ? values : null;
    }

    /// <summary>The path for these route values, as MVC's URL helper makes it for <c>CreatedAtAction</c>.</summary>
    public string Path(IReadOnlyDictionary<string, object?> values) =>
        "/" + string.Join('/', Segments.Select(segment => segment.IsLiteral
            ? segment.Text
            : Uri.EscapeDataString(Convert.ToString(values.GetValueOrDefault(segment.Text), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)));

    private sealed class PrecedenceComparer : IComparer<ApiRoute>
    {
        public static readonly PrecedenceComparer Instance = new();

        public int Compare(ApiRoute? x, ApiRoute? y)
        {
            for (var i = 0; i < Math.Max(x!.Segments.Length, y!.Segments.Length); i++)
            {
                var a = i < x.Segments.Length ? x.Segments[i].Precedence : 0;
                var b = i < y.Segments.Length ? y.Segments[i].Precedence : 0;
                if (a != b)
                    return a.CompareTo(b);
            }

            return string.CompareOrdinal(x.Template, y.Template);
        }
    }
}

/// <summary>A template's segment: a literal, or a parameter (<c>{id:guid}</c>, <c>{name?}</c>, <c>{*rest}</c>).</summary>
public sealed record RouteSegment(string Text, bool IsLiteral, string? Constraint, bool IsOptional, bool IsCatchAll)
{
    /// <summary>MVC's order: literals first, then constrained parameters, then plain ones, optional ones, catch-alls.</summary>
    public int Precedence =>
        IsLiteral ? 1 : IsCatchAll ? 5 : IsOptional ? 4 : Constraint is not null ? 2 : 3;

    public static RouteSegment Parse(string segment)
    {
        if (!segment.StartsWith('{') || !segment.EndsWith('}'))
            return new RouteSegment(segment, true, null, false, false);

        var inner = segment[1..^1];
        var catchAll = inner.StartsWith('*');
        inner = inner.TrimStart('*');
        var optional = inner.EndsWith('?');
        inner = inner.TrimEnd('?');
        var colon = inner.IndexOf(':');
        return colon < 0
            ? new RouteSegment(inner, false, null, optional, catchAll)
            : new RouteSegment(inner[..colon], false, inner[(colon + 1)..], optional, catchAll);
    }

    public bool Accepts(string value) =>
        Constraint switch
        {
            null => value.Length > 0,
            "guid" => Guid.TryParse(value, out _),
            "int" => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
            "long" => long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
            "bool" => bool.TryParse(value, out _),
            _ => throw new NotSupportedException($"The route constraint '{Constraint}' isn't supported in the browser."),
        };
}
