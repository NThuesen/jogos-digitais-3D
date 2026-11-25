using System.Collections.Generic;
using UnityEngine;
using PocketPutt.Sim;

namespace PocketPutt.Levels
{
    /// <summary>Tiny fluent helper so levels read like layouts, not boilerplate.</summary>
    public sealed class LevelBuilder
    {
        public readonly LevelData data = new LevelData();

        public LevelBuilder(string id, string title, string hint, Theme theme, int three, int two)
        {
            data.id = id; data.title = title; data.hint = hint; data.theme = theme;
            data.threeStar = three; data.twoStar = two;
        }

        public LevelBuilder Final() { data.isFinal = true; return this; }
        public LevelBuilder Start(float x, float z) { data.start = new Vector2(x, z); return this; }
        public LevelBuilder Hole(float x, float z) { data.hole = new Vector2(x, z); return this; }
        public LevelBuilder Floor(float x0, float z0, float x1, float z1) { data.rects.Add(new RectF(x0, z0, x1, z1)); return this; }

        /// <summary>Free-standing wall: x,z pairs.</summary>
        public LevelBuilder Rail(params float[] xz) { data.rails.Add(Pts(xz)); return this; }
        /// <summary>Solid island (hedge, pond, rock): polygon as x,z pairs (clockwise or not).</summary>
        public LevelBuilder Block(params float[] xz) { data.blocks.Add(Pts(xz)); return this; }
        public LevelBuilder BlockRect(float x0, float z0, float x1, float z1) { return Block(x0, z0, x1, z0, x1, z1, x0, z1); }
        public LevelBuilder Post(float x, float z, float radius = 0.32f)
        {
            data.posts.Add(new KeyValuePair<Vector2, float>(new Vector2(x, z), radius)); return this;
        }
        public LevelBuilder Zone(Surface surface, float x0, float z0, float x1, float z1)
        {
            data.zones.Add(new Zone { surface = surface, poly = new[] {
                new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1) } });
            return this;
        }
        public LevelBuilder ZonePoly(Surface surface, params float[] xz)
        {
            data.zones.Add(new Zone { surface = surface, poly = Pts(xz) }); return this;
        }
        /// <summary>Lake on the floor: touching it returns the ball to the tee (the stroke stays counted).</summary>
        public LevelBuilder Water(float x0, float z0, float x1, float z1) { data.water.Add(new RectF(x0, z0, x1, z1)); return this; }
        /// <summary>Tunnel: mouth at (fx,fz) opening towards (ox,oz); the ball re-emerges at (tx,tz) heading (dx,dz).</summary>
        public LevelBuilder Tunnel(float fx, float fz, float ox, float oz, float tx, float tz, float dx, float dz, float radius = 0.3f)
        {
            data.portals.Add(new Portal { from = new Vector2(fx, fz), entryOpen = new Vector2(ox, oz).normalized,
                to = new Vector2(tx, tz), exitDir = new Vector2(dx, dz).normalized, radius = radius });
            return this;
        }
        public LevelBuilder Spinner(float cx, float cz, float halfLength, float degPerSec, float startDeg = 0f, float radius = 0.12f)
        {
            data.movers.Add(new Obstacle
            {
                kind = ObstacleKind.Mover, motion = MotionKind.Spin,
                shape = new Capsule(new Vector2(cx - halfLength, cz), new Vector2(cx + halfLength, cz), radius),
                pivot = new Vector2(cx, cz), angularSpeed = degPerSec, phase = startDeg
            });
            return this;
        }
        public LevelBuilder Slider(float x0, float z0, float x1, float z1, float dx, float dz, float travel, float period, float phase = 0f, float radius = 0.14f)
        {
            data.movers.Add(new Obstacle
            {
                kind = ObstacleKind.Mover, motion = MotionKind.Slide,
                shape = new Capsule(new Vector2(x0, z0), new Vector2(x1, z1), radius),
                slideDir = new Vector2(dx, dz).normalized, slideTravel = travel, period = period, phase = phase
            });
            return this;
        }

        public LevelData Done() { data.Finish(); return data; }

        static Vector2[] Pts(float[] xz)
        {
            var p = new Vector2[xz.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xz[i * 2], xz[i * 2 + 1]);
            return p;
        }
    }
}
