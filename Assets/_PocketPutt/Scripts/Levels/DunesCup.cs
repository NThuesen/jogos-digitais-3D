using PocketPutt.Sim;

namespace PocketPutt.Levels
{
    /// <summary>
    /// Copa das Dunas: sand multiplies friction (a full-power shot dies in ~3.4 m of sand). Lessons: spend power to cross
    /// sand, use sand as a brake, or take the longer safe grass route.
    /// </summary>
    public static class DunesCup
    {
        public static LevelData[] Build() => new[]
        {
            L01(), L02(), L03(), L04(), L05(), L06(), L07(), L08(), L09(), L10()
        };

        // One band of sand across a short lane: more power, same aim (intro; a straight strong shot is the lesson).
        static LevelData L01() => new LevelBuilder("C2-01", "Primeira Duna|First Dune", "A areia freia a bola. Use mais força.|Sand slows the ball. Use more power.", Theme.Dunes, 1, 2)
            .Floor(-1.5f, -3.0f, 1.5f, 3.2f)
            .Zone(Surface.Sand, -1.5f, -0.4f, 1.5f, 0.4f)
            .Start(0f, -2.0f).Hole(0f, 2.0f).Done();

        // Sand pit hugging the left wall: one max-power shot across the sand is the ace, the grass strip on the right is the safe two-stroke way.
        static LevelData L02() => new LevelBuilder("C2-02", "Contorne ou Atravesse|Around or Through", "A grama da direita é mais longa, mas segura.|The grass on the right is longer but safe.", Theme.Dunes, 1, 2)
            .Floor(-2f, -3.6f, 2f, 3.6f)
            .Zone(Surface.Sand, -2f, -1.2f, 0.6f, 1.2f)
            .Start(-0.8f, -2.6f).Hole(-0.8f, 2.6f).Done();

        // The hole sits inside a sand brake behind a hedge: aim around the hedge, hit hard, the sand does the stopping.
        static LevelData L03() => new LevelBuilder("C2-03", "Freio de Areia|Sand Brake", "Dentro da areia a bola para rápido.|Inside the sand the ball stops quickly.", Theme.Dunes, 2, 3)
            .Floor(-1.8f, -3.4f, 1.8f, 3.4f)
            .Zone(Surface.Sand, -1.8f, 0.2f, 1.8f, 3.4f)
            .Post(0f, -0.9f, 0.34f)
            .Start(0f, -2.6f).Hole(0.6f, 1.9f).Done();

        // Zig-zag lane with two bunkers on the straight line: cut the corners through the sand or skirt them on the grass.
        static LevelData L04() => new LevelBuilder("C2-04", "Beira do Bunker|Bunker Rim", "Atravesse a areia ou contorne os bunkers.|Cut through the sand or skirt the bunkers.", Theme.Dunes, 2, 3)
            .Floor(-0.8f, -3.6f, 1.4f, 0.4f)
            .Floor(-1.4f, -0.4f, 0.8f, 3.6f)
            .Zone(Surface.Sand, 0.2f, -1.5f, 1.4f, -0.1f)
            .Zone(Surface.Sand, -1.4f, 1.1f, -0.2f, 2.5f)
            .Start(0.8f, -3.0f).Hole(-0.7f, 3.0f).Done();

        // A baffle forces a stop; stop on the grass before the sand, or use the grass gap on the right.
        static LevelData L05() => new LevelBuilder("C2-05", "Planeje a Parada|Plan the Stop", "Pare no gramado antes da areia.|Stop on the grass before the sand.", Theme.Dunes, 2, 3)
            .Floor(-2f, -3.8f, 2f, 3.8f)
            .Rail(-2f, -0.6f, -0.2f, -0.6f)
            .Zone(Surface.Sand, -2f, 0.4f, 1.0f, 1.6f)
            .Start(-1.2f, -3.0f).Hole(-1.0f, 3.0f).Done();

        // Sand lane with a hedge in the middle (two strokes, needs offset aim) versus the wide grass loop on the left (three, forgiving).
        static LevelData L06() => new LevelBuilder("C2-06", "Atalho na Areia|Sand Shortcut", "Atalho curto na areia ou volta longa no gramado.|Short cut in the sand or the long grass loop.", Theme.Dunes, 2, 3)
            .Floor(-2f, -3.6f, 2.4f, 3.6f)
            .BlockRect(-0.5f, -1.9f, 0.2f, 1.9f)
            .Zone(Surface.Sand, 0.2f, -1.9f, 2.4f, 1.9f)
            .Post(1.3f, 0f, 0.34f)
            .Start(1.3f, -3.0f).Hole(1.3f, 3.0f).Done();

        // Bank off the wall; the sand patch only punishes the narrow gap, the grass part of the gap is safe.
        static LevelData L07() => new LevelBuilder("C2-07", "Ricochete na Areia|Sandy Bank", "Quique na parede e evite a areia.|Bank off the wall and avoid the sand.", Theme.Dunes, 2, 3)
            .Floor(-1.8f, -3.4f, 1.8f, 3.4f)
            .Rail(-1.8f, 0.2f, 0.2f, 0.2f)
            .Zone(Surface.Sand, 0.2f, -0.6f, 1.0f, 1.0f)
            .Start(-0.9f, -2.6f).Hole(-0.4f, 2.6f).Done();

        // Sand sea with a hedged grass island: rest on the island, then hit again. A narrow grass trail on the left avoids the sand.
        static LevelData L08() => new LevelBuilder("C2-08", "Ilha de Grama|Grass Island", "Descanse na ilha de grama ou siga a trilha da esquerda.|Rest on the grass island or follow the left trail.", Theme.Dunes, 2, 3)
            .Floor(-2f, -3.8f, 2f, 3.8f)
            .Zone(Surface.Sand, -1.3f, -2.1f, 2f, -0.9f)
            .Zone(Surface.Sand, 0.9f, -0.9f, 2f, 0.9f)
            .Zone(Surface.Sand, -1.3f, 0.9f, 2f, 2.1f)
            .Post(0f, 0.1f, 0.34f)
            .Start(0f, -3.1f).Hole(0f, 3.0f).Done();

        // Hedge gate, baffle and a sand ford with a grass gap: the sand is two strokes but tight, the grass gap is three and forgiving.
        static LevelData L09() => new LevelBuilder("C2-09", "Mistura das Dunas|Dune Mix", "Areia, ângulo e economia de tacadas.|Sand, angle and economy.", Theme.Dunes, 2, 3)
            .Floor(-2f, -4.2f, 2f, 4.2f)
            .Post(-0.6f, -2.2f, 0.34f).Post(0.8f, -2.2f, 0.34f)
            .Rail(-2f, 0.1f, 0f, 0.1f)
            .Zone(Surface.Sand, -2f, 1.2f, 0.8f, 2.4f)
            .Start(-1.2f, -3.6f).Hole(-1.2f, 3.6f).Done();

        // Final: gate, sand ford with a grass lane, switchback past a hedge and a braking pit around the hole.
        static LevelData L10() => new LevelBuilder("C2-10", "Grande Final das Dunas|Dunes Grand Final", "Risco e recompensa na areia.|Risk and reward in the sand.", Theme.Dunes, 3, 4).Final()
            .Floor(-2f, -4.5f, 2f, 4.5f)
            .Rail(-2f, -2.9f, -0.6f, -2.9f).Rail(0.9f, -2.9f, 2f, -2.9f)
            .Zone(Surface.Sand, -2f, -1.8f, 0.4f, -0.7f)
            .Rail(2f, 0.3f, 0f, 0.3f)
            .Post(-1.0f, 1.7f, 0.34f)
            .Rail(-2f, 2.5f, 0.6f, 2.5f)
            .Zone(Surface.Sand, 0.3f, 3.0f, 2f, 4.5f)
            .Start(-1.3f, -4.0f).Hole(1.2f, 3.8f).Done();

    }
}
