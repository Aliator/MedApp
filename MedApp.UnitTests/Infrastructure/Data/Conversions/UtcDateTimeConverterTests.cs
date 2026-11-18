using FluentAssertions;
using MedApp.Infrastructure.Data;
using MedApp.Infrastructure.Data.Conversions;
using Microsoft.EntityFrameworkCore;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Infrastructure.Data.Conversions;

[TestFixture]
public sealed class UtcDateTimeConverterTests
{
    /// <summary><see cref="Now"/> as SQL Server's <c>datetime2</c> gives it back: the same time, with no time zone.</summary>
    private static readonly DateTime StoredNow = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);

    private readonly UtcDateTimeConverter _converter = new();

    [Test]
    public void ConvertFromProvider_StoredValue_IsTheSameTimeMarkedUtc()
    {
        var read = (DateTime)_converter.ConvertFromProvider(StoredNow)!;

        read.Kind.Should().Be(DateTimeKind.Utc);
        read.Ticks.Should().Be(Now.Ticks);
    }

    [TestCase(DateTimeKind.Utc)]
    [TestCase(DateTimeKind.Unspecified)]
    public void ConvertToProvider_Value_IsWrittenAsItIs(DateTimeKind kind)
    {
        var value = DateTime.SpecifyKind(Now, kind);

        var written = (DateTime)_converter.ConvertToProvider(value)!;

        written.Should().Be(value);
        written.Kind.Should().Be(kind);
    }

    [Test]
    public void ConvertToProvider_LocalTime_IsWrittenAsUtc()
    {
        var local = Now.ToLocalTime();

        var written = (DateTime)_converter.ConvertToProvider(local)!;

        written.Should().Be(Now);
        written.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Test]
    public void Model_EveryDateTimeProperty_IsReadAsUtc()
    {
        var properties = DateTimeProperties().ToList();

        properties.Should().Contain(p => p.ClrType == typeof(DateTime?), "the convention must cover nullable times too");
        properties.Should().AllSatisfy(p => p.GetValueConverter().Should().BeOfType<UtcDateTimeConverter>(
            $"{p.DeclaringType.DisplayName()}.{p.Name} is a time stored as UTC"));
    }

    private static IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IProperty> DateTimeProperties()
    {
        using var context = Context();
        return context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .ToList();
    }

    /// <summary>A context for its model only: it never connects.</summary>
    private static MedAppDbContext Context() =>
        new(new DbContextOptionsBuilder<MedAppDbContext>().UseSqlServer("Server=unused;Database=unused").Options);
}
