using JetBrains.Annotations;
using SandBox.GauntletUI;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;

namespace CustomTroopNames {
    internal static class ModColors {
        public static Color MainColor = new Color(0, .8f, 1);
        public static Color AlertColor = Colors.Red;
        public static void InfoMessage(string text) {
            InformationManager.DisplayMessage(new InformationMessage(text, MainColor));
        }

        public static void AlertMessage(string text) {
            InformationManager.DisplayMessage(new InformationMessage(text, AlertColor));
        }
    }

    [UsedImplicitly]
    public class CustomTroopsSubModule : MBSubModuleBase {

        protected override void OnGameStart(Game game, IGameStarter gameStarter) {
            base.OnGameStart(game, gameStarter);
            if (!(game.GameType is Campaign)) return;
            var campaignStarter = (CampaignGameStarter) gameStarter;
            campaignStarter.AddBehavior(new CustomTroopNamesCampaignBehavior());
        }

        public override void OnMissionBehaviourInitialize(Mission mission) {
            base.OnMissionBehaviourInitialize(mission);
            var customTroopsBehavior = Campaign.Current
                ?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>();
            if (customTroopsBehavior == null) return;
            mission.AddMissionBehaviour(
                new CustomTroopsMissionBehavior(customTroopsBehavior.TroopManager));
            mission.AddMissionBehaviour(new HighlightsMissionBehavior());
        }

        protected override void OnApplicationTick(float dt) {
            base.OnApplicationTick(dt);
            if (Input.IsKeyPressed(InputKey.Tilde)) {
                Campaign.Current
                    ?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()
                    ?.TroopManager.PrintDebug(Input.IsKeyDown(InputKey.RightShift));
            }
        }
    }

    public class CustomTroopNamesCampaignBehavior : CampaignBehaviorBase {
        public CustomTroopNameManager TroopManager { get; } =
            new CustomTroopNameManager();

        public override void RegisterEvents() {
            CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this,
                (unit, howMany) => {
                    for (var i = 0; i < howMany; i++) {
                        TroopManager.TroopRecruited(unit);
                    }
                });
            CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(this,
                (unit, newUnit, howMany) => {
                    for (var i = 0; i < howMany; i++) {
                        TroopManager.TroopUpgraded(unit, newUnit);
                    }
                });
            CampaignEvents.OnTroopsDesertedEvent.AddNonSerializedListener(this,
                (party, deserters) => {
                    if (party != MobileParty.MainParty) return;
                    var rosterBeforeDesertion = TroopRoster.CreateDummyTroopRoster();
                    rosterBeforeDesertion.Add(party.MemberRoster);
                    rosterBeforeDesertion.Add(deserters);
                    foreach(var troop in deserters.ToFlattenedRoster()) {
                        TroopManager.TroopDeserted(troop.Troop, rosterBeforeDesertion);
                    }
                });
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, () => {
                TroopManager.CheckValid();
            });
        }

        public override void SyncData(IDataStore dataStore) {
            TroopManager.SyncData(dataStore);
        }
    }

    public class CustomNameAgentComponent : AgentComponent {
        public readonly CustomTroopInfo TroopInfo;

        public CustomNameAgentComponent(Agent agent, CustomTroopInfo troopInfo) :
            base(agent) {
            TroopInfo = troopInfo;
        }
    }

    [UsedImplicitly]
    [OverrideView(typeof(BattleSimulationMapView))]
    public class CustomTroopBattleSimulationGauntletView : BattleSimulationGauntletView {
        private readonly BattleSimulation _battleSimulation;

        public CustomTroopBattleSimulationGauntletView(BattleSimulation battleSimulation)
            : base(battleSimulation) {
            _battleSimulation = battleSimulation;
        }

        protected override void CreateLayout() {
            base.CreateLayout();
            var customNameManager =
                Campaign.Current?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()
                    ?.TroopManager;
            if (customNameManager == null) return;
            // Wrap the BattleObserver here, after it's been set by SPScoreboardVM.Initialize
            _battleSimulation.BattleObserver =
                new BattleObserverWrapper(_battleSimulation.BattleObserver,
                    customNameManager);
        }
    }

    public class BattleObserverWrapper : IBattleObserver {
        private readonly IBattleObserver _wrappedObserver;
        private readonly CustomTroopNameManager _customTroopNameManager;

        public BattleObserverWrapper(IBattleObserver wrappedObserver,
            CustomTroopNameManager customTroopNameManager) {
            _wrappedObserver = wrappedObserver;
            _customTroopNameManager = customTroopNameManager;
        }

        public void TroopNumberChanged(BattleSideEnum side,
            IBattleCombatant battleCombatant,
            BasicCharacterObject character, int number = 0, int numberKilled = 0,
            int numberWounded = 0, int numberRouted = 0, int killCount = 0,
            int numberReadyToUpgrade = 0) {
            _wrappedObserver.TroopNumberChanged(side, battleCombatant, character, number,
                numberKilled, numberWounded, numberRouted, killCount,
                numberReadyToUpgrade);

            if (battleCombatant != PartyBase.MainParty) return;
            if (MapEvent.PlayerMapEvent == null) return; // Shouldn't hit this, but just in case

            var killerSide =
                MapEvent.PlayerMapEvent.GetMapEventSide(1 - MapEvent.PlayerMapEvent
                    .PlayerSide);
            var killerPartyName = killerSide.LeaderParty.Name.ToString();

            for (var _ = 0; _ < numberKilled; _++) {
                _customTroopNameManager.AnonymousTroopDied(character, $"killed in battle against {killerPartyName}");
            }
        }

        public void HeroSkillIncreased(BattleSideEnum side,
            IBattleCombatant battleCombatant,
            BasicCharacterObject heroCharacter, SkillObject skill) {
            _wrappedObserver.HeroSkillIncreased(side, battleCombatant, heroCharacter,
                skill);
        }

        public void BattleResultsReady() {
            _wrappedObserver.BattleResultsReady();
        }
    }
}
