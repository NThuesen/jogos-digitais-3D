using PocketPutt.Sim;

namespace PocketPutt.Levels
{
    /// <summary>
    /// Copa Glacial: ice keeps momentum (friction is ~4x lower than grass) and tunnels connect two mouths.
    /// Lessons: dose power on ice, bank freely, then use tunnels as shortcuts.
    /// </summary>
    public static class GlacierCup
    {
        public static LevelData[] Build() => new[]
        {
            L01(), L02(), L03(), L04(), L05(), L06(), L07(), L08(), L09(), L10()
        };

        // A patch of ice mid-lane: the ball keeps its speed across it (intro).
        static LevelData L01() => new LevelBuilder("C3-01", "Primeiro Gelo|First Ice", "No gelo a bola desliza mais longe.|On ice the ball slides further.", Theme.Glacier, 1, 2)
            .Floor(-1.5f, -3.2f, 1.5f, 3.4f)
            .Zone(Surface.Ice, -1.5f, -0.8f, 1.5f, 3.4f)
            .Start(0f, -2.3f).Hole(0f, 2.6f).Done();

        // Whole lane is ice with a lake behind the hole: a gentle shot sinks, a hard one slides past and splashes.
        static LevelData L02() => new LevelBuilder("C3-02", "Deslize Longo|Long Slide", "Suavidade: passou do buraco, cai na água.|Be gentle: slide past the hole and you splash.", Theme.Glacier, 1, 2)
            .Floor(-1.3f, -4.0f, 1.3f, 5.0f)
            .Zone(Surface.Ice, -1.3f, -4.0f, 1.3f, 5.0f)
            .Water(-1.3f, 3.9f, 1.3f, 5.0f)
            .Start(0f, -3.2f).Hole(0f, 3.0f).Done();

        // Frictionless banking with a lake in the far corner: the wall returns almost everything, so plan the rebound.
        static LevelData L03() => new LevelBuilder("C3-03", "Ricochete Gelado|Icy Bank", "A parede devolve quase tudo.|The wall returns almost everything.", Theme.Glacier, 1, 2)
            .Floor(-1.9f, -3.4f, 1.9f, 3.4f)
            .Zone(Surface.Ice, -1.9f, -3.4f, 1.9f, 3.4f)
            .Rail(-1.9f, 0f, 0.2f, 0f)
            .Water(-1.9f, 1.0f, -0.6f, 3.4f)
            .Start(-1.0f, -2.6f).Hole(0.8f, 2.5f).Done();

        // Grass, an ice causeway between two lakes, then a grass pad: compare ice with grass.
        static LevelData L04() => new LevelBuilder("C3-04", "Gelo e Grama|Ice and Grass", "Compare o gelo com a grama.|Compare ice with grass.", Theme.Glacier, 1, 2)
            .Floor(-1.5f, -3.8f, 1.5f, 3.8f)
            .Zone(Surface.Ice, -1.5f, -1.8f, 1.5f, 1.2f)
            .Water(-1.5f, -1.4f, -0.6f, 0.8f).Water(0.6f, -1.4f, 1.5f, 0.8f)
            .Start(0f, -3.0f).Hole(0f, 2.9f).Done();

        // First tunnel: the entrance is below the wall, the exit pops out under an ice yard with a lake behind the hole.
        static LevelData L05() => new LevelBuilder("C3-05", "O Túnel|The Tunnel", "Entre no túnel para sair do outro lado.|Enter the tunnel to come out the other side.", Theme.Glacier, 1, 2)
            .Floor(-2f, -3.6f, 2f, 4.6f)
            .Rail(-2f, 0f, 2f, 0f)
            .Zone(Surface.Ice, -2f, 0f, 2f, 4.6f)
            .Water(-2f, 3.6f, 2f, 4.6f)
            .Tunnel(-1.3f, -0.9f, 0f, -1f, 0f, 0.9f, 0f, 1f)
            .Start(0.8f, -2.8f).Hole(0f, 2.7f).Done();

        // The mouth is set in the right wall: aim across the course; the exit throws you towards a lake on the left.
        static LevelData L06() => new LevelBuilder("C3-06", "Mire a Entrada|Aim for the Mouth", "A entrada fica na parede.|The mouth is in the wall.", Theme.Glacier, 1, 2)
            .Floor(-2f, -3.6f, 2f, 3.6f)
            .Rail(-2f, 0f, 2f, 0f)
            .Zone(Surface.Ice, -2f, 0f, 2f, 3.6f)
            .Water(-2f, 1.2f, -1.2f, 3.6f)
            .Tunnel(1.5f, -1.6f, -1f, 0f, 1.4f, 1.0f, -0.78f, 0.62f)
            .Start(-1.3f, -3.0f).Hole(-0.6f, 2.6f).Done();

        // Direct over a lake in the ice band, or take the tunnel; the hole is across the course from the exit.
        static LevelData L07() => new LevelBuilder("C3-07", "Direta ou Túnel|Direct or Tunnel", "Escolha a rota.|Pick your route.", Theme.Glacier, 2, 3)
            .Floor(-2f, -4.0f, 2f, 4.0f)
            .Zone(Surface.Ice, -2f, -0.8f, 2f, 1.0f)
            .Water(-0.6f, -0.4f, 0.6f, 0.6f)
            .Rail(-2f, 1.9f, 0.5f, 1.9f)
            .Post(-0.5f, -1.8f, 0.34f)
            .Tunnel(1.4f, -1.5f, -1f, 0f, -1.3f, 2.5f, 0.3f, 1f)
            .Start(0f, -3.3f).Hole(1.2f, 3.4f).Done();

        // The exit spits you onto ice between two lakes: soft hands, and a second shot is needed.
        static LevelData L08() => new LevelBuilder("C3-08", "Saída Escorregadia|Slippery Exit", "Saia do túnel e deslize.|Leave the tunnel and glide.", Theme.Glacier, 2, 3)
            .Floor(-2f, -3.6f, 2f, 4.6f)
            .Rail(-2f, -0.2f, 2f, -0.2f)
            .Zone(Surface.Ice, -2f, -0.2f, 2f, 4.6f)
            .Water(-2f, 3.9f, 2f, 4.6f)
            .Water(-2f, 1.4f, -0.8f, 2.6f)
            .Tunnel(-1.3f, -1.1f, 0f, -1f, 1.3f, 0.6f, -0.27f, 0.96f)
            .Start(0f, -2.9f).Hole(-0.9f, 3.2f).Done();

        // Ice yards, a hedge gate, a lake and a tunnel whose exit throws you sideways.
        static LevelData L09() => new LevelBuilder("C3-09", "Síntese Glacial|Glacier Mix", "Gelo, inércia e atalho.|Ice, momentum and shortcut.", Theme.Glacier, 2, 3)
            .Floor(-2f, -4.2f, 2f, 4.6f)
            .Zone(Surface.Ice, -2f, -4.2f, 2f, -1.0f)
            .Zone(Surface.Ice, -2f, 1.2f, 2f, 4.6f)
            .Rail(-2f, 0.6f, 0.4f, 0.6f)
            .Post(0.9f, -0.2f, 0.34f)
            .Water(-2f, 2.6f, -0.5f, 4.6f)
            .Tunnel(-1.5f, -0.35f, 0f, -1f, -1.4f, 1.5f, 1f, 0.15f)
            .Start(0f, -3.6f).Hole(1.0f, 3.6f).Done();

        // Final: ice yard with a lake, tunnel, switchback and a baffle in front of an ice-rimmed hole.
        static LevelData L10() => new LevelBuilder("C3-10", "Grande Final Glacial|Glacier Grand Final", "Leia o impulso e as conexões.|Read the momentum and the connections.", Theme.Glacier, 2, 3).Final()
            .Floor(-2f, -4.5f, 2f, 4.5f)
            .Zone(Surface.Ice, -2f, -4.5f, 2f, -2.0f)
            .Water(-0.2f, -3.6f, 1.0f, -2.6f)
            .Rail(-2f, -1.2f, 0.7f, -1.2f)
            .Rail(2f, 0.6f, -0.6f, 0.6f)
            .Tunnel(1.4f, -1.8f, -1f, 0f, 1.4f, 1.6f, 0f, 1f)
            .Rail(-2f, 3.0f, -0.2f, 3.0f)
            .Zone(Surface.Ice, -2f, 2.0f, 2f, 4.5f)
            .Post(0.9f, 2.4f, 0.32f)
            .Start(-1.3f, -4.0f).Hole(-1.2f, 3.9f).Done();
    }
}
