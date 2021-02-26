using System.Diagnostics;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace CustomTroopNames {
    [UsedImplicitly]
    public class CustomTroopsSubModule : MBSubModuleBase {
        public override void OnMissionBehaviourInitialize(Mission mission) {
            base.OnMissionBehaviourInitialize(mission);
            mission.AddMissionBehaviour(new RenameTroopsBehavior());
        }
        
        protected override void OnGameStart(Game game, IGameStarter gameStarter) {
            base.OnGameStart(game, gameStarter);
            if (!(game.GameType is Campaign)) return;
            var campaignStarter = (CampaignGameStarter) gameStarter;
            campaignStarter.AddBehavior(new CustomTroopNamesCampaignBehavior());
        }
    }

    public class CustomTroopNamesCampaignBehavior : CampaignBehaviorBase {
        private string _customName = "Initial";

        public override void RegisterEvents() {
            CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this,
                (o, i) => {
                    InformationManager.ShowTextInquiry(new TextInquiryData("Title",
                        _customName, true, true, "Yes", "No", s => { _customName = s; },
                        () => { }));
                });
        }

        public override void SyncData(IDataStore dataStore) {
            dataStore.SyncData("_customName", ref _customName);
        }
    }

    public class RenameTroopsBehavior : MissionLogic {
        public override void OnAgentCreated(Agent agent) {
            base.OnAgentCreated(agent);
            if (!agent.IsHuman) return;

            var originalName = agent.Character.Name;
            agent.Character.Name =
                new TextObject(agent.Name + " " + agent.Index.ToString());
            // Reapply the setter logic that copies the name from the character object
            agent.Character = agent.Character;
            agent.Character.Name = originalName;

            Debug.WriteLine(agent.Name);
        }
    }

}