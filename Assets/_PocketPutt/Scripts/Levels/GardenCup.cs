using PocketPutt.Sim;

namespace PocketPutt.Levels
{
    /// <summary>
    /// Copa do Jardim: control, banking and routes. Every course has a safe line; 3 stars reward the sharper one.
    /// Titles are authored as "pt|en". Star targets come from the solver (see docs/LEVEL_QA.md).
    /// </summary>
    public static class GardenCup
    {
        public static LevelData[] Build() => new[]
        {
            L01(), L02(), L03(), L04(), L05(), L06(), L07(), L08(), L09(), L10()
        };

        // Straight lane: learn direction and power.
        static LevelData L01() => new LevelBuilder("C1-01", "Primeiro Putt|First Putt", "Arraste para trás e solte.|Drag back and release.", Theme.Garden, 1, 2)
            .Floor(-1.5f, -3.6f, 1.5f, 3.6f)
            .Start(0f, -2.45f).Hole(0f, 2.45f).Done();

        // Power lesson: a narrow lane opens into a green with a sand backstop behind the hole.
        // Too little stops short, too much runs into the sand; only a measured putt drops.
        static LevelData L02() => new LevelBuilder("C1-02", "Força Certa|Right Power", "Força demais para na areia.|Too much power ends in the sand.", Theme.Garden, 2, 3)
            .Floor(-0.9f, -3.8f, 0.9f, 0.6f)
            .Floor(-1.7f, 0.6f, 1.7f, 3.4f)
            .Zone(Surface.Sand, -1.7f, 1.85f, 1.7f, 3.4f)
            .Start(0f, -3.0f).Hole(0f, 1.0f).Done();

        // L-shaped lane with a diagonal bumper in the elbow: walls guide the ball.
        static LevelData L03() => new LevelBuilder("C1-03", "A Curva|The Bend", "A parede inclinada também conduz.|The angled wall guides the ball.", Theme.Garden, 1, 2)
            .Floor(-1.8f, -4.0f, -0.6f, 1.0f)
            .Floor(-1.8f, -0.2f, 1.9f, 1.0f)
            .Block(-1.8f, 0.21f, -1.01f, 1.0f, -1.8f, 1.0f)
            .Start(-1.2f, -3.2f).Hole(1.2f, 0.4f).Done();

        // Two offset chambers share a broad bank wall: one deliberate rebound beats two positioning putts.
        static LevelData L04() => new LevelBuilder("C1-04", "Ricochete|Bank Shot", "Use a parede lateral.|Use the side wall.", Theme.Garden, 2, 3)
            .Floor(-1.8f, -3.2f, 2f, 0.4f)
            .Floor(-1.2f, -0.4f, 2f, 3.2f)
            .Rail(-1.8f, 0f, 0.3f, 0f)
            .Start(-0.9f, -2.4f).Hole(-0.6f, 2.4f).Done();

        // Offset rooms around an island: the left reflector advances through the short wet lane.
        // Three dry positioning putts use the wide right flank; the deliberate bank saves one.
        static LevelData L05() => new LevelBuilder("C1-05", "Atalho ou Caminho|Shortcut or Safe", "Rebata à esquerda ou contorne pela direita.|Bank left or take the safe right route.", Theme.Garden, 2, 3)
            .Floor(-2.3f, -3.8f, 1.1f, -1.3f)
            .Floor(-2.3f, -1.3f, -0.5f, 1.2f)
            .Floor(-2.3f, 1.2f, 2.3f, 3.4f)
            .Floor(0.2f, -2.6f, 2.3f, 1.2f)
            .Block(-2.3f, -3.8f, -1.31f, -3.8f, -2.3f, -2.81f)
            .Water(-1.25f, -0.9f, -0.5f, 0.8f)
            .Rail(-1.25f, -1.3f, -0.5f, -1.3f)
            .Rail(0.2f, -1.3f, 1f, -1.3f)
            .Rail(0.1f, 1.2f, 0.1f, 2.2f)
            .Start(-0.2f, -3f).Hole(-0.6f, 2.8f).Done();

        // Hourglass: a hedge splits the pinch. Pick the lane that leaves you a clean second shot.
        static LevelData L06() => new LevelBuilder("C1-06", "Portão do Jardim|Garden Gate", "Sem pressa: os arbustos perdoam.|No rush: the hedges are forgiving.", Theme.Garden, 2, 3)
            .Floor(-2f, -3.8f, 2f, -0.7f)
            .Floor(-1.1f, -0.7f, 1.1f, 0.7f)
            .Floor(-2f, 0.7f, 2f, 3.8f)
            .Post(0f, 0f, 0.32f)
            .Start(1.2f, -3.2f).Hole(-1.2f, 3.2f).Done();

        // A real switchback: three lanes joined alternately on the right and on the left. Think about where the ball stops.
        static LevelData L07() => new LevelBuilder("C1-07", "Serpentina|Switchback", "Pense onde a bola vai parar.|Think about where the ball will stop.", Theme.Garden, 3, 4)
            .Floor(-2f, -4.2f, 2f, -2.6f)
            .Floor(0.7f, -2.6f, 2f, -0.6f)
            .Floor(-2f, -0.6f, 2f, 1f)
            .Floor(-2f, 1f, -0.7f, 3f)
            .Floor(-2f, 3f, 2f, 4.6f)
            .Start(-1.2f, -3.6f).Hole(1.2f, 4.0f).Done();

        // Wider elbows and a centred tee expose two banks without a free initial side-wall bounce.
        static LevelData L08() => new LevelBuilder("C1-08", "Dois Rebotes|Double Bounce", "Mire nas duas paredes inclinadas.|Aim for both angled walls.", Theme.Garden, 1, 2)
            .Floor(-1.9f, -2.8f, -0.5f, 1.1f)
            .Floor(-1.9f, -0.3f, 1.9f, 1.1f)
            .Floor(0.5f, -0.3f, 1.9f, 3.4f)
            .Block(-1.9f, 0.15f, -0.95f, 1.1f, -1.9f, 1.1f)
            .Block(0.95f, -0.3f, 1.9f, 0.65f, 1.9f, -0.3f)
            .Start(-1.2f, -1.7f).Hole(1.2f, 2.0f).Done();

        // A planted courtyard separates two dry paths. The left terrace is wider but longer;
        // positioning at the right entrance opens a two-putt line past the tree, without requiring a bank.
        static LevelData L09() => new LevelBuilder("C1-09", "Pátio das Árvores|Tree Courtyard", "Escolha onde preparar a próxima tacada.|Choose where to set up your next putt.", Theme.Garden, 2, 3)
            .Floor(-2.8f, -3.6f, 1.4f, -1.4f)
            .Floor(-2.8f, -1.4f, -1f, 2f)
            .Floor(0.3f, -2.5f, 2.5f, 2f)
            .Floor(-2.8f, 2f, 2.5f, 3.6f)
            .Post(0.7f, 0.2f, 0.38f)
            .Post(-0.2f, 3.35f, 0.25f)
            .Start(-0.9f, -2.8f).Hole(0.9f, 2.8f).Done();

        // Final: everything the Garden taught -- a lake with a narrow causeway, a safe flank, hedges and a bank around the baffle.
        static LevelData L10() => new LevelBuilder("C1-10", "Grande Final do Jardim|Garden Grand Final", "Tudo que você aprendeu.|Everything you learned.", Theme.Garden, 2, 3).Final()
            .Floor(-2f, -4.5f, 2f, 4.5f)
            .Water(-2f, -1.6f, -0.9f, 0.4f).Water(-0.3f, -1.6f, 0.4f, 0.4f)
            .Post(-1.0f, -2.7f, 0.34f)
            .Rail(2f, 2.0f, -0.6f, 2.0f)
            .Start(-1.2f, -4.0f).Hole(-1.2f, 3.7f).Done();
    }
}
