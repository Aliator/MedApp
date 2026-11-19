using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Conversions;

/// <summary>
/// A property stored as one JSON column (<c>nvarchar(max)</c>): a template's links, outcomes and addable branches, a
/// pathway's plan and a branch addition's stages (MC8 to MC10b). Values are records and lists, compared by their JSON,
/// so a value replaced with an equal one isn't saved. Nothing in them is queried in SQL.
/// </summary>
public static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> property)
    {
        property.HasConversion(
            value => Write(value),
            json => Read<T>(json),
            new ValueComparer<T>(
                (a, b) => Write(a) == Write(b),
                value => Write(value).GetHashCode(),
                value => Read<T>(Write(value))));
        return property;
    }

    private static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}
