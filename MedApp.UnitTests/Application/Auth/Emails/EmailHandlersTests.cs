using FluentAssertions;
using MedApp.Application.Authentication.Emails.Commands.ChangeMyEmail;
using MedApp.Application.Authentication.Emails.Commands.ConfirmEmail;
using MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail;
using MedApp.Application.Authentication.Emails.Common;
using MedApp.Application.Authentication.Emails.Queries.GetMyEmail;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using Microsoft.AspNetCore.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.Emails;

[TestFixture]
public sealed class EmailHandlersTests
{
    private Mock<IAccountEmailService> _emails = null!;
    private Mock<IEmailSender> _sender = null!;
    private Mock<ISecurityNotices> _notices = null!;
    private Mock<ISecurityEventLog> _events = null!;
    private List<EmailMessage> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        _emails = new Mock<IAccountEmailService>();
        _sender = new Mock<IEmailSender>();
        _sender.SetupGet(s => s.CanSend).Returns(true);
        _notices = new Mock<ISecurityNotices>();
        _events = new Mock<ISecurityEventLog>();
        _sent = [];
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _sent.Add(m)).Returns(Task.CompletedTask);
    }

    private ChangeMyEmailHandler ChangeHandler(bool canSend = true) =>
        new(_emails.Object, Availability(canSend), _sender.Object, Links(), _events.Object);

    [Test]
    public async Task GetMyEmail_ReturnsTheAddresses()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(Address, OtherAddress));

        var result = await new GetMyEmailHandler(_emails.Object).Handle(new GetMyEmailQuery(UserId), CancellationToken.None);

        result.Should().Be(new EmailResponse(Address, OtherAddress));
    }

    [Test]
    public async Task GetMyEmail_NoAccount_IsNotFound()
    {
        var act = () => new GetMyEmailHandler(_emails.Object).Handle(new GetMyEmailQuery(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ChangeMyEmail_NewAddress_SendsItAConfirmationLinkAndRecordsIt()
    {
        _emails.Setup(e => e.RequestChangeAsync(UserId, OtherAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, OtherAddress, Token));
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(Address, OtherAddress));

        var result = await ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, $" {OtherAddress} "), CancellationToken.None);

        result.Should().Be(new EmailResponse(Address, OtherAddress));
        var email = _sent.Should().ContainSingle().Subject;
        email.To.Should().Be(OtherAddress);
        email.Subject.Should().Be(EmailTemplates.ConfirmEmailSubject);
        email.TextBody.Should().Contain($"{LinkBaseUrl}/confirm-email#user={UserId:D}&token={Uri.EscapeDataString(Token)}");
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.EmailChangeRequested), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ChangeMyEmail_TheConfirmedAddressAgain_SendsNothing()
    {
        _emails.Setup(e => e.RequestChangeAsync(UserId, Address, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, Address, null));
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        await ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, Address), CancellationToken.None);

        _sent.Should().BeEmpty();
        _events.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ChangeMyEmail_EmailCantBeSent_IsAConflictAndChangesNothing()
    {
        var act = () => ChangeHandler(canSend: false).Handle(new ChangeMyEmailCommand(UserId, OtherAddress), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _emails.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ChangeMyEmail_NoAccount_IsNotFound()
    {
        var act = () => ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, OtherAddress), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ChangeMyEmail_AccountGoneAfterwards_IsNotFound()
    {
        _emails.Setup(e => e.RequestChangeAsync(UserId, OtherAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, OtherAddress, Token));

        var act = () => ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, OtherAddress), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ConfirmEmail_ValidLink_RecordsItAndTellsTheOldAddress()
    {
        _emails.Setup(e => e.ConfirmAsync(UserId, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmation(IdentityResult.Success, Username, Address));

        var result = await new ConfirmEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new ConfirmEmailCommand(UserId, Token), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.EmailChanged, It.IsAny<CancellationToken>(), Address, null), Times.Once);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.EmailConfirmed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ConfirmEmail_FirstAddress_TellsNobody()
    {
        _emails.Setup(e => e.ConfirmAsync(UserId, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmation(IdentityResult.Success, Username));

        await new ConfirmEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new ConfirmEmailCommand(UserId, Token), CancellationToken.None);

        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ConfirmEmail_InvalidLink_ReturnsTheErrorAndRecordsNothing()
    {
        _emails.Setup(e => e.ConfirmAsync(UserId, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmation(IdentityResult.Failed(IdentityErrors.InvalidToken)));

        var result = await new ConfirmEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new ConfirmEmailCommand(UserId, Token), CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);
        _events.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveMyEmail_ConfirmedAddress_TellsItAndRecords()
    {
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        await new RemoveMyEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId), CancellationToken.None);

        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.EmailRemoved, It.IsAny<CancellationToken>(), Address, null), Times.Once);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.EmailRemoved), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveMyEmail_OnlyAPendingAddress_RecordsButTellsNobody()
    {
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null, OtherAddress));

        await new RemoveMyEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId), CancellationToken.None);

        _notices.VerifyNoOtherCalls();
        _events.Verify(e => e.RecordAsync(It.IsAny<SecurityEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveMyEmail_NothingToRemove_DoesNothing()
    {
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null));

        await new RemoveMyEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId), CancellationToken.None);

        _events.VerifyNoOtherCalls();
        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveMyEmail_NoAccount_IsNotFound()
    {
        var act = () => new RemoveMyEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void EmailMapper_ReturnsTheConfirmedAndPendingAddresses() =>
        EmailMapper.ToResponse(Account(Address, OtherAddress)).Should().Be(new EmailResponse(Address, OtherAddress));

    [TestCase(Address, true)]
    [TestCase("", false)]
    [TestCase("not-an-address", false)]
    public void ChangeMyEmailValidator_ChecksTheAddress(string email, bool valid) =>
        new ChangeMyEmailValidator().Validate(new ChangeMyEmailCommand(UserId, email)).IsValid.Should().Be(valid);

    [Test]
    public void ChangeMyEmailValidator_TooLong_Fails() =>
        new ChangeMyEmailValidator().Validate(new ChangeMyEmailCommand(UserId, new string('a', 250) + "@example.test"))
            .IsValid.Should().BeFalse();

    [Test]
    public void ConfirmEmailValidator_ChecksTheLink()
    {
        var validator = new ConfirmEmailValidator();

        validator.Validate(new ConfirmEmailCommand(UserId, Token)).IsValid.Should().BeTrue();
        validator.Validate(new ConfirmEmailCommand(Guid.Empty, Token)).IsValid.Should().BeFalse();
        validator.Validate(new ConfirmEmailCommand(UserId, "")).IsValid.Should().BeFalse();
        validator.Validate(new ConfirmEmailCommand(UserId, new string('a', 5000))).IsValid.Should().BeFalse();
        new ConfirmEmailCommand(UserId, Token).ToString().Should().NotContain(Token);
    }
}
