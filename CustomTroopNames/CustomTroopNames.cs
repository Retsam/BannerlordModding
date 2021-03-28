using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SandBox.GauntletUI;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.MountAndBlade.View.Screen;
using Debug = System.Diagnostics.Debug;

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
    [GameStateScreen(typeof(PartyState))]
    public class CtnPartyVm : GauntletPartyScreen {

        private Dictionary<CharacterObject, int> _troopTransferCounts =
            new Dictionary<CharacterObject, int>();

        private static void AttachDoneHandler(PartyScreenLogic logic, Action<TroopRoster> doneHandler) {
            var prevDelegate = logic.PartyPresentationDoneButtonDelegate;
            // Prepends a delegate: the last delegate determines the final return value: prepending to avoid that responsibility.
            logic.PartyPresentationDoneButtonDelegate = (roster, prisonRoster,
                memberRoster, rightPrisonRoster, prisonerRoster,
                releasedPrisonerRoster, forced, parties, rightParties) => {
                doneHandler(memberRoster);
                return true; // Doesn't matter, won't be the last delegate.
            };
            logic.PartyPresentationDoneButtonDelegate += prevDelegate;
        }

        public CtnPartyVm(PartyState partyState) : base(partyState) {
            var customTroopBehavior =
                Campaign.Current.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()?.TroopManager;
            if (customTroopBehavior == null) return;

            // Used when changes are confirmed to determine whether to remove named troops or not
            // Mutated to mark changes (so don't use the real one!)
            var originalRosterCopy = TroopRoster.CreateDummyTroopRoster();
            originalRosterCopy.Add(PartyBase.MainParty.MemberRoster);

            AttachDoneHandler(PartyScreenManager.PartyScreenLogic, (memberRoster) => {
                foreach (var pair in _troopTransferCounts) {
                    for (var i = 0; i < Math.Abs(pair.Value); i++) {
                        if (PartyScreenManager.PartyScreenLogic.LeftOwnerParty == null) {
                            if (pair.Value < 0) {
                                Debug.WriteLine("Negative troop counts while transferring nowhere?");
                            }
                            customTroopBehavior.TroopAbandoned(pair.Key, originalRosterCopy);
                        } else {/* transfer */}

                    }
                }
                Debug.WriteLine("DONE");
            });
            // var isTransferToSettlement =
            //     PlayerEncounter.LocationEncounter?.Settlement?.GetComponent<Fief>()?
            //         .GarrisonParty.Party == logic.LeftOwnerParty;
            // logic.PartyScreenClosedEvent += (party, roster,
            //     prisonRoster, ownerParty, memberRoster, rightPrisonRoster) => {
            //     Debug.WriteLine("Closed party screen");
            // };

            PartyScreenManager.PartyScreenLogic.Update += command => {
                // TODO: check RecruitTroop case?
                if (command.Code != PartyScreenLogic.PartyCommandCode.TransferTroop && command.Code != PartyScreenLogic.PartyCommandCode.RecruitTroop)
                    return;

                _troopTransferCounts.TryGetValue(command.Character, out var prevCount);
                _troopTransferCounts[command.Character] =
                    command.RosterSide == PartyScreenLogic.PartyRosterSide.Right
                        ? prevCount + command.TotalNumber
                        : prevCount - command.TotalNumber;
            };

            PartyScreenManager.PartyScreenLogic.AfterReset += screenLogic => {
                _troopTransferCounts = new Dictionary<CharacterObject, int>();
            };

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
