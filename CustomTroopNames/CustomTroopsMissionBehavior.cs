using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Debug = System.Diagnostics.Debug;

namespace CustomTroopNames {
    public class CustomTroopsMissionBehavior : MissionLogic {
        private Dictionary<string, List<CustomTroopInfo>> _troopsToAssign;

        public override void EarlyStart() {
            base.EarlyStart();
            var customTroopsBehavior = Campaign.Current
                ?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>();

            // TODO: pass in customTroopBehavior when creating MissionLogic?  (Only apply when appropriate)
            _troopsToAssign = customTroopsBehavior?
                .GetTroopsToAssign() ?? new Dictionary<string, List<CustomTroopInfo>>();
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
                            $"{affectorAgent.Name} killed {affectedAgent.Name}"));
                }
                else if (agentState == AgentState.Unconscious) {
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"{affectorAgent.Name} knocked out {affectedAgent.Name}"));
                }
                else {
                    Debug.WriteLine($"Unexpected state {agentState}");
                    InformationManager.DisplayMessage(
                        new InformationMessage(
                            $"{affectorAgent.Name} did something to {affectedAgent.Name}"));
                }
            }

            if (agentState != AgentState.Killed) return;

            var affectedTroopInfo = affectedAgent
                ?.GetComponent<CustomNameAgentComponent>()?.TroopInfo;
            var customTroopsBehavior = Campaign.Current
                ?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>();
            if (affectedTroopInfo == null || customTroopsBehavior == null) return;

            InformationManager.DisplayMessage(
                new InformationMessage($"{affectedAgent.Name} DIES", Colors.Red));
            customTroopsBehavior.TroopManager.TroopDied(affectedAgent.Character, affectedTroopInfo);
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