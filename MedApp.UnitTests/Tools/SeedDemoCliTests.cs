using FluentAssertions;
using MedApp.DevMcp;
using MedApp.DevMcp.DemoData;

namespace MedApp.UnitTests.Tools;

/// <summary>
/// The terminal's way to load the demo data (<c>seed-demo</c>, which <c>tools/seed-demo-data.sh</c> runs): what its
/// arguments mean, and that nothing happens without <c>--confirm</c>.
/// </summary>
[TestFixture]
public sealed class SeedDemoCliTests
{
    [Test]
    public void Parse_NoArguments_IsNotConfirmed_WithTheStandardSeedAndASnapshot()
    {
        var arguments = SeedDemoArguments.Parse([], out var error);

        error.Should().BeNull();
        arguments.Should().BeEquivalentTo(new SeedDemoArguments(
            Confirm: false, Check: false, Snapshot: true, Seed: DemoDataReset.StandardSeed, KeepUsers: [], Out: null, SaveSnapshot: null, Help: false));
    }

    [Test]
    public void Parse_EveryOption_IsRead()
    {
        var arguments = SeedDemoArguments.Parse(
            ["--confirm", "--keep-users", "dev-parity, another", "--seed", "7", "--out", "summary.json", "--no-snapshot", "--save-snapshot", "kind-demo"],
            out var error);

        error.Should().BeNull();
        arguments.Should().BeEquivalentTo(new SeedDemoArguments(
            Confirm: true, Check: false, Snapshot: false, Seed: 7, KeepUsers: ["dev-parity", "another"], Out: "summary.json", SaveSnapshot: "kind-demo",
            Help: false));
    }

    [TestCase("--check")]
    [TestCase("--help")]
    [TestCase("-h")]
    public void Parse_CheckOrHelp_IsReadWithoutConfirming(string option)
    {
        var arguments = SeedDemoArguments.Parse([option], out var error);

        error.Should().BeNull();
        arguments!.Confirm.Should().BeFalse();
        (arguments.Check || arguments.Help).Should().BeTrue();
    }

    [TestCase(new[] { "--force" }, "Unknown argument '--force'.")]
    [TestCase(new[] { "confirm" }, "Unknown argument 'confirm'.")]
    [TestCase(new[] { "--seed", "many" }, "--seed needs a whole number.")]
    [TestCase(new[] { "--seed" }, "--seed needs a whole number.")]
    [TestCase(new[] { "--keep-users" }, "--keep-users needs a list of usernames, separated by commas.")]
    [TestCase(new[] { "--out" }, "--out needs a file path.")]
    [TestCase(new[] { "--save-snapshot" }, "--save-snapshot needs a name (letters, digits, - and _).")]
    public void Parse_AnythingItDoesNotKnow_IsRefusedWithWhy(string[] args, string expected)
    {
        var arguments = SeedDemoArguments.Parse(args, out var error);

        arguments.Should().BeNull();
        error.Should().Be(expected);
    }

    [Test]
    public async Task RunAsync_UnknownArgument_ChangesNothing_AndSaysHowToRunIt()
    {
        using var output = new StringWriter();
        using var log = new StringWriter();

        var exit = await SeedDemoCli.RunAsync(["--force"], output, log);

        exit.Should().Be(SeedDemoCli.Refused);
        output.ToString().Should().BeEmpty("stdout carries only the summary");
        log.ToString().Should().Contain("Unknown argument '--force'.").And.Contain("--confirm");
    }

    [Test]
    public async Task RunAsync_Help_PrintsTheUsage()
    {
        using var output = new StringWriter();
        using var log = new StringWriter();

        var exit = await SeedDemoCli.RunAsync(["--help"], output, log);

        exit.Should().Be(SeedDemoCli.Succeeded);
        output.ToString().Should().Contain("seed-demo --confirm").And.Contain("--keep-users").And.Contain("--no-snapshot");
    }
}
