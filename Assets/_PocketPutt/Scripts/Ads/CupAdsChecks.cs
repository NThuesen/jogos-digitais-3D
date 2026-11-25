using System;
using System.Collections.Generic;

namespace PocketPutt.Ads
{
    /// <summary>Fast deterministic checks, executable without Unity or an ad SDK.</summary>
    public static class CupAdsChecks
    {
        public static List<string> Run()
        {
            var failures = new List<string>();
            Action<bool, string> check = (ok, name) => { if (!ok) failures.Add(name); };
            foreach (int cup in new[] { 0, 4 })
            {
                var mock = new MockCupInterstitial();
                int navigations = 0, target = -1, audioChanges = 0;
                bool muted = false;
                var flow = new CupInterstitialService(mock, v => { muted = v; audioChanges++; },
                    i => { navigations++; target = i; });
                flow.Continue(0);
                check(mock.Shows == 0 && navigations == 0, "no trophy: no opportunity");
                flow.TrophyWon(cup, 5);
                flow.Tick(999);
                check(mock.Shows == 0 && navigations == 0, "celebration never triggers an ad by time");
                flow.Continue(1000); flow.Continue(1000);
                check(mock.Shows == 1 && muted && navigations == 0, "one show only after continue/back");
                mock.Completion(); mock.Completion(); flow.Continue(1001);
                check(navigations == 1 && target == Math.Min(cup + 1, 4) && !muted && audioChanges == 2,
                    "C1/C5 duplicate close/fail navigates/restores exactly once");
                flow.TrophyWon(cup, 5);
                mock.Ready = true;
                flow.Continue(1100);
                Action stale = mock.Completion;
                flow.Cancel();
                flow.TrophyWon(cup, 5); mock.Ready = true; flow.Continue(1200);
                stale();
                check(flow.Busy && navigations == 1 && muted, "stale callback cannot complete later event");
                flow.Tick(1320);
                mock.Completion();
                check(navigations == 2 && !muted && !flow.Busy, "missing callback watchdog and late callback");
            }
            foreach (string scenario in new[] { "offline", "denied", "throw", "sync" })
            {
                var mock = new MockCupInterstitial
                {
                    Ready = scenario != "offline", Consent = scenario != "denied",
                    ThrowOnShow = scenario == "throw", CompleteImmediately = scenario == "sync"
                };
                int navigations = 0;
                bool muted = false;
                var flow = new CupInterstitialService(mock, v => muted = v, i => navigations++);
                flow.TrophyWon(0, 5); flow.Continue(0);
                mock.Ready = mock.Consent = true;
                flow.Tick(999); flow.Continue(999);
                check(navigations == 1 && !muted && !flow.Pending && !flow.Busy, scenario + ": immediate, once, no delayed show");
                check(mock.Shows == (scenario == "offline" || scenario == "denied" ? 0 : 1), scenario + ": show count");
            }
            var nullFlow = new CupInterstitialService(new NullCupInterstitial(), v => { }, i => { });
            nullFlow.TrophyWon(0, 5); nullFlow.Continue(0);
            check(!nullFlow.Pending && !nullFlow.Busy, "Null provider never waits");
            return failures;
        }
    }
}
