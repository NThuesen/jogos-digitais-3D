using System;
using UnityEngine;

namespace PocketPutt.Sim
{
    public enum Surface : byte { Grass = 0, Sand = 1, Ice = 2 }

    /// <summary>Deterministic ball state. Positions are on the XZ field plane (x, y => world x, z).</summary>
    public struct BallState
    {
        public Vector2 pos;
        public Vector2 vel;
        public float time;
        public bool sunk;
        public bool drowned;          // last motion ended in water (ball was returned to the tee); cleared by Launch
        public float Speed => vel.magnitude;
    }

    /// <summary>Events raised during a step, consumed by audio/VFX (never by gameplay rules).</summary>
    public struct StepEvents
    {
        public float wallImpact;      // strongest impact speed this step (0 = none)
        public Vector2 wallPoint;
        public bool sunk;
        public bool teleported;
        public int portalIndex;
        public bool water;            // the ball touched water this step and was returned to the tee
        public Vector2 waterPoint;    // ball centre at the moment of contact (before the return)
    }

    public static class Tuning
    {
        public const float BallRadius = 0.19f;
        public const float WallRadius = 0.10f;
        public const float HoleRadius = 0.30f;
        public const float HoleCaptureSpeed = 3.5f;   // at the exact centre
        public const float HoleRimCaptureSpeed = 1.3f; // at the rim
        public const float MaxLaunchSpeed = 8.0f;
        public const float MinLaunchSpeed = 0.55f;
        public const float StopSpeed = 0.07f;
        public const float WallRestitution = 0.78f;
        public const float WallTangentKeep = 0.985f;
        public const float StepDt = 1f / 120f;
        public const float MaxSimSeconds = 30f;

        public static float Decel(Surface s) => s == Surface.Sand ? 9.5f : s == Surface.Ice ? 0.75f : 3.2f;

        // Drag fraction (0..1) to launch speed. Progressive curve gives finer control at low power.
        public static float SpeedForPower(float power01)
        {
            float p = Mathf.Clamp01(power01);
            return MinLaunchSpeed + (MaxLaunchSpeed - MinLaunchSpeed) * Mathf.Pow(p, 1.35f);
        }
    }

    public static class BallSim
    {
        public static void Launch(ref BallState s, Vector2 dir, float power01)
        {
            if (dir.sqrMagnitude < 1e-8f) return;
            s.vel = dir.normalized * Tuning.SpeedForPower(power01);
            s.drowned = false;
        }

        /// <summary>
        /// Water contact: the stroke already taken stays counted (the caller owns the counter), the ball returns to
        /// the tee at rest and the motion ends. The simulation clock is kept so movers stay continuous.
        /// Contact is tested once per fixed step against the ball disc. A step moves at most
        /// MaxLaunchSpeed * StepDt (about 0.07), far less than the disc diameter (0.38), so no lake can be skipped
        /// and a swept test would add nothing.
        /// </summary>
        static bool DrownIfWet(ref BallState s, LevelData level, ref StepEvents ev)
        {
            if (!level.HasWater || !level.TouchesWater(s.pos)) return false;
            ev.water = true;
            ev.waterPoint = s.pos;
            ev.sunk = false;
            ev.teleported = false;
            s.pos = level.start;
            s.vel = Vector2.zero;
            s.drowned = true;
            return true;
        }

        /// <summary>Advance one fixed step. Returns true while the ball is still moving.</summary>
        public static bool Step(ref BallState s, LevelData level, ref StepEvents ev)
        {
            ev = default;
            if (s.sunk) return false;
            if (DrownIfWet(ref s, level, ref ev)) return false;   // already wet (injected state): no free tick
            float dt = Tuning.StepDt;
            float speed = s.vel.magnitude;
            if (speed > 0f)
            {
                Surface surf = level.SurfaceAt(s.pos);
                float ns = speed - Tuning.Decel(surf) * dt;
                if (ns < Tuning.StopSpeed) { s.vel = Vector2.zero; ns = 0f; }
                else s.vel *= ns / speed;
                speed = ns;
            }

            if (speed > 0f)
            {
                s.pos += s.vel * dt;
                s.time += dt;
                level.Collide(ref s, ref ev);
                if (DrownIfWet(ref s, level, ref ev)) return false;   // water beats portal and hole
                if (level.TryPortal(ref s, ref ev) && DrownIfWet(ref s, level, ref ev)) return false;
                Vector2 toHole = level.hole - s.pos;
                float hd = toHole.magnitude;
                if (hd <= Tuning.HoleRadius &&
                    s.vel.magnitude <= Mathf.Lerp(Tuning.HoleCaptureSpeed, Tuning.HoleRimCaptureSpeed, hd / Tuning.HoleRadius))
                {
                    s.sunk = true;
                    s.pos = level.hole;
                    s.vel = Vector2.zero;
                    ev.sunk = true;
                    return false;
                }
                return s.vel.sqrMagnitude > 0f;
            }

            // Resting: moving obstacles can still shove the ball.
            if (level.HasMovers)
            {
                s.time += dt;
                level.Collide(ref s, ref ev);
                if (DrownIfWet(ref s, level, ref ev)) return false;   // a mover shoved the ball into a lake
                return s.vel.sqrMagnitude > 0f;
            }
            return false;
        }

        /// <summary>Simulates until rest or sink. Returns number of steps taken.</summary>
        public static int RunToRest(ref BallState s, LevelData level, Action<Vector2> trace = null)
        {
            int maxSteps = Mathf.RoundToInt(Tuning.MaxSimSeconds / Tuning.StepDt);
            StepEvents ev = default;
            int n = 0;
            while (n < maxSteps)
            {
                n++;
                bool moving = Step(ref s, level, ref ev);
                trace?.Invoke(s.pos);
                if (!moving) break;
            }
            return n;
        }
    }
}
