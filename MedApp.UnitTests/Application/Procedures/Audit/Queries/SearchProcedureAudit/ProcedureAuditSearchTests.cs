using FluentAssertions;
using MedApp.Application.Procedures.Audit.Queries.SearchProcedureAudit;
using MedApp.Domain.Procedures.PatientProcedures;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;
using Action = MedApp.Domain.Procedures.PatientProcedures.PatientProcedureAuditAction;

namespace MedApp.UnitTests.Application.Procedures.Audit.Queries.SearchProcedureAudit;

[TestFixture]
public sealed class ProcedureAuditSearchTests
{
    private static readonly PatientProcedureAuditEntry Created =
        CreateAuditEntry(Action.Created, Now.AddDays(-3), username: "k.ashford", procedureTitle: "Knee replacement");
    private static readonly PatientProcedureAuditEntry Finished =
        CreateAuditEntry(Action.StageFinished, Now.AddDays(-1), stageName: "Consultation");
    private static readonly PatientProcedureAuditEntry Unlocked =
        CreateAuditEntry(Action.StageUnlocked, Now.AddHours(-1), username: "admin");

    private static IQueryable<PatientProcedureAuditEntry> Entries() =>
        new[] { Finished, Unlocked, Created }.AsQueryable();

    [Test]
    public void Filter_NoFilters_ReturnsEveryEntry()
    {
        Entries().Filter(CreateAuditSearchQuery()).Should().HaveCount(3);
    }

    [Test]
    public void Filter_Action_ReturnsThatActionIgnoringCase()
    {
        Entries().Filter(CreateAuditSearchQuery() with { Action = "stagefinished" }).Should().Equal(Finished);
    }

    [TestCase("ASHFORD")]
    [TestCase("knee")]
    public void Filter_Search_MatchesUsernameOrProcedureTitle(string search)
    {
        Entries().Filter(CreateAuditSearchQuery() with { Search = search }).Should().Equal(Created);
    }

    [Test]
    public void Filter_Search_MatchesStageName()
    {
        Entries().Filter(CreateAuditSearchQuery() with { Search = "consult" }).Should().Equal(Finished);
    }

    [Test]
    public void Filter_DateRange_IncludesWholeUkDays()
    {
        var query = CreateAuditSearchQuery() with { From = Today.AddDays(-1), To = Today.AddDays(-1) };

        Entries().Filter(query).Should().Equal(Finished);
    }

    [Test]
    public void Filter_Procedure_ReturnsItsEntries()
    {
        Entries().Filter(CreateAuditSearchQuery() with { ProcedureId = Unlocked.PatientProcedureId }).Should().Equal(Unlocked);
    }

    [Test]
    public void Sort_Default_IsNewestFirst()
    {
        Entries().Sort(false).Should().Equal(Unlocked, Finished, Created);
        Entries().Sort(true).Should().Equal(Created, Finished, Unlocked);
    }
}
