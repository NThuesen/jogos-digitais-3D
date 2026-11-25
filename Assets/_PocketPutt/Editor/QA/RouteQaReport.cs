using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PocketPutt.QA
{
    [Serializable]
    public sealed class QaRunReport
    {
        public string schema = "pocket-putt-route-qa-v1";
        public string revision, sourceFingerprint, environment;
        public RouteQaSettings settings;
        public List<QaLevelReport> levels = new List<QaLevelReport>();
    }

    public static class RouteQaReport
    {
        static string N(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        static string Rate(int hits, int total) => hits + "/" + total;
        public static string Csv(QaRunReport run)
        {
            var sb = new StringBuilder("level,exact_high_hits,exact_high_samples,near_high_hits,near_high_samples,reaim_high_completed,reaim_high_samples,reaim_high_best_shots,reaim_high_within_3star,route_families,winning_candidates,best_sand_shots,best_sand_free_shots,sand_robustness,sand_free_robustness,search_shots,budget_exhausted,family_limit,high_review,policy_review,tutorial\n");
            foreach (QaLevelReport r in run.levels)
                sb.AppendLine(string.Join(",", r.id, r.direct.exactHighSunk, r.direct.exactHighAttempts, r.direct.highSunk, r.direct.highAttempts,
                    r.policies.highCompleted, r.policies.highAttempts, r.policies.bestHighShots, r.policies.highCompletedWithinThreeStar,
                    r.families.Count, r.winningPlans, r.bestSandShots, r.bestSandFreeShots, N(r.bestSandRobustness), N(r.bestSandFreeRobustness),
                    r.searchShots, r.searchBudgetExhausted, r.familyLimitReached, r.highPowerReview, r.trivialPolicyReview, r.tutorialException));
            return sb.ToString();
        }

        public static string Markdown(QaRunReport run)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Pocket Putt — QA técnico de rotas");
            sb.AppendLine();
            sb.AppendLine("Revisão: `" + run.revision + "`; SHA-256 das fontes: `" + run.sourceFingerprint + "`.");
            sb.AppendLine("Ambiente: " + run.environment + ".");
            sb.AppendLine();
            sb.AppendLine("Contagens são amostras determinísticas, não probabilidades de acerto humano. REVIEW indica evidência para revisão autoral; C1-01 é exceção tutorial. Zero soluções significa apenas nenhuma encontrada nesta busca limitada. Diversão e justiça: PENDENTES DE TESTE DO CRIADOR.");
            sb.AppendLine();
            sb.AppendLine("| Fase | Reta forte exata | Quase reta forte | Re-mira forte: concluídas | Melhor nº | ≤3★ | Famílias | Areia: tacadas / robustez | Sem areia: tacadas / robustez | Revisar |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---|");
            foreach (QaLevelReport r in run.levels)
            {
                string flags = r.tutorialException ? "tutorial" : (r.highPowerReview ? "tiro forte " : "") + (r.trivialPolicyReview ? "re-mira" : "");
                sb.AppendLine($"| {r.id} | {Rate(r.direct.exactHighSunk, r.direct.exactHighAttempts)} | {Rate(r.direct.highSunk, r.direct.highAttempts)} | {Rate(r.policies.highCompleted, r.policies.highAttempts)} | {r.policies.bestHighShots} | {r.policies.highCompletedWithinThreeStar} | {r.families.Count}{(r.familyLimitReached ? "+" : "")} | {r.bestSandShots} / {N(r.bestSandRobustness)} | {r.bestSandFreeShots} / {N(r.bestSandFreeRobustness)} | {flags} |");
            }
            sb.AppendLine();
            sb.AppendLine("0 tacadas nas colunas de alternativas = não encontrada. Robustez = fração de replays completos com erros, não tolerância garantida. Sem areia não comprova segurança. Famílias são agrupamentos geométricos aproximados, não classes topológicas. Ver ROUTE_QA.md para grids, caps, timing e limites de hazards.");
            return sb.ToString();
        }
    }
}
