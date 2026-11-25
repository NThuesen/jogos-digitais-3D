using System;
using PocketPutt.Game;
using UnityEngine;

namespace PocketPutt.Ads
{
    public sealed class CupAdsHost : MonoBehaviour
    {
        public bool PrivacyOptionsRequired => false;
        public void ShowPrivacyOptions() { }

        public ICupInterstitial Provider { get; private set; }
        public CupInterstitialService Flow { get; private set; }
        bool audioSuspended, previousAudioPause, paused;
        public void Initialize(Action<int> navigate)
        {
            Provider = GameApp.CommandLineValue("-pp-ads-mock") == "1"
                ? (ICupInterstitial)new MockCupInterstitial { CompleteImmediately = true }
                : new NullCupInterstitial();
            Flow = new CupInterstitialService(Provider, SuspendAudio, navigate, Debug.LogException);
        }

        void SuspendAudio(bool suspend)
        {
            if (suspend && !audioSuspended)
            {
                previousAudioPause = AudioListener.pause;
                AudioListener.pause = true;
                audioSuspended = true;
            }
            else if (!suspend && audioSuspended)
            {
                AudioListener.pause = previousAudioPause;
                audioSuspended = false;
            }
        }

        void Update()
        {
            if (!paused) Flow?.Tick(Time.realtimeSinceStartupAsDouble);
        }

        void OnApplicationPause(bool value) { paused = value; }
        void OnDestroy()
        {
            Flow?.Cancel();
            Provider?.Dispose();
            SuspendAudio(false);
        }
    }
}
