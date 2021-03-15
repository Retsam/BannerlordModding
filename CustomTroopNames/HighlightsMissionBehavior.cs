using System;
using System.Collections.Generic;
using System.Linq;
using SandBox.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.MountAndBlade;

namespace CustomTroopNames {
    public class HighlightData {
        public int Kills;
        public List<string> HeroKills;
    }

    public class HighlightsMissionBehavior : MissionLogic {
        private readonly Dictionary<string, HighlightData> _battleStats =
            new Dictionary<string,
                HighlightData>();

        private readonly List<string> _deadTroops = new List<string>();

        private HighlightData _getOrInsertDefault(string troopName) {
            if (_battleStats.TryGetValue(troopName, out var stats)) {
                return stats;
            }

            var newStats = new HighlightData();
            _battleStats.Add(troopName, newStats);
            return newStats;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
            AgentState agentState,
            KillingBlow blow) {
            if (affectorAgent == null) return;

            // Named unit gets kill
            var affectorTroopInfo = affectorAgent
                .GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectorTroopInfo != null && affectedAgent.IsHuman) {
                var highlights = _getOrInsertDefault(affectorTroopInfo.Name);
                highlights.Kills += 1;
                if (affectedAgent?.IsHero ?? false) {
                    if (highlights.HeroKills == null) {
                        highlights.HeroKills = new List<string>(1);
                    }

                    highlights.HeroKills.Add(affectedAgent.Name);
                }
            }

            // Named unit was killed
            if (agentState != AgentState.Killed) return;

            var affectedTroopInfo = affectedAgent
                ?.GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectedTroopInfo != null) {
                _deadTroops.Add(affectedTroopInfo.Name);
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

        public override void ShowBattleResults() {
            base.ShowBattleResults();
            if (!(Mission.GetMissionBehaviour<BattleObserverMissionLogic>()?
                .BattleObserver is SPScoreboardVM scoreboard)) return;

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
    }
}
