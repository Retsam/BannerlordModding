using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Debug = System.Diagnostics.Debug;

namespace CustomTroopNames {
    public class CustomTroopsMissionBehavior : MissionLogic {
        public CustomTroopsMissionBehavior(CustomTroopNameManager nameManager) {
            _nameManager = nameManager;
        }

        private readonly CustomTroopNameManager _nameManager;
        private Dictionary<string, List<CustomTroopInfo>> _troopsToAssign;

        public override void EarlyStart() {
            base.EarlyStart();
            _troopsToAssign = _nameManager.GetTroopsToAssign();
        }

        public override void OnAgentBuild(Agent agent, Banner banner) {
            base.OnAgentBuild(agent, banner);
            if (
                // Ignore non-humans...
                !agent.IsHuman ||
                // enemy soldiers...
                agent.Team != Mission.PlayerTeam
                // and the player
                || agent.IsPlayerControlled)
                return;

            if (
                !_troopsToAssign.TryGetValue(agent.Character.Name.ToString(),
                    out var troops)
                || troops.Count == 0
            ) return;
            agent.AddComponent(new CustomNameAgentComponent(agent, troops[0]));
            RenameAgent(agent, troops[0].Name);
            troops.RemoveAt(0);
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
            AgentState agentState,
            KillingBlow blow) {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            var affectorTroopInfo = affectorAgent
                ?.GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectorTroopInfo != null) {
                affectorTroopInfo.Kills += 1;
                if (agentState == AgentState.Killed) {
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"{affectorAgent.Name} killed {affectedAgent.Name}",
                            ModColors.MainColor));
                }
                else if (agentState == AgentState.Unconscious) {
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"{affectorAgent.Name} knocked out {affectedAgent.Name}",
                            ModColors.MainColor));
                }
                else {
                    Debug.WriteLine($"Unexpected state {agentState}");
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"{affectorAgent.Name} did something to {affectedAgent.Name}",
                            ModColors.AlertColor));
                }
            }

            if (agentState != AgentState.Killed) return;

            var affectedTroopInfo = affectedAgent
                ?.GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            if (affectedTroopInfo == null) return;

            _nameManager.TroopDied(affectedAgent.Character, affectedTroopInfo);
            InformationManager.DisplayMessage(
                new InformationMessage($"{affectedAgent.Name} DIES",
                    ModColors.AlertColor));
        }

        private static void RenameAgent(Agent agent, string customName) {
            var originalName = agent.Character.Name;
            agent.Character.Name = new TextObject(customName);
            // Reapply the setter logic that copies the name from the character object
            agent.Character = agent.Character;
            agent.Character.Name = originalName;
        }
    }
}
