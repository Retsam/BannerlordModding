using System.Collections.Generic;
using System.Linq;
using SandBox.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.MountAndBlade;

namespace CustomTroopNames {
    public class HighlightData {
        public int Kills;
        public bool Died;
    }

    public class HighlightsMissionBehavior : MissionLogic {
        private Dictionary<string, HighlightData> _battleStats = new Dictionary<string,
            HighlightData>();

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
            var affectorTroopInfo = affectorAgent
                .GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectorTroopInfo != null) {
                _getOrInsertDefault(affectorTroopInfo.Name).Kills += 1;
            }

            if (agentState != AgentState.Killed) return;

            var affectedTroopInfo = affectedAgent
                ?.GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectedTroopInfo != null) {
                _getOrInsertDefault(affectedTroopInfo.Name).Died = true;
            }
        }

        private string MessageForTroop(string troopName, HighlightData data) {
            if (data.Died) {
                return data.Kills > 0
                    ? $"{troopName} died after inflicting {data.Kills} casualties"
                    : $"${troopName} died.";
            }

            return data.Kills >= 5
                ?
                $"Killing spree! {troopName} inflicted {data.Kills} casualties!"
                : data.Kills >= 3
                    ? $"{troopName} inflicted {data.Kills} casualties!"
                    : null;
        }

        public override void ShowBattleResults() {
            base.ShowBattleResults();
            if (!(Mission.GetMissionBehaviour<BattleObserverMissionLogic>()?
                .BattleObserver is SPScoreboardVM scoreboard)) return;

            var statsList = _battleStats.ToList();
            statsList.Sort((x, y) => x.Value.Kills - y.Value.Kills);
            foreach (var message in statsList.Select(pair => MessageForTroop(pair.Key, pair.Value)).Where(message => message != null)) {
                scoreboard.BattleResults.Add(new BattleResultVM(message,
                    () => new List<TooltipProperty>()));
            }

        }
    }
}
