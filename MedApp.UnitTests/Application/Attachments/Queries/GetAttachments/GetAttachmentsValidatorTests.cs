using FluentAssertions;
using MedApp.Application.Attachments.Queries.GetAttachments;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Attachments.Queries.GetAttachments;

[TestFixture]
public sealed class GetAttachmentsValidatorTests
{
    private readonly GetAttachmentsValidator _validator = new();

    [TestCase(true, false, false, true)]
    [TestCase(false, true, false, true)]
    [TestCase(false, false, true, true)]
    [TestCase(false, false, false, false)]
    [TestCase(true, false, true, false)]
    public void Validate_Targets_ExactlyOneIsValid(bool patient, bool pathway, bool stage, bool valid)
    {
        var result = _validator.Validate(new GetAttachmentsQuery(
            patient ? Guid.NewGuid() : null, pathway ? Guid.NewGuid() : null, stage ? Guid.NewGuid() : null,
            null,
            false, Staff.UserId, false));

        result.IsValid.Should().Be(valid);
        if (!valid)
            result.Errors.Should().ContainSingle(e => e.PropertyName == "Target");
    }
}
