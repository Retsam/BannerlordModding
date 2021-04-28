using CustomTroopNames.Views;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
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
            if (!Input.IsKeyPressed(InputKey.Tilde)) return;

            var troopManager = Campaign.Current
                ?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()
                ?.TroopManager;
            if (troopManager == null) return;
            if (Input.IsKeyDown(InputKey.RightShift)) {
                if (Input.IsKeyDown(InputKey.RightControl)) {
                    troopManager.PrintAway();
                } else {
                    troopManager.PrintGrave();
                }
            } else {
                if (!(ScreenManager.TopScreen is TroopsScreen)) {
                    ScreenManager.PushScreen(ViewCreatorManager.CreateScreenView<TroopsScreen>());
                }

                // troopManager.PrintTroops();
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
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this,
                (party, hero) => {
                    if (hero != Hero.MainHero) return;
                    var captor = party.Leader?.Name.ToString() ?? party.Name.ToString();
                    TroopManager.PartyWipe($"taken captive by {captor}");
                });
        }

        public override void SyncData(IDataStore dataStore) {
            TroopManager.SyncData(dataStore);
        }
    }
}
