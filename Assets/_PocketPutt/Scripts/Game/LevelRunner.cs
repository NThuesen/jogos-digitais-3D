using System;
using PocketPutt.Audio;
using PocketPutt.Presentation;
using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PocketPutt.Game
{
    /// <summary>
    /// Plays one course: slingshot input, fixed-step simulation, views, audio and fx.
    /// Raises HoleSunk after the ball has dropped; progression lives elsewhere.
    /// </summary>
    public sealed class LevelRunner : MonoBehaviour
    {
        public event Action<int> ShotsChanged;
        public event Action<int> HoleSunk;
        public event Action FirstDrag;

        public LevelData Level { get; private set; }
        public int Shots { get; private set; }
        public bool InputEnabled = true;
        public bool IsRolling => state.vel.sqrMagnitude > 0f;
        public bool Finished { get; private set; }
        public Vector2 BallPosition => state.pos;
        public float SimTime => state.time;
        /// <summary>Test hook: pins the simulation clock so a planned mover phase is reproduced exactly.</summary>
        public void TestSetTime(float t) { state.time = t; prev.time = t; }
        public float TopBlockFraction = 0.11f;

        /// <summary>Live gesture, for the on-screen guide: pressed anchor, current (smoothed) pointer and delivered power.</summary>
        public bool Dragging => dragging;
        public Vector2 DragAnchor => anchor;
        public Vector2 DragPointer => smooth;
        public float DragPower { get; private set; }
        public float DragFullPixels => GestureMath.FullDragPixels(Screen.width, Screen.height);
        /// <summary>True while the finger sits on the screen edge short of full power (the pull cannot go any further).</summary>
        public bool DragBlockedByEdge { get; private set; }
        /// <summary>Last shot actually fired (by gesture or test hook): field direction in degrees and power 0..1.</summary>
        public float LastShotAngle { get; private set; }
        public float LastShotPower { get; private set; }

        CameraRig rig;
        Fx fx;
        Sfx sfx;
        CourseView course;
        BallView ball;
        AimArrow arrow;

        BallState state, prev;
        Vector2 lastRest;
        float acc;
        bool dragging;
        Vector2 anchor, smooth;
        float sinkWait;
        bool sunkReported;
        bool dragAnnounced;

        public void Begin(LevelData level, CameraRig cameraRig, Fx effects, Sfx sound, float topInset, float bottomInset)
        {
            End();
            Level = level;
            rig = cameraRig;
            fx = effects;
            sfx = sound;
            StageRig.Apply(rig.Cam, level.theme);
            course = CourseView.Build(level, transform);
            ball = BallView.Create(transform);
            arrow = AimArrow.Create(transform);
            rig.Frame(level.bounds, topInset, bottomInset);
            ResetAttempt();
        }

        public void End()
        {
            if (course != null) Destroy(course.gameObject);
            if (ball != null) Destroy(ball.gameObject);
            if (arrow != null) Destroy(arrow.gameObject);
            course = null; ball = null; arrow = null;
            Level = null;
            dragging = false;
            Finished = false;
        }

        public void ResetAttempt()
        {
            Shots = 0;
            Finished = false;
            sunkReported = false;
            dragging = false;
            DragPower = 0f;
            DragBlockedByEdge = false;
            dragAnnounced = false;
            acc = 0f;
            state = new BallState { pos = Level.start };
            prev = state;
            lastRest = state.pos;
            ball.Teleport(state.pos);
            arrow.Hide();
            ShotsChanged?.Invoke(Shots);
        }

        // ---- input -----------------------------------------------------------------------------

        Vector2 ScreenToField(Vector2 screen)
        {
            Ray ray = rig.Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float d))
            {
                Vector3 p = ray.GetPoint(d);
                return new Vector2(p.x, p.z);
            }
            return state.pos;
        }

        void HandleInput(float dt)
        {
            if (Level == null) return;
            Pointer ptr = Pointer.current;
            bool canAim = InputEnabled && !Finished && !IsRolling;

            if (!canAim)
            {
                if (dragging && !InputEnabled) EndDrag();
                if (dragging && IsRolling) EndDrag();
                return;
            }
            if (ptr == null) return;
            Vector2 pos = ptr.position.ReadValue();

            if (!dragging)
            {
                if (ptr.press.wasPressedThisFrame && pos.y < Screen.height * (1f - TopBlockFraction))
                {
                    dragging = true;
                    anchor = pos;
                    smooth = pos;
                }
                return;
            }

            smooth = Vector2.Lerp(smooth, pos, 1f - Mathf.Exp(-dt * 32f));
            bool released = ptr.press.wasReleasedThisFrame || !ptr.press.isPressed;
            Vector2 effective = released ? pos : smooth;
            float power = GestureMath.Power(anchor, effective, Screen.width, Screen.height);
            DragPower = power;
            DragBlockedByEdge = !released && GestureMath.BlockedByEdge(pos, power, Screen.width, Screen.height, 3f);

            if (power > GestureMath.AnnouncePower && !dragAnnounced) { dragAnnounced = true; FirstDrag?.Invoke(); }

            if (released)
            {
                EndDrag();
                if (power >= GestureMath.MinPower)
                {
                    Vector2 dir = ScreenToField(anchor) - ScreenToField(pos);
                    Shoot(dir, power);
                }
                return;
            }

            if (power >= GestureMath.MinPower)
            {
                Vector2 dir = ScreenToField(anchor) - ScreenToField(smooth);
                arrow.Show(state.pos, dir, power);
            }
            else arrow.Hide();
        }

        void EndDrag()
        {
            dragging = false;
            DragPower = 0f;
            DragBlockedByEdge = false;
            arrow.Hide();
        }

        /// <summary>Fires a shot (also used by self-tests). Returns false if the ball cannot be hit right now.</summary>
        public bool Shoot(Vector2 dir, float power)
        {
            if (Finished || IsRolling || dir.sqrMagnitude < 1e-6f) return false;
            lastRest = state.pos;
            BallSim.Launch(ref state, dir, power);
            LastShotAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            LastShotPower = power;
            prev = state;
            Shots++;
            ShotsChanged?.Invoke(Shots);
            sfx?.Putt(power);
            fx?.Puff(state.pos, 3f + power * 5f, new Color(1f, 1f, 1f));
            rig.Kick(0.02f + power * 0.03f);
            return true;
        }

        // ---- simulation loop -------------------------------------------------------------------

        void Update()
        {
            if (Level == null) return;
            float dt = Time.deltaTime;
            HandleInput(dt);

            bool simulate = (IsRolling || Level.HasMovers) && !Finished;
            if (simulate)
            {
                acc += Mathf.Min(dt, 0.05f);
                int guard = 0;
                StepEvents ev = default;
                while (acc >= Tuning.StepDt && guard++ < 12)
                {
                    prev = state;
                    BallSim.Step(ref state, Level, ref ev);
                    acc -= Tuning.StepDt;
                    if (ev.wallImpact > 0f)
                    {
                        sfx?.Wall(ev.wallImpact);
                        if (ev.wallImpact > 1.2f)
                        {
                            fx?.Puff(ev.wallPoint, ev.wallImpact, new Color(1f, 1f, 0.95f));
                            if (ev.wallImpact > 4f) sfx?.Tick(12, 0.5f);
                        }
                    }
                    if (ev.water)
                    {
                        // The sim already put the ball back on the tee; the stroke stays counted (Shoot incremented it).
                        sfx?.Splash();
                        fx?.Splash(ev.waterPoint);
                        rig.Kick(0.015f);
                        prev = state;
                        lastRest = state.pos;
                        ball.Reappear(state.pos);
                    }
                    else if (ev.teleported)
                    {
                        sfx?.Whoosh();
                        fx?.Sparkle(state.pos, new Color(0.8f, 0.6f, 1f), 10);
                        prev = state; acc = 0f;
                    }
                    if (ev.sunk) { OnSunk(); break; }
                    if (!IsRolling && !Level.HasMovers) break;
                }
                if (!IsRolling && !Finished) lastRest = state.pos;
            }

            if (!Finished)
            {
                float a = IsRolling ? Mathf.Clamp01(acc / Tuning.StepDt) : 0f;
                Vector2 shown = Vector2.Lerp(prev.pos, state.pos, a);
                ball.Show(shown);
                course.UpdateFlag(shown, dt);
                SafetyNet();
            }
            else course.UpdateFlag(Level.hole, dt);

            if (Level.HasMovers) course.SyncMovers(state.time + acc);

            if (Finished && !sunkReported)
            {
                sinkWait += dt;
                if (ball.SinkDone && sinkWait > 0.9f)
                {
                    sunkReported = true;
                    HoleSunk?.Invoke(Shots);
                }
            }
        }

        void OnSunk()
        {
            Finished = true;
            sinkWait = 0f;
            arrow.Hide();
            ball.BeginSink(Level.hole);
            sfx?.Drop();
            fx?.Sparkle(Level.hole, new Color(1f, 0.9f, 0.4f), 18);
            if (Shots == 1) fx?.Confetti(Level.hole, 60);
            rig.Kick(0.04f);
        }

        void SafetyNet()
        {
            // The sim prevents leaving the course; this is only a recovery path for unexpected states.
            if (!Level.InsideFloor(state.pos) && !(Level.portals.Count > 0))
            {
                Debug.LogWarning("[PocketPutt] Ball left the course; recovering to last rest position.");
                state.pos = lastRest; state.vel = Vector2.zero; prev = state;
                ball.Teleport(state.pos);
            }
        }
    }
}
