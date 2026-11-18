using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Application.Referrals.Repositories;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>A referral repository with no referrals, for timeline tests about the other feeds.</summary>
public static class EmptyReferrals
{
    public static IReferralRepository Repository()
    {
        var repository = new Mock<IReferralRepository>();
        repository
            .Setup(r => r.GetPatientTimelineAsync(It.IsAny<GetPatientTimelineQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository
            .Setup(r => r.GetAwaitingTriageForPatientAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return repository.Object;
    }
}
