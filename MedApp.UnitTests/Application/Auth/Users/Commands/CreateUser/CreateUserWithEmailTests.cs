using FluentAssertions;
using MedApp.Application.Authentication.Users.Commands.CreateUser;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Contracts.Authentication.Responses;
using Microsoft.AspNetCore.Identity;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.Users.Commands.CreateUser;

/// <summary>Creating an account with an email address: the address is sent a confirmation link, never the password.</summary>
[TestFixture]
public sealed class CreateUserWithEmailTests
{
    private Mock<IIdentityUserService> _users = null!;
    private Mock<IIdentityReadService> _read = null!;
    private Mock<IAccountEmailService> _emails = null!;
    private Mock<IEmailSender> _sender = null!;
    private List<EmailMessage> _sent = null!;
    private CreateUserHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new Mock<IIdentityUserService>();
        _users.Setup(u => u.CreateUserAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(CreateUserResult.Created(NewPassword));
        _read = new Mock<IIdentityReadService>();
        _read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(new UserResponse(UserId, Username, []));
        _emails = new Mock<IAccountEmailService>();
        _emails.Setup(e => e.RequestChangeAsync(UserId, Address, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailChangeRequest(Username, Address, Token));
        _sender = new Mock<IEmailSender>();
        _sent = [];
        _sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, _) => _sent.Add(m)).Returns(Task.CompletedTask);
        _handler = new CreateUserHandler(_users.Object, new MedApp.UnitTests.Common.Fixtures.RecordingActivityLog(), _read.Object, _emails.Object, _sender.Object, Links(), Mock.Of<ISecurityEventLog>());
    }

    [Test]
    public async Task Handle_WithAnAddress_SendsAConfirmationLinkWithoutThePassword()
    {
        var result = await _handler.Handle(new CreateUserCommand(Username, $" {Address} "), CancellationToken.None);

        result.GeneratedPassword.Should().Be(NewPassword);
        var email = _sent.Should().ContainSingle().Subject;
        email.To.Should().Be(Address);
        email.Subject.Should().Be(EmailTemplates.ConfirmEmailSubject);
        email.HtmlBody.Should().NotContain(NewPassword);
        email.TextBody.Should().NotContain(NewPassword);
    }

    [Test]
    public async Task Handle_WithoutAnAddress_SendsNothing()
    {
        await _handler.Handle(new CreateUserCommand(Username), CancellationToken.None);

        _sent.Should().BeEmpty();
        _emails.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Handle_CreationFails_SendsNothing()
    {
        _users.Setup(u => u.CreateUserAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateUserResult.Failed(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName" })));

        var result = await _handler.Handle(new CreateUserCommand(Username, Address), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        _sent.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_AccountNotFoundAfterwards_SendsNothing()
    {
        _read.Setup(r => r.GetUserAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync((UserResponse?)null);

        var result = await _handler.Handle(new CreateUserCommand(Username, Address), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _sent.Should().BeEmpty();
    }

    [Test]
    public void Validator_AddressWhereEmailCantBeSent_Fails() =>
        new CreateUserValidator(Availability(canSend: false)).Validate(new CreateUserCommand(Username, Address))
            .Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateUserCommand.Email));

    [Test]
    public void Validator_NoAddressWhereEmailCantBeSent_Passes() =>
        new CreateUserValidator(Availability(canSend: false)).Validate(new CreateUserCommand(Username)).IsValid.Should().BeTrue();

    [Test]
    public void Validator_NotAnAddress_Fails() =>
        new CreateUserValidator(Availability(canSend: true)).Validate(new CreateUserCommand(Username, "not-an-address"))
            .Errors.Should().ContainSingle(e => e.PropertyName == nameof(CreateUserCommand.Email));
}
