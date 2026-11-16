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
    private Mock<IIdentityUserService> _users = null!;
    private Mock<ITwoFactorService> _twoFactor = null!;
    private Mock<IAccountAttemptLimiter> _attempts = null!;
    private List<EmailMessage> _sent = null!;

    [SetUp]
    public void SetUp()
    {
        _emails = new Mock<IAccountEmailService>();
        _sender = new Mock<IEmailSender>();
        _sender.SetupGet(s => s.CanSend).Returns(true);
        _notices = new Mock<ISecurityNotices>();
        _events = new Mock<ISecurityEventLog>();
        _users = new Mock<IIdentityUserService>();
        _users.Setup(u => u.VerifyPasswordAsync(Username, CurrentPassword, It.IsAny<CancellationToken>())).ReturnsAsync(IdentityResult.Success);
        _users.Setup(u => u.VerifyPasswordAsync(Username, It.Is<string>(p => p != CurrentPassword), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.PasswordIncorrect));
        _twoFactor = new Mock<ITwoFactorService>();
        _attempts = new Mock<IAccountAttemptLimiter>();
        _sent = [];
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _sent.Add(m)).Returns(Task.CompletedTask);
    }

    private ChangeMyEmailHandler ChangeHandler(bool canSend = true) =>
        new(_emails.Object, _users.Object, _twoFactor.Object, Availability(canSend), _sender.Object, Links(), _notices.Object, _events.Object, _attempts.Object);

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

        var result = await ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, $" {OtherAddress} ", CurrentPassword), CancellationToken.None);

        result.Should().Be(new EmailResponse(Address, OtherAddress));
        var email = _sent.Should().ContainSingle().Subject;
        email.To.Should().Be(OtherAddress);
        email.Subject.Should().Be(EmailTemplates.ConfirmAddressSubject);
        email.TextBody.Should().Contain($"{LinkBaseUrl}/confirm-email#user={UserId:D}&token={Uri.EscapeDataString(Token)}");
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.AddressChangeRequested), It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.AddressChangeRequested, It.IsAny<CancellationToken>(), Address, null), Times.Once);
        _attempts.Verify(a => a.Attempt(AccountAttempt.EmailChange, UserId), Times.Once);
    }

    [Test]
    public async Task ChangeMyEmail_TheConfirmedAddressAgain_SendsNothing()
    {
        _emails.Setup(e => e.RequestChangeAsync(UserId, Address, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, Address, null));
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        await ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, Address, CurrentPassword), CancellationToken.None);

        _sent.Should().BeEmpty();
        _events.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ChangeMyEmail_EmailCantBeSent_IsAConflictAndChangesNothing()
    {
        var act = () => ChangeHandler(canSend: false).Handle(new ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _emails.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ChangeMyEmail_NoAccount_IsNotFound()
    {
        var act = () => ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ChangeMyEmail_AccountGoneAfterwards_IsNotFound()
    {
        _emails.Setup(e => e.RequestChangeAsync(UserId, OtherAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, OtherAddress, Token));

        var act = () => ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ConfirmEmail_ValidLink_RecordsItAndTellsTheOldAddress()
    {
        _emails.Setup(e => e.ConfirmAsync(UserId, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmation(IdentityResult.Success, Username, Address));

        var result = await new ConfirmEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new ConfirmEmailCommand(UserId, Token, UserId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.AddressChanged, It.IsAny<CancellationToken>(), Address, null), Times.Once);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.AddressConfirmed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ConfirmEmail_FirstAddress_TellsNobody()
    {
        _emails.Setup(e => e.ConfirmAsync(UserId, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmation(IdentityResult.Success, Username));

        await new ConfirmEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new ConfirmEmailCommand(UserId, Token, UserId), CancellationToken.None);

        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ConfirmEmail_InvalidLink_ReturnsTheErrorAndRecordsNothing()
    {
        _emails.Setup(e => e.ConfirmAsync(UserId, Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailConfirmation(IdentityResult.Failed(IdentityErrors.InvalidToken)));

        var result = await new ConfirmEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new ConfirmEmailCommand(UserId, Token, UserId), CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.InvalidTokenCode);
        _events.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveMyEmail_ConfirmedAddress_TellsItAndRecords()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        await new RemoveMyEmailHandler(_emails.Object, _users.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId, CurrentPassword), CancellationToken.None);

        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.AddressRemoved, It.IsAny<CancellationToken>(), Address, null), Times.Once);
        _events.Verify(e => e.RecordAsync(It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.AddressRemoved), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveMyEmail_OnlyAPendingAddress_RecordsButTellsNobody()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null, OtherAddress));
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null, OtherAddress));

        await new RemoveMyEmailHandler(_emails.Object, _users.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId, CurrentPassword), CancellationToken.None);

        _notices.VerifyNoOtherCalls();
        _events.Verify(e => e.RecordAsync(It.IsAny<SecurityEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RemoveMyEmail_NothingToRemove_DoesNothing()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null));
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null));

        await new RemoveMyEmailHandler(_emails.Object, _users.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId, CurrentPassword), CancellationToken.None);

        _events.VerifyNoOtherCalls();
        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveMyEmail_NoAccount_IsNotFound()
    {
        var act = () => new RemoveMyEmailHandler(_emails.Object, _users.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void EmailMapper_ReturnsTheConfirmedAndPendingAddresses() =>
        EmailMapper.ToResponse(Account(Address, OtherAddress)).Should().Be(new EmailResponse(Address, OtherAddress));

    [TestCase(Address, true)]
    [TestCase("", false)]
    [TestCase("not-an-address", false)]
    public void ChangeMyEmailValidator_ChecksTheAddress(string email, bool valid) =>
        new ChangeMyEmailValidator().Validate(new ChangeMyEmailCommand(UserId, email, CurrentPassword)).IsValid.Should().Be(valid);

    [Test]
    public void ChangeMyEmailValidator_TooLong_Fails() =>
        new ChangeMyEmailValidator().Validate(new ChangeMyEmailCommand(UserId, new string('a', 250) + "@example.test", CurrentPassword))
            .IsValid.Should().BeFalse();

    [Test]
    public void ConfirmEmailValidator_ChecksTheLink()
    {
        var validator = new ConfirmEmailValidator();

        validator.Validate(new ConfirmEmailCommand(UserId, Token, UserId)).IsValid.Should().BeTrue();
        validator.Validate(new ConfirmEmailCommand(Guid.Empty, Token, Guid.Empty)).IsValid.Should().BeFalse();
        validator.Validate(new ConfirmEmailCommand(UserId, "", UserId)).IsValid.Should().BeFalse();
        validator.Validate(new ConfirmEmailCommand(UserId, new string('a', 5000), UserId)).IsValid.Should().BeFalse();
        new ConfirmEmailCommand(UserId, Token, UserId).ToString().Should().NotContain(Token);
    }

    [Test]
    public async Task ChangeMyEmail_WrongPassword_IsAValidationErrorAndChangesNothing()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        var act = () => ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, OtherAddress, "not-it"), CancellationToken.None);

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Password");
        _emails.Verify(e => e.RequestChangeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task ChangeMyEmail_OverTheLimit_ChangesNothing()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());
        _attempts.Setup(a => a.Attempt(AccountAttempt.EmailChange, UserId)).Throws(new TooManyAttemptsException("Too many.", 60));

        var act = () => ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, OtherAddress, CurrentPassword), CancellationToken.None);

        await act.Should().ThrowAsync<TooManyAttemptsException>();
        _users.Verify(u => u.VerifyPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ChangeMyEmail_DisplayName_StoresOnlyTheBareAddress()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null));
        _emails.Setup(e => e.RequestChangeAsync(UserId, OtherAddress, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, OtherAddress, Token));

        await ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, $"\"Payroll team\" <{OtherAddress}>", CurrentPassword), CancellationToken.None);

        _emails.Verify(e => e.RequestChangeAsync(UserId, OtherAddress, It.IsAny<CancellationToken>()), Times.Once);
        // No confirmed address before: nobody else to tell.
        _notices.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ChangeMyEmail_NotAnAddressAfterAll_IsAValidationError()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        var act = () => ChangeHandler().Handle(new ChangeMyEmailCommand(UserId, "not an address", CurrentPassword), CancellationToken.None);

        (await act.Should().ThrowAsync<FluentValidation.ValidationException>()).Which.Errors.Should().ContainSingle(e => e.PropertyName == "Email");
    }

    [Test]
    public async Task ConfirmEmail_SignedInAsAnotherAccount_IsWrongAccountAndConfirmsNothing()
    {
        var result = await new ConfirmEmailHandler(_emails.Object, _notices.Object, _events.Object)
            .Handle(new ConfirmEmailCommand(UserId, Token, ActorId), CancellationToken.None);

        result.Errors.Should().ContainSingle(e => e.Code == IdentityErrors.WrongAccountCode);
        _emails.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveMyEmail_WrongPassword_RemovesNothing()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        var act = () => new RemoveMyEmailHandler(_emails.Object, _users.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId, "not-it"), CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        _emails.Verify(e => e.RemoveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RemoveMyEmail_LockedByTheWrongPassword_IsLockedOut()
    {
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());
        _users.Setup(u => u.VerifyPasswordAsync(Username, "not-it", It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdentityResult.Failed(IdentityErrors.LockedOut));
        _twoFactor.Setup(t => t.LockoutMinutesLeftAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var act = () => new RemoveMyEmailHandler(_emails.Object, _users.Object, _twoFactor.Object, _notices.Object, _events.Object)
            .Handle(new RemoveMyEmailCommand(UserId, "not-it"), CancellationToken.None);

        await act.Should().ThrowAsync<AccountLockedOutException>();
    }

    [Test]
    public void RemoveMyEmailValidator_NeedsThePassword()
    {
        new MedApp.Application.Authentication.Emails.Commands.RemoveMyEmail.RemoveMyEmailValidator()
            .Validate(new RemoveMyEmailCommand(UserId, "")).IsValid.Should().BeFalse();
        new RemoveMyEmailCommand(UserId, CurrentPassword).ToString().Should().NotContain(CurrentPassword);
        new ChangeMyEmailCommand(UserId, Address, CurrentPassword).ToString().Should().NotContain(CurrentPassword).And.NotContain(Address);
    }

    [Test]
    public void ChangeMyEmailValidator_NeedsThePassword() =>
        new ChangeMyEmailValidator().Validate(new ChangeMyEmailCommand(UserId, Address, "")).IsValid.Should().BeFalse();

    [TestCase("name@example.test", "name@example.test")]
    [TestCase("  Name <name@example.test> ", "name@example.test")]
    [TestCase("a@b.test, c@d.test", null)]
    [TestCase("a@b.test\r\nBcc: c@d.test", null)]
    [TestCase("not an address", null)]
    [TestCase("", null)]
    public void Normalize_KeepsOnlyOneBareAddress(string value, string? expected) =>
        MedApp.Application.Authentication.Emails.Common.EmailAddressRules.Normalize(value).Should().Be(expected);

    // RemoveUserEmail (admin)

    private MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail.RemoveUserEmailHandler RemoveUserHandler(Mock<IIdentityReadService> read) =>
        new(read.Object, _emails.Object, _notices.Object, _events.Object);

    [Test]
    public async Task RemoveUserEmail_RemovesItRecordsTheAdminAndTellsTheAddress()
    {
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(new UserResponse(UserId, Username, []));
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account());

        var result = await RemoveUserHandler(read).Handle(
            new MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail.RemoveUserEmailCommand(Username, ActorId, "an.admin"),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _events.Verify(e => e.RecordAsync(
            It.Is<SecurityEvent>(ev => ev.Kind == SecurityEventKind.AddressRemoved && ev.ActorUserId == ActorId && ev.UserId == UserId),
            It.IsAny<CancellationToken>()), Times.Once);
        _notices.Verify(n => n.SendAsync(UserId, SecurityNotice.AddressRemoved, It.IsAny<CancellationToken>(), Address, null), Times.Once);
    }

    [Test]
    public async Task RemoveUserEmail_NothingThere_RecordsNothing()
    {
        var read = new Mock<IIdentityReadService>();
        read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(new UserResponse(UserId, Username, []));
        _emails.Setup(e => e.RemoveAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null));

        var result = await RemoveUserHandler(read).Handle(
            new MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail.RemoveUserEmailCommand(Username, ActorId, "an.admin"),
            CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _events.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RemoveUserEmail_UnknownOrGone_IsUserNotFound()
    {
        var read = new Mock<IIdentityReadService>();
        var command = new MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail.RemoveUserEmailCommand(Username, ActorId, "an.admin");

        (await RemoveUserHandler(read).Handle(command, CancellationToken.None)).Errors.Should().ContainSingle(e => e.Code == IdentityErrors.UserNotFoundCode);

        read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(new UserResponse(UserId, Username, []));
        (await RemoveUserHandler(read).Handle(command, CancellationToken.None)).Errors.Should().ContainSingle(e => e.Code == IdentityErrors.UserNotFoundCode);
    }

    [Test]
    public void RemoveUserEmailValidator_NeedsAUsername() =>
        new MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail.RemoveUserEmailValidator()
            .Validate(new MedApp.Application.Authentication.Emails.Commands.RemoveUserEmail.RemoveUserEmailCommand("", ActorId, "a")).IsValid.Should().BeFalse();
}
