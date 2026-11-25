using System;

namespace PocketPutt.Ads
{
    // All calls/callbacks run on Unity's main thread. Unavailable means skip, never wait for a load.
    public interface ICupInterstitial : IDisposable
    {
        bool CanShow { get; }
        void Show(Action finished);
        void Cancel();
    }

    /// <summary>One transient opportunity per trophy event. No persistent ad counters or delayed shows.</summary>
    public sealed class CupInterstitialService
    {
        readonly ICupInterstitial ad;
        readonly Action<bool> suspendAudio;
        readonly Action<int> navigate;
        readonly Action<Exception> report;
        int generation, destination;
        double deadline;
        public bool Pending { get; private set; }
        public bool Busy { get; private set; }
        public const double CallbackTimeoutSeconds = 120;

        public CupInterstitialService(ICupInterstitial ad, Action<bool> suspendAudio, Action<int> navigate,
            Action<Exception> report = null)
        {
            this.ad = ad; this.suspendAudio = suspendAudio; this.navigate = navigate; this.report = report;
        }

        public void TrophyWon(int cupIndex, int cupCount)
        {
            Cancel();
            destination = Math.Min(cupIndex + 1, cupCount - 1);
            Pending = true;
        }

        public void Continue(double now)
        {
            if (!Pending || Busy) return;
            Pending = false; // consume before any external call (including synchronous/reentrant callbacks)
            Busy = true;
            int ticket = ++generation;
            deadline = now + CallbackTimeoutSeconds;
            try
            {
                if (!ad.CanShow) { Finish(ticket); return; }
                suspendAudio(true);
                ad.Show(() => Finish(ticket));
            }
            catch (Exception e) { report?.Invoke(e); Finish(ticket); }
        }

        public void Tick(double now)
        {
            if (Busy && now >= deadline) Finish(generation);
        }

        void Finish(int ticket)
        {
            if (!Busy || ticket != generation) return;
            Busy = false;
            ++generation;
            try { ad.Cancel(); } catch (Exception e) { report?.Invoke(e); }
            suspendAudio(false);
            navigate(destination);
        }

        // Navigation outside the celebration, shutdown, and reset discard the opportunity.
        public void Cancel()
        {
            Pending = false;
            ++generation;
            if (!Busy) return;
            Busy = false;
            try { ad.Cancel(); } catch (Exception e) { report?.Invoke(e); }
            suspendAudio(false);
        }
    }

    public sealed class NullCupInterstitial : ICupInterstitial
    {
        public bool CanShow => false;
        public void Show(Action finished) => finished();
        public void Cancel() { }
        public void Dispose() { }
    }

    /// <summary>Callback-only mock. Never draws a fake ad. Explicitly enabled for desktop QA.</summary>
    public sealed class MockCupInterstitial : ICupInterstitial
    {
        public bool Ready = true;
        public bool Consent = true;
        public bool ThrowOnShow;
        public bool CompleteImmediately;
        public int Shows;
        public Action Completion;
        public bool CanShow => Ready && Consent;
        public void Show(Action finished)
        {
            Shows++;
            Ready = false;
            Completion = finished;
            if (ThrowOnShow) throw new InvalidOperationException("Mock show failure");
            if (CompleteImmediately) finished();
        }
        public void Cancel() { Ready = false; }
        public void Dispose() { Completion = null; }
    }
}
