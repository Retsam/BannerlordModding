using System.Collections.Generic;
using System.Linq;
using Debug = System.Diagnostics.Debug;
using System.Threading.Tasks;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;

namespace CustomTroopNames {
    [UsedImplicitly]
    public class CustomTroopsSubModule : MBSubModuleBase {
        public override void OnMissionBehaviourInitialize(Mission mission) {
            base.OnMissionBehaviourInitialize(mission);
            mission.AddMissionBehaviour(new CustomTroopsMissionBehavior());
        }

        protected override void OnApplicationTick(float dt) {
            base.OnApplicationTick(dt);
            if (Input.IsKeyPressed(InputKey.Tilde)) {
                Campaign.Current
                    ?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()
                    ?.PrintDebug(Input.IsKeyDown(InputKey.RightShift));
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
            AddClassDefinition(typeof(DeadTroopInfo), 2);
        }

        protected override void DefineContainerDefinitions() {
            ConstructContainerDefinition(typeof(List<CustomTroopInfo>));
            ConstructContainerDefinition(
                typeof(Dictionary<string, List<CustomTroopInfo>>));
            ConstructContainerDefinition(typeof(List<DeadTroopInfo>));
        }
    }

    public class CustomTroopInfo {
        public CustomTroopInfo(string name) {
            Name = name;
        }

        [SaveableField(1)] public readonly string Name;

        [SaveableField(2)] public int Kills = 0;
    }

    public class DeadTroopInfo {
        public DeadTroopInfo(CustomTroopInfo info, string troopType) {
            Info = info;
            TroopType = troopType;
        }
        [SaveableField(1)] public readonly CustomTroopInfo Info;

        // Type of troop, at time of death
        [SaveableField(2)] public readonly string TroopType;


    }

    public class CustomTroopNameManager {
        private Dictionary<string, List<CustomTroopInfo>> _troopNameMapping =
            new Dictionary<string, List<CustomTroopInfo>>();

        private List<DeadTroopInfo> _troopGraveyard =
            new List<DeadTroopInfo>();

        public void TroopRecruited(CharacterObject unit, string customName) {
            AddTroop(unit, new CustomTroopInfo(customName));
        }

        private void AddTroop(CharacterObject unit, CustomTroopInfo newTroop) {
            var unitName = unit.Name.ToString();
            if (_troopNameMapping.TryGetValue(unitName, out var troops)) {
                troops.Add(newTroop);
            }
            else {
                _troopNameMapping.Add(unitName, new List<CustomTroopInfo>() {newTroop});
            }
        }

        public void TroopUpgraded(CharacterObject oldType, CharacterObject newType) {
            if (!_troopNameMapping.TryGetValue(oldType.Name.ToString(), out var troops)
                || troops.Count == 0) {
                return;
            }

            var troopInfo = troops[0];
            troops.Remove(troopInfo);

            AddTroop(newType, troopInfo);
            InformationManager.DisplayMessage(new InformationMessage(
                ($"{troopInfo.Name} has been promoted to {newType.Name}")));
        }

        public void TroopDied(BasicCharacterObject type, CustomTroopInfo troopInfo) {
            _troopNameMapping.TryGetValue(type.Name.ToString(), out var troops);
            if (troops == null || !troops.Remove(troopInfo)) {
                Debug.WriteLine(
                    $"ERROR - didn't find {type.Name} to mark {troopInfo.Name} as dead");
            }
            _troopGraveyard.Add(new DeadTroopInfo(troopInfo, type.Name.ToString()));
        }

        // Clones the _troopNameMapping dictionary into a new one that will be mutated in order to assign the troops to agents in the battle handler
        public Dictionary<string, List<CustomTroopInfo>> GetTroopsToAssign() {
            return _troopNameMapping.ToDictionary(pair => pair.Key,
                pair => new List<CustomTroopInfo>(pair.Value));
        }

        public void PrintDebug(bool showGrave) {
            if (showGrave) {
                foreach (var deadTroop in _troopGraveyard) {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{deadTroop.Info.Name} - dead {deadTroop.TroopType} with {deadTroop.Info.Kills} kills"));
                }
            }
            else {
                foreach (var pair in _troopNameMapping) {
                    var troopName = pair.Key;
                    foreach (var troopInfo in pair.Value) {
                        var killInfo = troopInfo.Kills == 0 ? "" :
                            troopInfo.Kills == 1 ? "(1 Kill)" :
                            $"({troopInfo.Kills} Kills)";
                        InformationManager.DisplayMessage(new InformationMessage
                            ($"{troopName} {troopInfo.Name} {killInfo}"));
                    }
                }
            }
        }

        public void SyncData(IDataStore dataStore) {
            dataStore.SyncData("_troopNameMapping", ref _troopNameMapping);
            dataStore.SyncData("_troopGraveyard", ref _troopGraveyard);
        }
    }

    public class CustomTroopNamesCampaignBehavior : CampaignBehaviorBase {
        private readonly CustomTroopNameManager _troopManager =
            new CustomTroopNameManager();

        public CustomTroopNameManager TroopManager => _troopManager;

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
                    for (var i = 0; i < howMany; i++) {
                        _troopManager.TroopUpgraded(unit, newUnit);
                        Debug.WriteLine($"{unit.Name} digivolved to ${newUnit.Name}");
                    }
                });

        }

        public Dictionary<string, List<CustomTroopInfo>> GetTroopsToAssign() {
            return _troopManager.GetTroopsToAssign();
        }


        public void PrintDebug(bool showGrave) {
            _troopManager.PrintDebug(showGrave);
        }

        public override void SyncData(IDataStore dataStore) {
            _troopManager.SyncData(dataStore);
        }
    }

    public class CustomNameAgentComponent : AgentComponent {
        public readonly CustomTroopInfo TroopInfo;

        public CustomNameAgentComponent(Agent agent, CustomTroopInfo troopInfo) :
            base(agent) {
            this.TroopInfo = troopInfo;
        }
    }
}
