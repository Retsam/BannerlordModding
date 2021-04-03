using JetBrains.Annotations;
using SandBox.GauntletUI;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.View.Missions;

namespace CustomTroopNames.Views {
    [UsedImplicitly]
    [OverrideView(typeof(BattleSimulationMapView))]
    public class BattleSimulationOverride : BattleSimulationGauntletView {
        private readonly BattleSimulation _battleSimulation;

        public BattleSimulationOverride(BattleSimulation battleSimulation)
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
