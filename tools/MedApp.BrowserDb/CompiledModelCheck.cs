using System.Reflection;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MedApp.BrowserDb;

/// <summary>
/// The browser runs on a compiled model (<c>MedApp.Browser/Data/CompiledModel</c>), which doesn't change when the
/// entities or their configurations do. This checks it still describes the model as the app builds it (the design-time
/// model, which the database is made from), as the app runs it: the same tables, columns, keys and indexes, and the
/// same properties and what their converters store. (What only makes the schema, such as collations, isn't in a compiled model.)
/// </summary>
public static class CompiledModelCheck
{
    public const string Regenerate = "tools/MedApp.BrowserDb/optimize-model.sh";

    /// <summary>Says the compiled model matches; throws, naming the differences, if it doesn't.</summary>
    public static string Run(MedAppDbContext context)
    {
        if (!typeof(MedAppDbContext).Assembly.GetCustomAttributes<DbContextModelAttribute>().Any())
            return $"No compiled model (MedApp.Browser/Data/CompiledModel): the browser builds the model at start. {Regenerate} makes one.";

        var expected = Describe(context.GetService<IDesignTimeModel>().Model);
        var actual = Describe(context.Model);
        var missing = expected.Except(actual).ToList();
        var extra = actual.Except(expected).ToList();
        if (missing.Count == 0 && extra.Count == 0)
            return $"The compiled model matches the model ({expected.Count} facts).";

        throw new InvalidOperationException(
            $"The compiled model (MedApp.Browser/Data/CompiledModel) is out of date: run {Regenerate}.\n" +
            string.Join("\n", missing.Take(20).Select(m => "  missing: " + m).Concat(extra.Take(20).Select(e => "  extra:   " + e))));
    }

    private static HashSet<string> Describe(IModel model)
    {
        var facts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in model.GetRelationalModel().Tables)
        {
            foreach (var column in table.Columns)
                facts.Add($"column {table.Name}.{column.Name} {column.StoreType} {(column.IsNullable ? "null" : "not null")}");
            if (table.PrimaryKey is { } key)
                facts.Add($"key {table.Name} {key.Name} ({string.Join(", ", key.Columns.Select(c => c.Name))})");
            foreach (var index in table.Indexes)
                facts.Add($"index {table.Name} {index.Name} ({string.Join(", ", index.Columns.Select(c => c.Name))}) {(index.IsUnique ? "unique" : "")}");
            foreach (var foreignKey in table.ForeignKeyConstraints)
                facts.Add($"foreign key {table.Name} {foreignKey.Name} -> {foreignKey.PrincipalTable.Name} {foreignKey.OnDeleteAction}");
        }

        foreach (var entity in model.GetEntityTypes())
        {
            facts.Add($"entity {entity.Name} {(entity.GetDeclaredQueryFilters().Count > 0 ? "filtered" : "")}");
            foreach (var property in entity.GetDeclaredProperties())
                facts.Add($"property {entity.Name}.{property.Name} {property.ClrType.Name} {property.GetTypeMapping().Converter?.ProviderClrType.Name} {property.ValueGenerated} {property.IsConcurrencyToken}");
            foreach (var navigation in entity.GetDeclaredNavigations())
                facts.Add($"navigation {entity.Name}.{navigation.Name}");
        }

        return facts;
    }
}
