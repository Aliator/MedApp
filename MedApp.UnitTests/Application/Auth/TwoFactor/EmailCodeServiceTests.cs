using System.Text.RegularExpressions;
using FluentAssertions;
using MedApp.Application.Authentication.TwoFactor.Common;
using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Application.Common.Email;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Security;
using MedApp.Domain.Authentication;
using Moq;
using static MedApp.UnitTests.Common.Constants.EmailTestConstants;

namespace MedApp.UnitTests.Application.Auth.TwoFactor;

/// <summary>
/// Codes by email: made from a cryptographic generator, stored only as a hash bound to their row, sent only to the
/// confirmed address, sent again at most once a minute and three times, a new code replacing the last, and each used once.
/// </summary>
[TestFixture]
public sealed partial class EmailCodeServiceTests
{
    private static readonly Guid ChallengeId = Guid.Parse("0000000f-0000-0000-0000-0000000000c1");

    private FakeEmailCodes _codes = null!;
    private Mock<IAccountEmailService> _emails = null!;
    private List<EmailMessage> _sent = null!;
    private Mock<IAccountAttemptLimiter> _attempts = null!;
    private MutableClock _clock = null!;
    private EmailCodeService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new MutableClock(NowUtc);
        _codes = new FakeEmailCodes();
        _emails = new Mock<IAccountEmailService>();
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(Address, pending: OtherAddress));
        _sent = [];
        var sender = new Mock<IEmailSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((message, _) => _sent.Add(message))
            .Returns(Task.CompletedTask);
        _attempts = new Mock<IAccountAttemptLimiter>();
        _service = new EmailCodeService(_codes, _emails.Object, sender.Object, _attempts.Object, _clock);
    }

    private string LastCode() => CodePattern().Match(_sent[^1].TextBody).Value;

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex CodePattern();

    // Making and storing them

    [Test]
    public void Generate_IsSixDigitsAndVaries()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => EmailCodes.Generate()).ToList();

        codes.Should().OnlyContain(code => code.Length == 6 && code.All(char.IsAsciiDigit));
        codes.Distinct().Count().Should().BeGreaterThan(190);
    }

    [Test]
    public void Hash_IsBoundToTheRowAndIgnoresSpaces()
    {
        var one = Guid.NewGuid();

        EmailCodes.Hash(one, "123456").Should().MatchRegex("^[0-9a-f]{64}$").And.NotContain("123456");
        EmailCodes.Hash(one, "123 456").Should().Be(EmailCodes.Hash(one, "123456"));
        EmailCodes.Hash(Guid.NewGuid(), "123456").Should().NotBe(EmailCodes.Hash(one, "123456"));
    }

    [TestCase("123456", true)]
    [TestCase(" 123 456 ", true)]
    [TestCase("12345", false)]
    [TestCase("1234567", false)]
    [TestCase("12345a", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsWellFormed_SixDigitsOnly(string? code, bool expected) =>
        EmailCodes.IsWellFormed(code).Should().Be(expected);

    [Test]
    public void Matches_OnlyTheCodeItHolds()
    {
        var stored = new EmailCode(UserId, EmailCodePurpose.TurnOn, null, NowUtc);
        stored.Issue(EmailCodes.Hash(stored.Id, "123456"));

        EmailCodes.Matches(stored, "123 456").Should().BeTrue();
        EmailCodes.Matches(stored, "123457").Should().BeFalse();
        EmailCodes.Matches(stored, "not a code").Should().BeFalse();
    }

    [Test]
    public void Entity_LivesTenMinutesAndCountsItsResends()
    {
        var code = new EmailCode(UserId, EmailCodePurpose.SignIn, ChallengeId, NowUtc);

        code.IsLive(NowUtc.AddMinutes(9)).Should().BeTrue();
        code.IsLive(NowUtc.AddMinutes(10)).Should().BeFalse();
        code.ResendAvailableAtUtc.Should().Be(NowUtc.AddMinutes(1));
        code.ResendsLeft.Should().Be(EmailCode.MaxResends);
    }

    // Sending

    [Test]
    public async Task Send_EmailsANewCodeToTheConfirmedAddressAndStoresOnlyItsHash()
    {
        var sent = await _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);

        sent.Should().Be(new EmailCodeSent(MaskedAddress, NowUtc.Add(EmailCode.Lifetime), NowUtc.AddMinutes(1), EmailCode.MaxResends));
        _sent.Should().ContainSingle().Which.To.Should().Be(Address);
        _sent[0].Category.Should().Be(EmailCategory.Code);
        var row = _codes.Rows.Should().ContainSingle().Subject;
        row.ChallengeId.Should().Be(ChallengeId);
        row.CodeHash.Should().Be(EmailCodes.Hash(row.Id, LastCode())).And.NotContain(LastCode());
        _attempts.Verify(a => a.Attempt(AccountAttempt.EmailCode, UserId), Times.Once);
    }

    [Test]
    public async Task Send_NoConfirmedAddress_SendsNothing()
    {
        // A pending address is never used.
        _emails.Setup(e => e.GetAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account(null, pending: OtherAddress));

        (await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None)).Should().BeNull();

        _sent.Should().BeEmpty();
        _codes.Rows.Should().BeEmpty();
        _attempts.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Send_AgainWithinAMinute_IsRefusedWithTheWait()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(20));

        var act = () => _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);

        (await act.Should().ThrowAsync<TooManyAttemptsException>()).Which.RetryAfterSeconds.Should().Be(40);
        _sent.Should().ContainSingle();
        _attempts.Verify(a => a.Attempt(AccountAttempt.EmailCode, UserId), Times.Once);
    }

    [Test]
    public async Task Send_AgainAfterAMinute_ReplacesTheCodeWithANewOne()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);
        var first = LastCode();
        _clock.Advance(TimeSpan.FromMinutes(1));

        var again = await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);

        again!.ResendsLeft.Should().Be(EmailCode.MaxResends - 1);
        again.ExpiresAtUtc.Should().Be(_clock.UtcNow.Add(EmailCode.Lifetime));
        _codes.Rows.Should().ContainSingle();
        _sent.Should().HaveCount(2);
        // The old code no longer works (unless the new one happens to be the same digits).
        if (LastCode() != first)
            (await _service.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, first, CancellationToken.None)).Should().BeFalse();
        (await _service.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, LastCode(), CancellationToken.None)).Should().BeTrue();
    }

    [Test]
    public async Task Send_AfterThreeResends_IsRefused()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);
        for (var i = 0; i < EmailCode.MaxResends; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);
        }

        _clock.Advance(TimeSpan.FromMinutes(1));
        var act = () => _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);

        (await act.Should().ThrowAsync<TooManyAttemptsException>()).Which.Message.Should().Contain("as many times as it can be");
        _sent.Should().HaveCount(1 + EmailCode.MaxResends);
    }

    [Test]
    public async Task Send_ResendLostToAnotherAtTheSameMoment_IsRefused()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _codes.RefuseResends = true;

        var act = () => _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);

        (await act.Should().ThrowAsync<TooManyAttemptsException>()).Which.RetryAfterSeconds.Should().Be(60);
        _sent.Should().ContainSingle();
    }

    [Test]
    public async Task Send_AfterTheCodeExpired_StartsANewOne()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.Confirm, null, CancellationToken.None);
        _clock.Advance(EmailCode.Lifetime);

        var sent = await _service.SendAsync(UserId, EmailCodePurpose.Confirm, null, CancellationToken.None);

        sent!.ResendsLeft.Should().Be(EmailCode.MaxResends);
        _codes.Rows.Should().ContainSingle();
    }

    [Test]
    public async Task Send_OverTheAccountsLimit_SendsNothing()
    {
        _attempts.Setup(a => a.Attempt(AccountAttempt.EmailCode, UserId)).Throws(new TooManyAttemptsException("Too many.", 300));

        var act = () => _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);

        await act.Should().ThrowAsync<TooManyAttemptsException>();
        _sent.Should().BeEmpty();
        _codes.Rows.Should().BeEmpty();
    }

    // Checking

    [Test]
    public async Task Check_RightCode_WorksOnce()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);
        var code = LastCode();

        (await _service.CheckAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, code, CancellationToken.None)).Should().BeTrue();
        (await _service.CheckAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, code, CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task Check_AnotherPurposeOrChallenge_IsWrong()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);
        var code = LastCode();

        (await _service.CheckAsync(UserId, EmailCodePurpose.Confirm, null, code, CancellationToken.None)).Should().BeFalse();
        (await _service.CheckAsync(UserId, EmailCodePurpose.SignIn, Guid.NewGuid(), code, CancellationToken.None)).Should().BeFalse();
        (await _service.CheckAsync(Guid.NewGuid(), EmailCodePurpose.SignIn, ChallengeId, code, CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task Check_Expired_IsWrong()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);
        _clock.Advance(EmailCode.Lifetime);

        (await _service.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, LastCode(), CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task Check_NotACode_TouchesNothing()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);

        (await _service.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, "ABCDE-23456", CancellationToken.None)).Should().BeFalse();

        _codes.Rows.Single().FailedAttempts.Should().Be(0);
    }

    [Test]
    public async Task Check_TurningOn_AllowsFiveTriesThenTheCodeStopsWorking()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.TurnOn, null, CancellationToken.None);
        var right = LastCode();
        var wrong = right == "000000" ? "111111" : "000000";

        for (var i = 0; i < EmailCode.MaxFailedAttempts; i++)
            (await _service.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, wrong, CancellationToken.None)).Should().BeFalse();

        (await _service.CheckAsync(UserId, EmailCodePurpose.TurnOn, null, right, CancellationToken.None)).Should().BeFalse();
    }

    [Test]
    public async Task Check_SignIn_LeavesAttemptsToTheChallenge()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, CancellationToken.None);
        var right = LastCode();
        var wrong = right == "000000" ? "111111" : "000000";

        for (var i = 0; i < EmailCode.MaxFailedAttempts + 2; i++)
            await _service.CheckAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, wrong, CancellationToken.None);

        _codes.Rows.Single().FailedAttempts.Should().Be(0);
        (await _service.CheckAsync(UserId, EmailCodePurpose.SignIn, ChallengeId, right, CancellationToken.None)).Should().BeTrue();
    }

    [Test]
    public async Task Check_RightCodeUsedByAnotherRequestFirst_IsWrong()
    {
        await _service.SendAsync(UserId, EmailCodePurpose.Confirm, null, CancellationToken.None);
        _codes.RefuseConsume = true;

        (await _service.CheckAsync(UserId, EmailCodePurpose.Confirm, null, LastCode(), CancellationToken.None)).Should().BeFalse();
    }

    // A test clock that can move on.
    private sealed class MutableClock(DateTime start) : TimeProvider
    {
        public DateTime UtcNow { get; private set; } = start;

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);

        public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
    }

    /// <summary>The repository's rules in memory, as its single statements apply them in SQL.</summary>
    private sealed class FakeEmailCodes : IEmailCodeRepository
    {
        public List<EmailCode> Rows { get; } = [];
        public bool RefuseResends { get; set; }
        public bool RefuseConsume { get; set; }

        public Task<EmailCode?> FindLiveAsync(Guid userId, EmailCodePurpose purpose, Guid? challengeId, DateTime utcNow, CancellationToken ct) =>
            Task.FromResult(Rows.Where(r => r.UserId == userId && r.Purpose == purpose && r.ChallengeId == challengeId && r.ExpiresAtUtc > utcNow)
                .OrderByDescending(r => r.SentAtUtc).FirstOrDefault());

        public Task AddAsync(EmailCode code, DateTime utcNow, CancellationToken ct)
        {
            Rows.RemoveAll(r => r.UserId == code.UserId && r.Purpose == code.Purpose || r.ExpiresAtUtc <= utcNow);
            Rows.Add(code);
            return Task.CompletedTask;
        }

        public Task<bool> TryResendAsync(Guid codeId, string codeHash, DateTime utcNow, CancellationToken ct)
        {
            var row = Rows.SingleOrDefault(r => r.Id == codeId);
            if (RefuseResends || row is null || row.ExpiresAtUtc <= utcNow || row.Resends >= EmailCode.MaxResends
                || row.SentAtUtc > utcNow - EmailCode.ResendInterval)
                return Task.FromResult(false);

            Set(row, nameof(EmailCode.CodeHash), codeHash);
            Set(row, nameof(EmailCode.SentAtUtc), utcNow);
            Set(row, nameof(EmailCode.ExpiresAtUtc), utcNow + EmailCode.Lifetime);
            Set(row, nameof(EmailCode.Resends), row.Resends + 1);
            return Task.FromResult(true);
        }

        public Task<bool> TryReserveAttemptAsync(Guid codeId, DateTime utcNow, CancellationToken ct)
        {
            var row = Rows.SingleOrDefault(r => r.Id == codeId);
            if (row is null || row.FailedAttempts >= EmailCode.MaxFailedAttempts || row.ExpiresAtUtc <= utcNow)
                return Task.FromResult(false);

            Set(row, nameof(EmailCode.FailedAttempts), row.FailedAttempts + 1);
            return Task.FromResult(true);
        }

        public Task<bool> ConsumeAsync(Guid codeId, string codeHash, CancellationToken ct) =>
            Task.FromResult(!RefuseConsume && Rows.RemoveAll(r => r.Id == codeId && r.CodeHash == codeHash) > 0);

        private static void Set(EmailCode row, string property, object value) =>
            typeof(EmailCode).GetProperty(property)!.SetValue(row, value);
    }
}
