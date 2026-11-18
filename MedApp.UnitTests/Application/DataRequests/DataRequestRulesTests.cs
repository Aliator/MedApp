using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.DataRequests.Common;
using MedApp.Domain.DataRequests;
using MedApp.Domain.Notifications;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.DataRequestTestConstants;

namespace MedApp.UnitTests.Application.DataRequests;

/// <summary>The data request's rules, its mapper and its notification.</summary>
[TestFixture]
public sealed class DataRequestRulesTests
{
    [TestCase("2026-01-31", 0, "2026-02-28")]
    [TestCase("2028-01-31", 0, "2028-02-29")]
    [TestCase("2026-03-15", 0, "2026-04-15")]
    [TestCase("2026-01-31", 2, "2026-04-30")]
    [TestCase("2026-12-31", 1, "2027-02-28")]
    public void DueOn_OneCalendarMonthPlusTheExtension_TheLastDayWhenThereIsNoMatchingDay(string received, int months, string due)
    {
        DataRequestRules.DueOn(DateOnly.Parse(received), months).Should().Be(DateOnly.Parse(due));
    }

    [Test]
    public void WhyNoPack_TheIdentityCheckThenTheReview()
    {
        var request = Open();
        DataRequestRules.WhyNoPack(request).Should().Contain("identity check");

        request.IdentityCheckedOn = Today;
        DataRequestRules.WhyNoPack(request).Should().Contain("clinician must review");

        request.ReviewedAtUtc = PathwaysTestConstants.Now;
        DataRequestRules.WhyNoPack(request).Should().BeNull();

        request.Status = DataRequestStatus.Sent;
        DataRequestRules.WhyNoPack(request).Should().Be(DataRequestRules.NotOpenMessage);
    }

    [Test]
    public void WhyCantChange_ARestrictedRecordCanChange_AnAnonymisedOneCant()
    {
        DataRequestRules.WhyCantChange(Open(PrivacyTestConstants.CreateRestrictedPatient())).Should().BeNull();
        DataRequestRules.WhyCantChange(Open(PrivacyTestConstants.CreateAnonymisedPatient()))
            .Should().Be(DataRequestRules.AnonymisedMessage);
    }

    [Test]
    public void EnsureCanChange_AStaleVersion_IsAConflict()
    {
        var act = () => DataRequestRules.EnsureCanChange(Open(), expectedVersion: 7);
        act.Should().Throw<ConflictException>().WithMessage(DataRequestRules.ChangedMeanwhileMessage);
    }

    [Test]
    public void WhyCantExtend_OnlyWithinTheFirstMonth()
    {
        var request = Open();
        DataRequestRules.WhyCantExtend(request, Today.AddMonths(1)).Should().BeNull();
        DataRequestRules.WhyCantExtend(request, Today.AddMonths(1).AddDays(1)).Should().NotBeNull();
    }

    [Test]
    public void WhyReceivedOnInvalid_NotInTheFuture_NorOverAYearBack()
    {
        DataRequestRules.WhyReceivedOnInvalid(Today, Today).Should().BeNull();
        DataRequestRules.WhyReceivedOnInvalid(Today.AddDays(1), Today).Should().NotBeNull();
        DataRequestRules.WhyReceivedOnInvalid(Today.AddDays(-DataRequestRules.MaxDaysBack - 1), Today).Should().NotBeNull();
    }

    [Test]
    public void CanRead_ManagePrivacyOrSignOffResults_NotOtherStaff()
    {
        DataRequestRules.CanRead(Admin).Should().BeTrue();
        DataRequestRules.CanRead(Clinician).Should().BeTrue();
        DataRequestRules.CanRead(Clerk).Should().BeFalse();
        var act = () => DataRequestRules.EnsureCanRead(Clerk);
        act.Should().Throw<ForbiddenException>();
    }

    [Test]
    public void ToResponse_WhatEachMayDo()
    {
        var request = Open();

        var forAdmin = DataRequestMapper.ToResponse(request, Admin, Today);
        forAdmin.CanManage.Should().BeTrue();
        forAdmin.CanReview.Should().BeFalse();
        forAdmin.CanMakePack.Should().BeFalse();
        forAdmin.PackBlockedReason.Should().Contain("identity check");
        forAdmin.FirstDueOn.Should().Be(Today.AddMonths(1));
        forAdmin.RequesterName.Should().Be(RequesterName);
        forAdmin.Status.Should().Be("Open");

        var forClinician = DataRequestMapper.ToResponse(request, Clinician, Today);
        forClinician.CanManage.Should().BeFalse();
        forClinician.CanReview.Should().BeTrue();
        forClinician.PackBlockedReason.Should().BeNull();

        DataRequestMapper.ToResponse(Ready(), Admin, Today).CanMakePack.Should().BeTrue();
    }

    [Test]
    public void ToSummary_OverdueWhenOpenAndPastItsDueDate()
    {
        var request = Open();
        DataRequestMapper.ToSummary(request, request.DueOn).IsOverdue.Should().BeFalse();
        DataRequestMapper.ToSummary(request, request.DueOn.AddDays(1)).IsOverdue.Should().BeTrue();
        request.Status = DataRequestStatus.Sent;
        DataRequestMapper.ToSummary(request, request.DueOn.AddDays(1)).IsOverdue.Should().BeFalse();
    }

    [Test]
    public void DueSoon_AFixedTitle_TheDueDateOnly_NeverThePatientOrRequester()
    {
        var request = Open();
        var users = new[] { Admin.UserId, Admin.UserId, Guid.NewGuid() };

        var sent = DataRequestNotifications.DueSoon(users, request, PathwaysTestConstants.Now);

        sent.Should().HaveCount(2);
        sent.Should().OnlyContain(n => n.Kind == NotificationKind.DataRequestDueSoon
                                       && n.Title == DataRequestNotifications.DueSoonTitle
                                       && n.SubjectId == request.Id
                                       && !n.Message.Contains(request.Patient.LastName)
                                       && !n.Message.Contains(RequesterName));
        Notification.HasFixedTitle(NotificationKind.DataRequestDueSoon).Should().BeTrue();
        ((int)NotificationKind.DataRequestDueSoon).Should().Be(42);
    }

    [Test]
    public void StoredNumbers_NeverRenumbered()
    {
        ((int)DataRequestStatus.Withdrawn).Should().Be(3);
        ((int)IdentityCheckMethod.Other).Should().Be(3);
        ((int)DataRequestSendMethod.Other).Should().Be(4);
        ((int)DataRequestItemKind.Referral).Should().Be(5);
        ((int)MedApp.Domain.Activity.ActivityAction.DataRequestPackDownloaded).Should().Be(50);
        ((int)MedApp.Domain.Activity.ActivityAction.DataRequestChanged).Should().Be(53);
    }
}
