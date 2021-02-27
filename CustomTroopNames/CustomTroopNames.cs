using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

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
    internal class CustomTroopNameManager {
        private const char Sep = '%';

        private Dictionary<string, string> _troopNameMapping =
            new Dictionary<string, string>();
        
        public void TroopRecruited(CharacterObject unit, string customName) {
            var unitName = unit.Name.ToString();
            if (_troopNameMapping.TryGetValue(unitName, out var troops)) {
                _troopNameMapping[unitName] = troops + $"{Sep}{customName}";
            }
            else {
                _troopNameMapping.Add(unitName, customName);
            }
        }

        public void PrintDebug() {
            foreach (var pair in _troopNameMapping) {
                var troopName = pair.Key;
                foreach (var customName in pair.Value.Split(Sep)) {
                    InformationManager.DisplayMessage(new InformationMessage
                        ($"{troopName} {customName}"));
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
                (unit, newUnit, i) => {
                    Debug.WriteLine($"{unit.Name} digivolved to ${newUnit.Name}");
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