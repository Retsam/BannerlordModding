﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;

namespace CustomTroopNames {
    [UsedImplicitly]
    public class CustomTroopsSubModule : MBSubModuleBase {
        public override void OnMissionBehaviourInitialize(Mission mission) {
            base.OnMissionBehaviourInitialize(mission);
            mission.AddMissionBehaviour(new RenameTroopsBehavior());
        }

        protected override void OnApplicationTick(float dt) {
            base.OnApplicationTick(dt);
            if (Input.IsKeyPressed(InputKey.Tilde)) {
                Campaign.Current?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()
                    ?.PrintDebug();
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter) {
            base.OnGameStart(game, gameStarter);
            if (!(game.GameType is Campaign)) return;
            var campaignStarter = (CampaignGameStarter) gameStarter;
            campaignStarter.AddBehavior(new CustomTroopNamesCampaignBehavior());
        }
    }

    [UsedImplicitly]
    public class CustomTroopNamesSaveableTypeDefiner : SaveableTypeDefiner {
        public CustomTroopNamesSaveableTypeDefiner() : base(48211205) { }

        protected override void DefineClassTypes() {
            AddClassDefinition(typeof(CustomTroopInfo), 1);
        }

        protected override void DefineContainerDefinitions() {
            ConstructContainerDefinition(typeof(List<CustomTroopInfo>));
            ConstructContainerDefinition(typeof(Dictionary<string, List<CustomTroopInfo>>));
        }
    }

    public class CustomTroopInfo {
        public CustomTroopInfo(string name) {
            Name = name;
        }
        [SaveableField(1)] public string Name;

        [SaveableField(2)] public int Kills = 0;
    }

    public class CustomTroopNameManager {
        private Dictionary<string, List<CustomTroopInfo>> _troopNameMapping =
            new Dictionary<string, List<CustomTroopInfo>>();

        public void TroopRecruited(CharacterObject unit, string customName) {
            AddTroop(unit, new CustomTroopInfo(customName));
        }

        public void AddTroop(CharacterObject unit, CustomTroopInfo newTroop) {
            var unitName = unit.Name.ToString();
            if (_troopNameMapping.TryGetValue(unitName, out var troops)) {
                troops.Add(newTroop);
            }
            else {
                _troopNameMapping.Add(unitName, new List<CustomTroopInfo>() {newTroop});
            }
        }

        public void TroopUpgraded(CharacterObject oldType, CharacterObject newType) {
            if (!_troopNameMapping.TryGetValue(oldType.Name.ToString(), out var troops)) {
                return;
            }

            var troopInfo = troops[0];
            troops.Remove(troopInfo);

            AddTroop(newType, troopInfo);
            InformationManager.DisplayMessage(new InformationMessage(
                ($"{troopInfo.Name} has been promoted to {newType.Name}")));
        }

        public void PrintDebug() {
            foreach (var pair in _troopNameMapping) {
                var troopName = pair.Key;
                foreach (var troopInfo in pair.Value) {
                    InformationManager.DisplayMessage(new InformationMessage
                        ($"{troopName} {troopInfo.Name}"));
                }
            }
        }

        public void SyncData(IDataStore dataStore) {
            dataStore.SyncData("_troopNameMapping", ref _troopNameMapping);
        }
    }

    public class CustomTroopNamesCampaignBehavior : CampaignBehaviorBase {
        private readonly CustomTroopNameManager _troopManager =
            new CustomTroopNameManager();

        private List<CharacterObject> _textPromptsToShow = new List<CharacterObject>();
        private Task _flushTextPromptsTask;

        private async void FlushTextPrompts() {
            _flushTextPromptsTask = null;
            var textPrompts = _textPromptsToShow;
            _textPromptsToShow = new List<CharacterObject>();
            var doneNaming = false;
            foreach (var unit in textPrompts) {
                await ShowTextInquiryAsync((new TextInquiryData("Name Troop",
                    $"Assign custom name to {unit.Name}?  (Leave blank to stop naming troops)",
                    true, false, "Set Name", "Don't name",
                    customName => {
                        if (customName.Length > 0) {
                            _troopManager.TroopRecruited(unit, customName);
                        }
                        else {
                            doneNaming = true;
                        }
                    },
                    // This currently doesn't work - negativeAction appears to not fire at all if the negative button is clicked
                    // For now working around it by leaving the input blank
                    () => { doneNaming = true; })));
                if (doneNaming) break;
            }
        }

        private static Task ShowTextInquiryAsync(TextInquiryData inquiryData) {
            // No meaning to the result value, but it has to be something - not exposed by the functions signature
            var task = new TaskCompletionSource<bool>(TaskCreationOptions.None);
            InformationManager.ShowTextInquiry(new TextInquiryData(inquiryData.TitleText,
                inquiryData.Text, inquiryData.IsAffirmativeOptionShown,
                inquiryData.IsNegativeOptionShown, inquiryData.AffirmativeText,
                inquiryData.NegativeText,
                (s) => {
                    inquiryData.AffirmativeAction(s);
                    task.SetResult(true);
                }, () => {
                    inquiryData.NegativeAction();
                    task.SetResult(true);
                }));
            return task.Task;
        }

        public override void RegisterEvents() {
            CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this,
                (unit, howMany) => {
                    if (howMany != 1) {
                        Debug.WriteLine($"Recruited more than 1 unit {howMany}");
                        return;
                    }

                    _textPromptsToShow.Add(unit);
                    if (_flushTextPromptsTask == null) {
                        _flushTextPromptsTask = Task.Run(async delegate {
                            await Task.Delay(500);
                            FlushTextPrompts();
                        });
                    }
                });
            CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(this,
                (unit, newUnit, howMany) => {
                    for(var i=0; i<howMany; i++) {
                        _troopManager.TroopUpgraded(unit, newUnit);
                        Debug.WriteLine($"{unit.Name} digivolved to ${newUnit.Name}");
                    }
                });
        }

        public void PrintDebug() {
            _troopManager.PrintDebug();
        }

        public override void SyncData(IDataStore dataStore) {
            _troopManager.SyncData(dataStore);
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
