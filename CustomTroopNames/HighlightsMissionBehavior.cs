using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SandBox.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.MountAndBlade;

namespace CustomTroopNames {
    public class HighlightData {
        public int Kills;
        public List<string> HeroKills;
    }

    public class HighlightsManager {
        private readonly Dictionary<string, HighlightData> _battleStats =
            new Dictionary<string, HighlightData>();

        private readonly List<string> _deadTroops = new List<string>();

        private HighlightData _getOrInsertDefault(string troopName) {
            if (_battleStats.TryGetValue(troopName, out var stats)) {
                return stats;
            }

            var newStats = new HighlightData();
            _battleStats.Add(troopName, newStats);
            return newStats;
        }

        public void TroopGetsKill(string troopName, [CanBeNull] string killedHero) {
            var highlights = _getOrInsertDefault(troopName);
            highlights.Kills += 1;

            if (killedHero == null) return;
            if (highlights.HeroKills == null) {
                highlights.HeroKills = new List<string>(1);
            }
            highlights.HeroKills.Add(killedHero);
        }

        public void TroopDied(string troopName) {
            _deadTroops.Add(troopName);
        }

        public void ShowResults(IBattleObserver battleObserver) {
            if (!(battleObserver is SPScoreboardVM scoreboard)) return;
            // Log kills
            var statsList = _battleStats.ToList();
            statsList.Sort((x, y) => ScoreHighlight(x.Value) - ScoreHighlight(y.Value));
            List<TooltipProperty> NoTooltip() => new List<TooltipProperty>();
            foreach (var message in statsList
                .Select(pair => MessageForTroop(pair.Key, pair.Value))
                .Where(message => message != null)) {
                scoreboard.BattleResults.Add(new BattleResultVM(message, NoTooltip));
            }

            // Log deaths
            if (_deadTroops.Count == 0) return;
            const int maxTroopsToName = 3;
            var allTroopsMsg = $"{_joinNames(_deadTroops)} died!";
            if (_deadTroops.Count <= maxTroopsToName) {
                scoreboard.BattleResults.Add(new BattleResultVM(allTroopsMsg, NoTooltip));
            }
            else {
                var msg =
                    $"{_joinNames(_deadTroops.GetRange(0, maxTroopsToName))} and {_deadTroops.Count - maxTroopsToName} others died!";
                scoreboard.BattleResults.Add(new BattleResultVM(msg, () =>
                    new List<TooltipProperty> {
                        new TooltipProperty(string.Empty, allTroopsMsg, 0)
                    }));
            }
        }

        private static readonly string[] StreakNames = {
            "Double Kill",
            "Triple Kill",
            "Overkill",
            // ReSharper disable StringLiteralTypo
            "Killtacular",
            "Killtrocity",
            "Killimanjaro",
            "Killtastrophe",
            "Killpocalypse",
            "Killionaire",
            // ReSharper restore StringLiteralTypo
        };

        private static string _joinNames(IEnumerable<string> names) {
            return string.Join(" and ", names);
        }

        private string MessageForTroop(string troopName, HighlightData data) {
            if (data.Kills == 0 || (data.Kills == 1 && data.HeroKills == null))
                return null;
            var diedSuffix = _deadTroops.Contains(troopName) ? ".. then died!" : "";
            var heroKillsStr = data.HeroKills == null ? null : _joinNames(data.HeroKills);

            if (data.Kills == 1) {
                return $"{troopName} wounded {heroKillsStr}.{diedSuffix}";
            }

            var streakName =
                StreakNames[Math.Min(data.Kills - 2, StreakNames.Length - 1)];

            var killsStr = heroKillsStr != null
                ? $"wounded {heroKillsStr} and inflicted {data.Kills - data.HeroKills.Count} other casualties"
                : $"inflicted {data.Kills} casualties";

            return $"{streakName}! {troopName} {killsStr}.{diedSuffix}";
        }

        private static int ScoreHighlight(HighlightData data) {
            return 10 * (data.HeroKills?.Count ?? 0) + data.Kills;
        }
    }

    public class HighlightsMissionBehavior : MissionLogic {
        private readonly HighlightsManager _highlightsManager = new HighlightsManager();

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow) {
            if (affectorAgent == null) return;

            // Named unit gets kill
            var affectorTroopInfo = affectorAgent
                .GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectorTroopInfo != null && affectedAgent.IsHuman) {
                var killedHeroName = affectedAgent.IsHero ? affectedAgent.Name : null;
                _highlightsManager.TroopGetsKill(affectorTroopInfo.Name, killedHeroName);
            }

            // Named unit was killed
            if (agentState != AgentState.Killed) return;

            var affectedTroopInfo = affectedAgent
                ?.GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectedTroopInfo != null) {
                _highlightsManager.TroopDied(affectedTroopInfo.Name);
            }
        }

        public override void ShowBattleResults() {
            base.ShowBattleResults();
            var battleObserver =
                Mission.GetMissionBehaviour<BattleObserverMissionLogic>();
            if (battleObserver != null) {
                _highlightsManager.ShowResults(battleObserver.BattleObserver);
            }

        }
    }
}
