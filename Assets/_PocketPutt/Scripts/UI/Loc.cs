using System.Collections.Generic;
using UnityEngine;

namespace PocketPutt.UI
{
    /// <summary>Two-language string table (pt-BR default for Portuguese devices, otherwise English).</summary>
    public static class Loc
    {
        public static bool Portuguese = true;

        public static void Init(int preference = 0)
        {
            if (preference == 1) Portuguese = true;
            else if (preference == 2) Portuguese = false;
            else Portuguese = Application.systemLanguage == SystemLanguage.Portuguese ||
                              Application.systemLanguage == SystemLanguage.Unknown;
        }

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // key -> { pt, en }
            ["title.sub"] = new[] { "Minigolfe de bolso", "Pocket mini golf" },
            ["menu.play"] = new[] { "JOGAR", "PLAY" },
            ["menu.continue"] = new[] { "CONTINUAR", "CONTINUE" },
            ["menu.cups"] = new[] { "Campeonatos", "Championships" },
            ["menu.trophies"] = new[] { "Troféus", "Trophies" },
            ["menu.gameHub"] = new[] { "Voltar ao hub", "Back to game hub" },
            ["menu.settings"] = new[] { "Ajustes", "Settings" },
            ["cup.finalProgress"] = new[] { "{0} / {1} estrelas para a Grande Final", "{0} / {1} stars to unlock the Grand Final" },
            ["cup.finalReady"] = new[] { "Grande Final liberada!", "Grand Final unlocked!" },
            ["cup.needAll"] = new[] { "Conclua as 9 classificatórias", "Finish all 9 qualifiers" },
            ["cup.locked"] = new[] { "Vença a Grande Final da {0}", "Win the Grand Final of the {0}" },
            ["cup.soon"] = new[] { "Em breve", "Coming soon" },
            ["cup.final"] = new[] { "GRANDE FINAL", "GRAND FINAL" },
            ["cup.finalLocked"] = new[] { "Faltam {0} estrelas", "{0} more stars needed" },
            ["cup.trophyWon"] = new[] { "Troféu conquistado", "Trophy won" },
            ["hud.shots"] = new[] { "TACADAS", "STROKES" },
            ["hud.hint"] = new[] { "Arraste de qualquer lugar da área de jogo para mirar e dosar a força", "Drag anywhere on the play area to aim and set power" },
            ["hud.edge"] = new[] { "Sem espaço? Volte ao ponto inicial, solte para cancelar e recomece mais ao centro", "Out of room? Move back to the start point, release to cancel, and restart nearer the middle" },
            ["hud.par"] = new[] { "{0} tacada(s) = 3 estrelas", "{0} stroke(s) = 3 stars" },
            ["res.hole"] = new[] { "BURACO!", "HOLE!" },
            ["res.ace"] = new[] { "HOLE IN ONE!", "HOLE IN ONE!" },
            ["res.shots"] = new[] { "{0} tacadas", "{0} strokes" },
            ["res.shot1"] = new[] { "1 tacada", "1 stroke" },
            ["res.best"] = new[] { "Melhor: {0}", "Best: {0}" },
            ["res.newBest"] = new[] { "Novo recorde!", "New record!" },
            ["res.next"] = new[] { "Próxima", "Next" },
            ["res.retry"] = new[] { "Repetir", "Retry" },
            ["res.menu"] = new[] { "Fases", "Levels" },
            ["res.finalOpen"] = new[] { "Grande Final desbloqueada!", "Grand Final unlocked!" },
            ["res.toFinal"] = new[] { "Grande Final", "Grand Final" },
            ["res.cupDone"] = new[] { "Voltar à Copa", "Back to cup" },
            ["res.stars1"] = new[] { "Termine em menos tacadas para mais estrelas", "Finish in fewer strokes for more stars" },
            ["trophy.title"] = new[] { "Armário de Troféus", "Trophy Cabinet" },
            ["trophy.won"] = new[] { "{0} conquistada!", "{0} won!" },
            ["trophy.cupUnlocked"] = new[] { "Nova copa desbloqueada: {0}", "New cup unlocked: {0}" },
            ["trophy.continue"] = new[] { "Continuar", "Continue" },
            ["trophy.allDone"] = new[] { "Você é o campeão do Pocket Putt!", "You are the Pocket Putt champion!" },
            ["trophy.locked"] = new[] { "Bloqueado", "Locked" },
            ["trophy.total"] = new[] { "{0} / {1} estrelas", "{0} / {1} stars" },
            ["set.title"] = new[] { "Ajustes", "Settings" },
            ["set.sound"] = new[] { "Efeitos sonoros", "Sound effects" },
            ["set.music"] = new[] { "Música", "Music" },
            ["set.vibration"] = new[] { "Vibração", "Vibration" },
            ["set.language"] = new[] { "Idioma: Português", "Language: English" },
            ["set.reset"] = new[] { "Apagar progresso", "Erase progress" },
            ["set.resetConfirm"] = new[] { "Toque de novo para apagar", "Tap again to erase" },
            ["set.privacy"] = new[] { "Opções de privacidade", "Privacy options" },
            ["set.close"] = new[] { "Fechar", "Close" },
            ["on"] = new[] { "LIGADO", "ON" },
            ["off"] = new[] { "DESLIGADO", "OFF" },
            ["level.locked"] = new[] { "Termine a fase anterior", "Finish the previous level" },
            ["back"] = new[] { "Voltar", "Back" },
            ["hub.title"] = new[] { "CAMPEONATOS", "CHAMPIONSHIPS" },
            ["hub.cupN"] = new[] { "COPA {0}", "CUP {0}" },
            ["hub.open"] = new[] { "ABRIR", "OPEN" },
            ["hub.review"] = new[] { "REVER", "REVIEW" },
            ["hub.replay"] = new[] { "REJOGAR", "REPLAY" },
            ["hub.locked"] = new[] { "Bloqueada", "Locked" },
            ["hub.mysteryLocked"] = new[] { "BLOQUEADO", "LOCKED" },
            ["hub.cupRequirement"] = new[] { "Conclua a Grande Final da {0}", "Complete the Grand Final of the {0}" },
            ["hub.mapRequirement"] = new[] { "Conclua a Grande Final da {0} para desbloquear", "Complete the Grand Final of the {0} to unlock" },
            ["hub.qual"] = new[] { "{0}/{1} classificatórias", "{0}/{1} qualifiers" },
            ["hub.qstars"] = new[] { "{0}/{1} estrelas nas classificatórias", "{0}/{1} qualifier stars" },
            ["hub.wip"] = new[] { "Mapa temático em produção", "Themed map in production" },
        };

        public static string T(string key)
        {
            return Table.TryGetValue(key, out string[] v) ? v[Portuguese ? 0 : 1] : key;
        }

        /// <summary>Picks the pt or en half of an authored "pt|en" string.</summary>
        public static string Pick(string both)
        {
            if (string.IsNullOrEmpty(both)) return both;
            int i = both.IndexOf('|');
            if (i < 0) return both;
            return Portuguese ? both.Substring(0, i) : both.Substring(i + 1);
        }

        public static string T(string key, params object[] args) => string.Format(T(key), args);
    }
}
