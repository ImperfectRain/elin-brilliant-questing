using System;
using BepInEx.Logging;
using BrilliantQuesting.Plugin;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class NativeZoneVisitTests
    {
        [Fact]
        public void EachCompletedVisitNotifiesEvenWhenTheSameZoneReturnsWithoutActions()
        {
            var zone = new Zone();
            int visits = 0;
            try
            {
                NativeZoneVisit.Install(new ManualLogSource(), actual =>
                {
                    Assert.Same(zone, actual);
                    visits++;
                });
                NativeZoneVisit.AfterVisit(zone);
                NativeZoneVisit.AfterVisit(zone);
                Assert.Equal(2, visits);
                NativeZoneVisit.Uninstall();
                NativeZoneVisit.AfterVisit(zone);
                Assert.Equal(2, visits);
            }
            finally { NativeZoneVisit.Uninstall(); }
        }

        [Fact]
        public void FailedReadbackDoesNotEscapeIntoVanillaAndCanRetryNextVisit()
        {
            int visits = 0;
            try
            {
                NativeZoneVisit.Install(new ManualLogSource(), zone =>
                {
                    if (++visits == 1) throw new InvalidOperationException("unavailable");
                });
                NativeZoneVisit.AfterVisit(new Zone());
                NativeZoneVisit.AfterVisit(new Zone());
                Assert.Equal(2, visits);
            }
            finally { NativeZoneVisit.Uninstall(); }
        }
    }
}

// Signature double only. These tests do not establish Harmony timing or vanilla catch-up.
internal class Zone
{
    public void OnVisit() { }
}
