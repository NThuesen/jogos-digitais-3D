using PocketPutt.Sim;

namespace PocketPutt.Levels
{
    /// <summary>
    /// Copa da Oficina: periodic movers. Motion never stops while you aim, so you pick the moment; windows are
    /// at least a second wide so no level needs reflexes (QA checks a +/-0.12 s tolerance around the planned shot).
    /// </summary>
    public static class WorkshopCup
    {
        public static LevelData[] Build() => new[]
        {
            L01(), L02(), L03(), L04(), L05(), L06(), L07(), L08(), L09(), L10()
        };

        // A sliding door switches between the left and right opening.
        static LevelData L01() => new LevelBuilder("C4-01", "Barreira Lenta|Slow Barrier", "Espere a porta abrir.|Wait for the door to open.", Theme.Workshop, 1, 2)
            .Floor(-1.6f, -3.6f, 1.6f, 3.6f)
            .Slider(-1.6f, 0f, 0.1f, 0f, 1f, 0f, 1.5f, 6f)
            .Start(0f, -2.8f).Hole(0f, 2.7f).Done();

        // A rotor in a narrow lane: go when it points along the lane.
        static LevelData L02() => new LevelBuilder("C4-02", "Janela de Tempo|Time Window", "Passe quando a pá estiver alinhada.|Go when the paddle lines up.", Theme.Workshop, 1, 2)
            .Floor(-1.0f, -3.6f, 1.0f, 3.6f)
            .Spinner(0f, 0f, 0.75f, 60f, 0f, 0.12f)
            .Start(0f, -2.8f).Hole(0f, 2.7f).Done();

        // The rotor sits in a doorway between two baffles; a hedge behind it blocks the straight line, so plan the second shot.
        static LevelData L03() => new LevelBuilder("C4-03", "Rotor|Rotor", "Uma pá giratória na porta.|A spinning paddle in the doorway.", Theme.Workshop, 2, 3)
            .Floor(-2f, -3.6f, 2f, 3.6f)
            .Rail(-2f, 0f, -1.2f, 0f).Rail(1.2f, 0f, 2f, 0f)
            .Spinner(0f, 0f, 0.9f, 60f, 90f, 0.12f)
            .Post(0.8f, 1.6f, 0.34f)
            .Start(0.8f, -2.8f).Hole(0.8f, 2.7f).Done();

        // L-shaped lane: a sliding door in the first leg, a bumper in the elbow.
        static LevelData L04() => new LevelBuilder("C4-04", "Timing e Ricochete|Timing and Bank", "Espere, rebata, acerte.|Wait, bank, score.", Theme.Workshop, 1, 2)
            .Floor(-1.8f, -4.0f, -0.6f, 1.0f)
            .Floor(-1.8f, -0.2f, 1.9f, 1.0f)
            .Block(-1.8f, 0.21f, -1.01f, 1.0f, -1.8f, 1.0f)
            .Slider(-1.8f, -1.8f, -0.9f, -1.8f, 1f, 0f, 0.9f, 4f)
            .Start(-1.2f, -3.3f).Hole(1.2f, 0.4f).Done();

        // Long static route versus a short timed route.
        static LevelData L05() => new LevelBuilder("C4-05", "Estático ou Temporizado|Static or Timed", "Rota segura ou atalho no tempo certo.|Safe route or a timed shortcut.", Theme.Workshop, 1, 2)
            .Floor(-2f, -3.8f, 2f, 3.8f)
            .BlockRect(-0.9f, -1.6f, 0.3f, 1.6f)
            .Spinner(1.15f, 0f, 0.6f, 60f, 90f, 0.12f)
            .Start(1.2f, -3.0f).Hole(1.2f, 3.0f).Done();

        // Bank off the moving paddle on purpose.
        static LevelData L06() => new LevelBuilder("C4-06", "Use o Movimento|Use the Motion", "A pá também serve de rebatedor.|The paddle can be a bumper too.", Theme.Workshop, 2, 3)
            .Floor(-2f, -3.6f, 2f, 3.6f)
            .Rail(-2f, 0.2f, 0.6f, 0.2f)
            .Slider(0.6f, 0.2f, 2f, 0.2f, 0f, 1f, 1.2f, 4f)
            .Start(-1.2f, -2.8f).Hole(-1.0f, 2.8f).Done();

        // Wait spot before the door, then the timed shot.
        static LevelData L07() => new LevelBuilder("C4-07", "Preparação|Set Up", "Posicione antes do ciclo.|Position before the cycle.", Theme.Workshop, 2, 3)
            .Floor(-2f, -4.2f, 2f, 4.2f)
            .Rail(-2f, -1.4f, 0.6f, -1.4f)
            .Slider(0.6f, -1.4f, 2f, -1.4f, 0f, 1f, 1.3f, 4f)
            .Spinner(-0.7f, 1.2f, 0.7f, 60f, 0f, 0.12f)
            .Start(-1.3f, -3.6f).Hole(1.2f, 3.6f).Done();

        // A tunnel delivers the ball into a rotor chamber.
        static LevelData L08() => new LevelBuilder("C4-08", "Túnel da Oficina|Workshop Tunnel", "Saia do túnel na hora certa.|Leave the tunnel at the right time.", Theme.Workshop, 2, 3)
            .Floor(-2f, -3.6f, 2f, 3.6f)
            .Rail(-2f, 0f, 2f, 0f)
            .Tunnel(-1.3f, -0.9f, 0f, -1f, -1.2f, 0.9f, 0.7f, 0.7f)
            .Spinner(0.3f, 1.4f, 0.9f, 72f, 0f, 0.12f)
            .Start(0.6f, -2.8f).Hole(1.2f, 2.9f).Done();

        // Sliding doors, a rotor and a sand brake.
        static LevelData L09() => new LevelBuilder("C4-09", "Síntese da Oficina|Workshop Mix", "Leia o tempo e controle a força.|Read the timing and control the power.", Theme.Workshop, 3, 4)
            .Floor(-2f, -4.2f, 2f, 4.2f)
            .Rail(-2f, -1.2f, 0.4f, -1.2f)
            .Rail(2f, 1.2f, -0.4f, 1.2f)
            .Spinner(1.2f, -1.2f, 0.7f, 60f, 0f, 0.12f)
            .Zone(Surface.Sand, -2f, 2.2f, 2f, 4.2f)
            .Start(-1.2f, -3.6f).Hole(0f, 3.3f).Done();

        // Final: door, rotor doorway, switchback and a sliding gate near the end. Doorway widened and rotor slowed so every window is a second or more.
        static LevelData L10() => new LevelBuilder("C4-10", "Grande Final da Oficina|Workshop Grand Final", "Precisão e timing.|Precision and timing.", Theme.Workshop, 3, 4).Final()
            .Floor(-2f, -4.5f, 2f, 4.5f)
            .Slider(-2f, -2.9f, -0.4f, -2.9f, 1f, 0f, 1.4f, 6f)
            .Rail(-2f, -1.2f, -1.0f, -1.2f).Rail(1.0f, -1.2f, 2f, -1.2f)
            .Spinner(0f, -1.2f, 0.6f, 60f, 90f, 0.12f)
            .Rail(2f, 1.0f, -0.6f, 1.0f)
            .Slider(-2f, 2.6f, -0.9f, 2.6f, 1f, 0f, 1.2f, 4f)
            .Rail(2f, 3.5f, -0.2f, 3.5f)
            .Start(-1.3f, -4.0f).Hole(1.2f, 4.2f).Done();
    }
}
