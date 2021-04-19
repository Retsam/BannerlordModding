using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using SandBox.GauntletUI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade.View.Screen;

namespace CustomTroopNames.Views {
    [UsedImplicitly]
    [GameStateScreen(typeof(PartyState))]
    public class PartyScreenOverride : GauntletPartyScreen {

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

        public PartyScreenOverride(PartyState partyState) : base(partyState) {
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
                            if (pair.Value > 0) {
                                customTroopBehavior.TroopAbandoned(pair.Key, originalRosterCopy);
                            }
                        } else {
                            var partyName = PartyScreenManager.PartyScreenLogic
                                // c.f. LeftOwnerParty.Name
                                .LeftPartyName.ToString();
                            if (pair.Value > 0) {
                                customTroopBehavior.TroopLeavesParty(pair.Key, originalRosterCopy, partyName);
                            } else {
                                customTroopBehavior.TroopReturnsToParty(pair.Key, partyName);
                            }
                        }

                    }
                }
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
                if (
                    command.Type == PartyScreenLogic.TroopType.Prisoner
                    || !(command.Code == PartyScreenLogic.PartyCommandCode.TransferTroop
                        || command.Code == PartyScreenLogic.PartyCommandCode.RecruitTroop)
                )
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
}
