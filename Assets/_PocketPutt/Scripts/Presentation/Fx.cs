using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>Small particle effects (dust puffs, sparkles, confetti), emitted from code.</summary>
    public sealed class Fx : MonoBehaviour
    {
        ParticleSystem puff, confetti, sparkle, splash;

        public static Fx Create(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<Fx>();
            fx.puff = fx.Make("Puff", 0.6f, 120, false, 0.0f);
            fx.sparkle = fx.Make("Sparkle", 0.9f, 120, true, 0f);
            fx.confetti = fx.Make("Confetti", 2.0f, 400, false, 1.8f);
            fx.splash = fx.Make("Splash", 0.8f, 160, false, 2.4f);
            return fx;
        }

        ParticleSystem Make(string name, float life, int max, bool additiveLook, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = life;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSize = 0.2f;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = ArtFactory.Cached("fx_" + name, () => ArtFactory.Particle(Color.white, name == "Confetti" ? ArtFactory.Plain() : ArtFactory.SoftDisc()));
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return ps;
        }

        public void Puff(Vector2 pos, float strength, Color tint)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(strength * 1.6f), 2, 9);
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                var p = new ParticleSystem.EmitParams
                {
                    position = new Vector3(pos.x, 0.18f, pos.y),
                    velocity = new Vector3(dir.x, 0.35f, dir.y) * Random.Range(0.2f, 0.7f) * (0.5f + strength * 0.12f),
                    startSize = Random.Range(0.12f, 0.24f),
                    startLifetime = Random.Range(0.3f, 0.55f),
                    startColor = new Color(tint.r, tint.g, tint.b, 0.55f)
                };
                puff.Emit(p, 1);
            }
        }

        /// <summary>Short droplet burst where the ball touched water. Cosmetic only: gameplay never waits for it.</summary>
        public void Splash(Vector2 pos)
        {
            for (int i = 0; i < 14; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                var p = new ParticleSystem.EmitParams
                {
                    position = new Vector3(pos.x, 0.08f, pos.y),
                    velocity = new Vector3(dir.x * 0.9f, Random.Range(1.6f, 2.8f), dir.y * 0.9f),
                    startSize = Random.Range(0.07f, 0.14f),
                    startLifetime = Random.Range(0.45f, 0.75f),
                    startColor = Color.Lerp(new Color(0.55f, 0.82f, 1f, 0.95f), Color.white, Random.value * 0.5f)
                };
                splash.Emit(p, 1);
            }
            Puff(pos, 2f, new Color(0.7f, 0.88f, 1f));
        }

        public void Sparkle(Vector2 pos, Color c, int n = 14)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                var p = new ParticleSystem.EmitParams
                {
                    position = new Vector3(pos.x, 0.35f, pos.y),
                    velocity = new Vector3(dir.x, Random.Range(0.4f, 1.1f), dir.y) * Random.Range(0.8f, 1.8f),
                    startSize = Random.Range(0.08f, 0.2f),
                    startLifetime = Random.Range(0.5f, 0.9f),
                    startColor = c
                };
                sparkle.Emit(p, 1);
            }
        }

        static readonly Color[] ConfettiColors =
        {
            new Color(1f, 0.82f, 0.25f), new Color(0.95f, 0.38f, 0.38f), new Color(0.4f, 0.8f, 0.5f),
            new Color(0.4f, 0.65f, 1f), new Color(0.9f, 0.55f, 0.95f), Color.white
        };

        public void Confetti(Vector2 pos, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                var p = new ParticleSystem.EmitParams
                {
                    position = new Vector3(pos.x, 0.4f, pos.y),
                    velocity = new Vector3(dir.x * 2.2f, Random.Range(2.5f, 5f), dir.y * 2.2f),
                    startSize = Random.Range(0.07f, 0.13f),
                    startLifetime = Random.Range(1.2f, 2.0f),
                    rotation = Random.Range(0f, 360f),
                    angularVelocity = Random.Range(-300f, 300f),
                    startColor = ConfettiColors[Random.Range(0, ConfettiColors.Length)]
                };
                confetti.Emit(p, 1);
            }
        }
    }
}
