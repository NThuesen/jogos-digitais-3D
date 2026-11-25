using PocketPutt.Sim;

namespace PocketPutt.Levels
{
    /// <summary>
    /// Copa dos Mestres: no new rules, only combinations of grass, sand, ice, tunnels and movers, with a safe line
    /// and an expert line on most courses.
    /// </summary>
    public static class MastersCup
    {
        public static LevelData[] Build() => new[]
        {
            L01(), L02(), L03(), L04(), L05(), L06(), L07(), L08(), L09(), L10()
        };

        // Warm-up: a narrow shortcut or the wide safe gap, now with a sand pad behind the baffle.
        static LevelData L01() => new LevelBuilder("C5-01", "Aquecimento|Warm-Up", "Duas rotas, a mesma bola.|Two routes, same ball.", Theme.Arena, 1, 2)
            .Floor(-2f, -3.6f, 2f, 3.6f)
            .Rail(-2f, 0f, -0.4f, 0f).Rail(0.4f, 0f, 1.0f, 0f)
            .Zone(Surface.Sand, -2f, 1.4f, 2f, 3.6f)
            .Start(0f, -2.7f).Hole(0f, 2.3f).Done();

        // Sand next to a banking wall.
        static LevelData L02() => new LevelBuilder("C5-02", "Areia e Ricochete|Sand and Bank", "Rebata sem cair na areia.|Bank it without hitting the sand.", Theme.Arena, 2, 3)
            .Floor(-1.9f, -3.6f, 1.9f, 3.6f)
            .Rail(-1.9f, 0f, 0.2f, 0f)
            .Zone(Surface.Sand, 0.2f, -0.8f, 1.9f, 0.8f)
            .Start(-1.0f, -2.8f).Hole(0.2f, 2.7f).Done();

        // Two parallel lanes to the same green: one sand, one ice. The ace needs the right lane and power; two strokes are the safe way.
        static LevelData L03() => new LevelBuilder("C5-03", "Gelo ou Areia|Ice or Sand", "Compare as velocidades.|Compare the speeds.", Theme.Arena, 1, 2)
            .Floor(-2.2f, -3.8f, 2.2f, 3.8f)
            .Rail(0f, -2.4f, 0f, 0.6f)
            .Zone(Surface.Sand, -2.2f, -2.4f, 0f, 0.6f)
            .Zone(Surface.Ice, 0f, -2.4f, 2.2f, 0.6f)
            .Start(0f, -3.1f).Hole(0f, 3.0f).Done();

        // Two tunnels, two destinations.
        static LevelData L04() => new LevelBuilder("C5-04", "Dois Túneis|Two Tunnels", "Escolha o túnel certo.|Pick the right tunnel.", Theme.Arena, 2, 3)
            .Floor(-2f, -3.8f, 2f, 3.8f)
            .Rail(-2f, 0f, 2f, 0f)
            .Tunnel(-1.4f, -1.0f, 0f, -1f, 1.4f, 1.0f, -0.5f, 0.87f)
            .Tunnel(1.4f, -1.0f, 0f, -1f, -1.4f, 1.0f, 0.2f, 0.98f)
            .Start(0f, -3.0f).Hole(1.0f, 3.0f).Done();

        // A slider door and a rotor, with two clear opportunities per cycle.
        static LevelData L05() => new LevelBuilder("C5-05", "Duas Janelas|Two Windows", "Duas oportunidades por ciclo.|Two chances per cycle.", Theme.Arena, 2, 3)
            .Floor(-2f, -3.8f, 2f, 3.8f)
            .Slider(-2f, -0.4f, -0.2f, -0.4f, 1f, 0f, 1.5f, 6f)
            .Spinner(0.9f, 1.8f, 0.75f, 60f, 0f, 0.12f)
            .Start(0f, -3.0f).Hole(0f, 3.1f).Done();

        // Ice, then a baffle: plan each stopping point.
        static LevelData L06() => new LevelBuilder("C5-06", "Posições de Parada|Resting Spots", "Planeje onde parar.|Plan where to stop.", Theme.Arena, 2, 3)
            .Floor(-2f, -4.2f, 2f, 4.2f)
            .Zone(Surface.Ice, -2f, -4.2f, 2f, -1.4f)
            .Rail(-2f, -1.4f, 0.8f, -1.4f)
            .Rail(2f, 1.4f, -0.8f, 1.4f)
            .Start(-1.2f, -3.6f).Hole(1.2f, 3.6f).Done();

        // Short and risky over sand and a rotor behind a hedge, or long and safe along the left lane (the safe route earns 3 stars).
        static LevelData L07() => new LevelBuilder("C5-07", "Curto e Arriscado|Short and Risky", "Atalho arriscado ou rota segura.|Risky shortcut or safe route.", Theme.Arena, 3, 4)
            .Floor(-2f, -3.8f, 2f, 3.8f)
            .BlockRect(-1.0f, -1.6f, 0.2f, 1.6f)
            .Zone(Surface.Sand, 0.2f, -1.2f, 2f, 1.2f)
            .Spinner(1.1f, 0f, 0.7f, 72f, 0f, 0.12f)
            .Post(1.2f, -2.0f, 0.34f)
            .Start(1.2f, -3.0f).Hole(1.2f, 3.0f).Done();

        // Ice yard with a lake in front of the tunnel line, a full baffle and a rotor chamber: surfaces, water and timing together.
        static LevelData L08() => new LevelBuilder("C5-08", "Mistura Total|Full Mix", "Superfícies e tempo juntos.|Surfaces and timing together.", Theme.Arena, 2, 3)
            .Floor(-2f, -4.0f, 2f, 4.6f)
            .Zone(Surface.Ice, -2f, -4.0f, 2f, -1.2f)
            .Rail(-2f, 0f, 2f, 0f)
            .Water(-0.6f, -3.0f, 0.3f, -2.0f)
            .Tunnel(-1.4f, -0.9f, 0f, -1f, -1.4f, 0.9f, 0f, 1f)
            .Spinner(-0.7f, 2.0f, 0.8f, 60f, 0f, 0.12f)
            .Start(0.8f, -3.4f).Hole(1.0f, 3.5f).Done();

        // Review: a safe solution and an expert one.
        static LevelData L09() => new LevelBuilder("C5-09", "Revisão Geral|Grand Review", "Solução segura e solução de especialista.|A safe solution and an expert one.", Theme.Arena, 2, 3)
            .Floor(-2.1f, -4.3f, 2.1f, 4.3f)
            .Post(-1.0f, -1.6f, 0.35f).Post(1.0f, -0.2f, 0.35f)
            .Zone(Surface.Sand, -2.1f, 0.6f, 2.1f, 1.8f)
            .Rail(-2.1f, 2.8f, 0.6f, 2.8f)
            .Start(0f, -3.7f).Hole(-1.0f, 3.7f).Done();

        // Final: gate, sand, ice with a lake, tunnel, slider and baffles -- every tool the cup has used, no new rule.
        static LevelData L10() => new LevelBuilder("C5-10", "Grande Final dos Mestres|Masters Grand Final", "O clímax: tudo que você domina.|The climax: everything you've mastered.", Theme.Arena, 3, 4).Final()
            .Floor(-2f, -4.8f, 2f, 4.8f)
            .Rail(-2f, -3.3f, -0.6f, -3.3f).Rail(0.9f, -3.3f, 2f, -3.3f)
            .Zone(Surface.Sand, -2f, -2.4f, 2f, -1.4f)
            .Zone(Surface.Ice, -2f, -1.4f, 2f, 0.2f)
            .Water(-0.5f, -1.2f, 0.7f, -0.1f)
            .Rail(2f, 0.2f, -0.2f, 0.2f)
            .Tunnel(1.4f, -0.5f, 0f, -1f, -1.4f, 1.3f, 0f, 1f)
            .Slider(-2f, 2.5f, -0.5f, 2.5f, 1f, 0f, 1.2f, 6f)
            .Rail(-2f, 3.4f, 0.2f, 3.4f)
            .Start(-1.3f, -4.3f).Hole(-1.2f, 4.2f).Done();
    }
}
