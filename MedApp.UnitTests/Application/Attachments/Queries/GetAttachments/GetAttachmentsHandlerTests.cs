using AutoFixture;
using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Attachments.Queries.GetAttachments;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.AttachmentsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Queries.GetAttachments;

[TestFixture]
public sealed class GetAttachmentsHandlerTests : AttachmentHandlerTestBase
{
    private GetAttachmentsHandler Handler => Fixture.Create<GetAttachmentsHandler>();

    [Test]
    public async Task Handle_KnownPatient_ReturnsTheRepositorysRowsForTheViewer()
    {
        var patient = GivenPatient();
        var query = new GetAttachmentsQuery(patient.Id, null, null, null, false, Staff.UserId, false);
        var own = CreateAttachment(uploadedBy: Staff.UserId);
        var others = CreateAttachment(uploadedBy: OtherUserId);
        using var cts = new CancellationTokenSource();
        Attachments.Setup(a => a.ListAsync(query, cts.Token))
            .ReturnsAsync(([new AttachmentRow(own, null, null), new AttachmentRow(others, null, null)], 7));

        var response = await Handler.Handle(query, cts.Token);

        response.TotalCount.Should().Be(7);
        response.Items.Select(i => (i.Id, i.CanDelete)).Should().Equal((own.Id, true), (others.Id, false));
    }

    [Test]
    public async Task Handle_KnownStage_ListsIt()
    {
        var pathway = CreatePathway();
        var stage = GivenStage(pathway, 1);
        var query = new GetAttachmentsQuery(null, null, stage.Id, null, false, Staff.UserId, false);
        Attachments.Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(([], 0));

        var response = await Handler.Handle(query, CancellationToken.None);

        response.Items.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_UnknownPathway_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new GetAttachmentsQuery(null, Guid.NewGuid(), null, null, false, Staff.UserId, false), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        Attachments.Verify(a => a.ListAsync(It.IsAny<GetAttachmentsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
