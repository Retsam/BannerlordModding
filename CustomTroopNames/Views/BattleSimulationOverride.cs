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
        private readonly HighlightsManager _highlightsManager = new HighlightsManager();
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

            if (
                !(battleCombatant is PartyBase party)
                || party != PartyBase.MainParty // Only care about our troops
                || !(character is CharacterObject charObj) // Not sure what case this is.  Player/Companion kills?
                || MapEvent.PlayerMapEvent == null // Shouldn't hit this, but just in case
            ) return;

            var troopCount = party.MemberRoster.GetTroopCount(charObj);

            // Handle troop deaths
            if (numberKilled > 0) {
                var killerSide =
                    MapEvent.PlayerMapEvent.GetMapEventSide(1 - MapEvent.PlayerMapEvent
                        .PlayerSide);
                var killerPartyName = killerSide.LeaderParty.Name.ToString();
                // The troop count is already adjusted, so adjust to get the original count
                var totalTroops = troopCount + numberKilled;

                for (var i = 0; i < numberKilled; i++) {
                    var deadTroop = _customTroopNameManager.AnonymousTroopDied(character,
                        totalTroops - i, $"killed in battle against {killerPartyName}");
                    if (deadTroop != null) {
                        _highlightsManager.TroopDied(deadTroop);
                    }
                }
            }

            // Award troop kills
            if (killCount <= 0) return;
            var troopGettingKill =
                _customTroopNameManager.GetRandomTroopForType(character, troopCount);
            if (troopGettingKill == null) return;
            troopGettingKill.Kills += killCount;
            for (var i = 0; i < killCount; i++) {
                // Not sure if we can figure out killedHero here or not
                // Probably not.
                _highlightsManager.TroopGetsKill(troopGettingKill.Name, null);
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
            _highlightsManager.ShowResults(_wrappedObserver);
        }

        public void TroopSideChanged(BattleSideEnum prevSide, BattleSideEnum newSide,
            IBattleCombatant battleCombatant, BasicCharacterObject character) {
            _wrappedObserver.TroopSideChanged(prevSide, newSide, battleCombatant, character);
        }
    }
}
