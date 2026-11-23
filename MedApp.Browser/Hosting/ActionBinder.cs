using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace MedApp.Browser.Hosting;

/// <summary>
/// Binds an action's parameters as MVC does for an <c>[ApiController]</c>: route values, the query string (into simple
/// values, lists and request objects), headers, services and the JSON body, with the sources MVC infers when none is
/// named. Then validates them as MVC does: DataAnnotations, and non-nullable reference types implicitly required.
/// Problems are collected as MVC's model state (key, messages) for the 400.
/// </summary>
public sealed class ActionBinder(HttpContext http, ApiRoute route, byte[] body)
{
    private const int MaxDepth = 32;

    private static readonly NullabilityInfoContext Nullability = new();

    public Dictionary<string, List<string>> Errors { get; } = new(StringComparer.Ordinal);

    /// <summary>Set when the body isn't JSON: MVC answers 415 before binding anything else.</summary>
    public bool UnsupportedMediaType { get; private set; }

    public object?[] Bind()
    {
        var parameters = route.Action.GetParameters();
        var values = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            values[i] = BindParameter(parameters[i]);
        return values;
    }

    private object? BindParameter(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        if (type == typeof(CancellationToken))
            return http.RequestAborted;

        if (parameter.GetCustomAttribute<FromServicesAttribute>() is not null)
            return http.RequestServices.GetRequiredService(type);

        var name = parameter.Name!;
        if (parameter.GetCustomAttribute<FromBodyAttribute>() is { } fromBody)
            return BindBody(parameter, fromBody.EmptyBodyBehavior);

        if (parameter.GetCustomAttribute<FromHeaderAttribute>() is { } fromHeader)
            return BindSimple(parameter, http.Request.Headers[fromHeader.Name ?? name], fromHeader.Name ?? name);

        if (parameter.GetCustomAttribute<FromRouteAttribute>() is { } fromRoute)
            return BindRoute(parameter, fromRoute.Name ?? name);

        if (parameter.GetCustomAttribute<FromQueryAttribute>() is { } fromQuery)
            return BindQuery(parameter, fromQuery.Name ?? name);

        // What [ApiController] infers: a complex type from the body, a name in the template from the route, and any
        // other simple value from the query string.
        if (!IsSimple(type) && !IsSimpleCollection(type))
            return BindBody(parameter, EmptyBodyBehavior.Default);
        if (route.ParameterNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return BindRoute(parameter, name);
        return BindQuery(parameter, name);
    }

    private object? BindRoute(ParameterInfo parameter, string name) =>
        BindSimple(parameter, http.Request.RouteValues.TryGetValue(name, out var value) ? new StringValues(value?.ToString()) : StringValues.Empty, name);

    private object? BindSimple(ParameterInfo parameter, StringValues values, string key)
    {
        var type = parameter.ParameterType;
        object? value;
        if (values.Count == 0 || (values.Count == 1 && string.IsNullOrEmpty(values[0]) && !IsSimpleCollection(type)))
        {
            value = parameter.HasDefaultValue ? parameter.DefaultValue : Default(type);
            if (value is null && IsRequiredReference(Nullability.Create(parameter), type))
                AddError(key, RequiredMessage(key));
            return value;
        }

        return Convert(values, type, key, key);
    }

    private object? BindQuery(ParameterInfo parameter, string name)
    {
        var type = parameter.ParameterType;
        if (IsSimple(type) || IsSimpleCollection(type))
            return BindSimple(parameter, QueryValues(name), name);

        var prefix = http.Request.Query.Keys.Any(key => key.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase)
                                                        || key.StartsWith(name + "[", StringComparison.OrdinalIgnoreCase))
            ? name
            : string.Empty;
        var model = BindQueryObject(type, prefix, 0);
        Validate(model, prefix, 0);
        return model;
    }

    private object? BindQueryObject(Type type, string prefix, int depth)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<BindNeverAttribute>() is null)
            .ToList();

        var constructor = type.GetConstructor(Type.EmptyTypes) ?? type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var arguments = constructor.GetParameters().Select(p =>
        {
            var key = Join(prefix, p.Name!);
            var values = QueryValues(key);
            return values.Count == 0 ? (p.HasDefaultValue ? p.DefaultValue : Default(p.ParameterType)) : Convert(values, p.ParameterType, key, null);
        }).ToArray();
        var model = constructor.Invoke(arguments);

        foreach (var property in properties.Where(p => p.SetMethod is not null))
        {
            if (constructor.GetParameters().Any(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var key = Join(prefix, property.Name);
            if (!IsSimple(property.PropertyType) && !IsSimpleCollection(property.PropertyType))
            {
                if (depth < MaxDepth && http.Request.Query.Keys.Any(k => k.StartsWith(key + ".", StringComparison.OrdinalIgnoreCase)))
                    property.SetValue(model, BindQueryObject(property.PropertyType, key, depth + 1));
                continue;
            }

            var values = QueryValues(key);
            if (values.Count == 0)
                continue;
            var converted = Convert(values, property.PropertyType, key, property.Name);
            if (!Errors.ContainsKey(key))
                property.SetValue(model, converted);
        }

        return model;
    }

    /// <summary>A key's values: repeated (<c>ids=a&amp;ids=b</c>) or indexed (<c>ids[0]=a</c>), matched ignoring case.</summary>
    private StringValues QueryValues(string key)
    {
        var query = http.Request.Query;
        var found = query.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
        if (found.Key is not null)
            return found.Value;

        var indexed = query
            .Where(pair => pair.Key.StartsWith(key + "[", StringComparison.OrdinalIgnoreCase) && pair.Key.EndsWith(']'))
            .OrderBy(pair => int.TryParse(pair.Key[(key.Length + 1)..^1], out var i) ? i : int.MaxValue)
            .SelectMany(pair => pair.Value)
            .ToArray();
        return indexed.Length > 0 ? new StringValues(indexed) : StringValues.Empty;
    }

    private object? BindBody(ParameterInfo parameter, EmptyBodyBehavior behavior)
    {
        var type = parameter.ParameterType;
        var allowEmpty = behavior == EmptyBodyBehavior.Allow
                         || (behavior == EmptyBodyBehavior.Default
                             && (parameter.HasDefaultValue || Nullable.GetUnderlyingType(type) is not null
                                 || Nullability.Create(parameter).ReadState == NullabilityState.Nullable));

        if (body.Length == 0)
        {
            if (!allowEmpty)
                AddError(string.Empty, "A non-empty request body is required.");
            return parameter.HasDefaultValue ? parameter.DefaultValue : Default(type);
        }

        if (!IsJson(http.Request.ContentType))
        {
            UnsupportedMediaType = true;
            return null;
        }

        object? model;
        try
        {
            model = JsonSerializer.Deserialize(body, type, ApiJson.Options);
        }
        catch (JsonException e)
        {
            AddError(e.Path ?? "$", e.Message);
            if (!allowEmpty)
                AddError(parameter.Name!, RequiredMessage(parameter.Name!));
            return null;
        }

        if (model is null)
        {
            if (!allowEmpty)
                AddError(parameter.Name!, RequiredMessage(parameter.Name!));
            return null;
        }

        Validate(model, string.Empty, 0);
        return model;
    }

    private static bool IsJson(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;
        var mediaType = contentType.Split(';')[0].Trim();
        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
               || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
               || (mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase)
                   && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>MVC's validation of a bound object: each property's attributes, then into complex values and lists.</summary>
    private void Validate(object? model, string prefix, int depth)
    {
        if (model is null || depth > MaxDepth || IsSimple(model.GetType()))
            return;

        if (model is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                Validate(entry.Value, $"{prefix}[{entry.Key}]", depth + 1);
            return;
        }

        if (model is IEnumerable items and not string)
        {
            var index = 0;
            foreach (var item in items)
                Validate(item, $"{prefix}[{index++}]", depth + 1);
            return;
        }

        foreach (var property in model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod is null)
                continue;

            var key = Join(prefix, property.Name);
            var value = property.GetValue(model);
            var attributes = property.GetCustomAttributes<ValidationAttribute>().ToList();
            if (attributes.All(a => a is not RequiredAttribute) && IsRequiredReference(Nullability.Create(property), property.PropertyType))
                attributes.Insert(0, new RequiredAttribute { AllowEmptyStrings = true });

            var context = new ValidationContext(model) { MemberName = property.Name, DisplayName = property.Name };
            foreach (var attribute in attributes)
            {
                if (attribute.GetValidationResult(value, context) is { ErrorMessage: { } message })
                    AddError(key, message);
            }

            if (value is not null && !IsSimple(property.PropertyType))
                Validate(value, key, depth + 1);
        }
    }

    private object? Convert(StringValues values, Type type, string key, string? propertyName)
    {
        var elementType = CollectionElement(type);
        if (elementType is not null && type != typeof(string))
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (var value in values)
            {
                if (TryConvert(value, elementType, out var item))
                    list.Add(item);
                else
                    AddError(key, InvalidMessage(value, propertyName));
            }

            if (type.IsArray)
            {
                var array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        var single = values[^1];
        if (TryConvert(single, type, out var converted))
            return converted;

        AddError(key, InvalidMessage(single, propertyName));
        return Default(type);
    }

    private static bool TryConvert(string? text, Type type, out object? value)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        value = null;
        if (string.IsNullOrEmpty(text))
            return target == typeof(string) || Nullable.GetUnderlyingType(type) is not null || !type.IsValueType;

        try
        {
            if (target == typeof(string))
                value = text;
            else if (target == typeof(DateTime))
                value = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AllowWhiteSpaces);
            else if (target.IsEnum)
            {
                if (!Enum.TryParse(target, text, ignoreCase: true, out var parsed)
                    || (!Enum.IsDefined(target, parsed!) && target.GetCustomAttribute<FlagsAttribute>() is null))
                    return false;
                value = parsed;
            }
            else
                value = TypeDescriptor.GetConverter(target).ConvertFromString(null, CultureInfo.InvariantCulture, text);
            return true;
        }
        catch (Exception e) when (e is FormatException or ArgumentException or NotSupportedException or OverflowException
                                      || e.InnerException is FormatException or OverflowException)
        {
            return false;
        }
    }

    private void AddError(string key, string message)
    {
        if (!Errors.TryGetValue(key, out var messages))
            Errors[key] = messages = [];
        messages.Add(message);
    }

    private static string RequiredMessage(string name) => $"The {name} field is required.";

    private static string InvalidMessage(string? value, string? propertyName) =>
        propertyName is null ? $"The value '{value}' is not valid." : $"The value '{value}' is not valid for {propertyName}.";

    private static string Join(string prefix, string name) => prefix.Length == 0 ? name : $"{prefix}.{name}";

    private static bool IsRequiredReference(NullabilityInfo nullability, Type type) =>
        !type.IsValueType && nullability.ReadState == NullabilityState.NotNull;

    private static object? Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    /// <summary>A type MVC binds from one string: anything with a converter from text (numbers, Guids, dates, enums).</summary>
    public static bool IsSimple(Type type)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        return target.IsPrimitive || target.IsEnum || target == typeof(string) || target == typeof(decimal)
               || target == typeof(Guid) || target == typeof(DateTime) || target == typeof(DateOnly) || target == typeof(TimeOnly)
               || target == typeof(DateTimeOffset) || target == typeof(TimeSpan)
               || TypeDescriptor.GetConverter(target).CanConvertFrom(typeof(string));
    }

    private static bool IsSimpleCollection(Type type) => CollectionElement(type) is { } element && IsSimple(element);

    private static Type? CollectionElement(Type type)
    {
        if (type == typeof(string))
            return null;
        if (type.IsArray)
            return type.GetElementType();
        if (type.IsGenericType && type.GetGenericArguments() is [var argument]
                               && typeof(IEnumerable<>).MakeGenericType(argument).IsAssignableFrom(type))
            return argument;
        return null;
    }
}
