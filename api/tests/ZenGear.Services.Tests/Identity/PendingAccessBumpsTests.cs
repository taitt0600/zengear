using Shouldly;
using ZenGear.Services.Identity.AccessCache;

namespace ZenGear.Services.Tests.Identity;

public class PendingAccessBumpsTests
{
    private static readonly AccessBump Bump = AccessBump.User(Guid.NewGuid());

    [Test]
    public void R_SEC_07_bump_stays_pending_until_applied()
    {
        var pending = new PendingAccessBumps();
        pending.Add([Bump]);

        var snapshot = pending.Snapshot();

        pending.HasAny.ShouldBeTrue();
        pending.Remove(snapshot);
        pending.HasAny.ShouldBeFalse();
    }

    [Test]
    public void R_SEC_07_bump_added_again_during_a_retry_stays_pending()
    {
        var pending = new PendingAccessBumps();
        pending.Add([Bump]);
        var inFlight = pending.Snapshot();

        pending.Add([Bump]); // a newer change, possibly committed after the in-flight INCR
        pending.Remove(inFlight);

        pending.HasAny.ShouldBeTrue();
    }
}
