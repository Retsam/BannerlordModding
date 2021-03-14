using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace CustomTroopNames {
    public class CustomTroopNameManager {
        private Dictionary<string, List<CustomTroopInfo>> _troopNameMapping =
            new Dictionary<string, List<CustomTroopInfo>>();

        private List<DeadTroopInfo> _troopGraveyard =
            new List<DeadTroopInfo>();

        private readonly RecruitTroopInquiryManager _recruitTroopInquiryManager;

        private static readonly Random Rnd = new Random();

        public CustomTroopNameManager() {
            _recruitTroopInquiryManager = new RecruitTroopInquiryManager((unit, name) =>
                AddTroop(unit, new CustomTroopInfo(name)));
        }

        public void TroopRecruited(CharacterObject unit) {
            _recruitTroopInquiryManager.AddTextPrompt(unit);
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
                ($"{troopInfo.Name} has been promoted to {newType.Name}"),
                ModColors.MainColor));
        }

        private CustomTroopInfo RemoveRandomTroopIfNecessary(CharacterObject type, TroopRoster roster) {
            _troopNameMapping.TryGetValue(type.Name.ToString(), out var troops);
            if (
                troops == null
                || troops.Count == 0
                || roster.GetTroopCount(type) > troops.Count) return null;

            var removeIdx = Rnd.Next(troops.Count);
            var removed = troops[removeIdx];
            troops.RemoveAt(removeIdx);
            return removed;
        }

        public void TroopDied(BasicCharacterObject type, CustomTroopInfo troopInfo, string causeOfDeath) {
            _troopNameMapping.TryGetValue(type.Name.ToString(), out var troops);
            if (troops == null || !troops.Remove(troopInfo)) {
                Debug.WriteLine(
                    $"ERROR - didn't find {type.Name} to mark {troopInfo.Name} as dead");
            }

            _troopGraveyard.Add(new DeadTroopInfo(troopInfo, type.Name.ToString(), causeOfDeath));

            InformationManager.DisplayMessage(
                new InformationMessage($"{troopInfo.Name} DIES",
                    ModColors.AlertColor));
        }

        public void AnonymousTroopDied(BasicCharacterObject type, string causeOfDeath) {
            _troopNameMapping.TryGetValue(type.Name.ToString(), out var troops);
            if (troops == null || troops.Count == 0) return;
            // TODO determine randomly based on total number of troops of this class
            TroopDied(type, troops[0], causeOfDeath);
        }

        public void TroopDeserted(CharacterObject type, TroopRoster rosterBeforeDesertion) {
            var deserted = RemoveRandomTroopIfNecessary(type, rosterBeforeDesertion);
            if (deserted == null) return;
            // Adjust the roster in case multiple desertions happen simultaneously
            rosterBeforeDesertion.AddToCounts(type, -1);

            ModColors.AlertMessage($"{deserted.Name} has deserted!");
            _troopGraveyard.Add(new DeadTroopInfo(deserted, type.Name.ToString(), "deserted"));
        }

        // Clones the _troopNameMapping dictionary into a new one that will be mutated in order to assign the troops to agents in the battle handler
        public Dictionary<string, List<CustomTroopInfo>> GetTroopsToAssign() {
            return _troopNameMapping.ToDictionary(pair => pair.Key,
                pair => new List<CustomTroopInfo>(pair.Value));
        }

        // Ensure we don't have more named troops than actual troops
        public void CheckValid() {
            var roster = PartyBase.MainParty.MemberRoster;
            foreach (var pair in _troopNameMapping) {
                var troopName = pair.Key;
                var namedTroops = pair.Value;
                if (namedTroops.Count == 0) continue;

                var troopCount = 0;
                for (var i = 0; i < roster.Count; i++) {
                    var character = roster.GetCharacterAtIndex(i);
                    if (character.Name.ToString() != troopName) continue;
                    troopCount = roster.GetTroopCount(character);
                    break;
                }

                while (namedTroops.Count > troopCount) {
                    var troop = namedTroops[0];
                    ModColors.AlertMessage($"{troop.Name} vanishes in a puff of logic.");
                    _troopGraveyard.Add(new DeadTroopInfo(troop, troopName, "killed by programming error"));
                    namedTroops.RemoveAt(0);
                }
            }
        }


        public void PrintDebug(bool showGrave) {
            if (showGrave) {
                foreach (var deadTroop in _troopGraveyard) {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{deadTroop.Info.Name} - {deadTroop.TroopType} {deadTroop.CauseOfDeath} with {deadTroop.Info.Kills} kills",
                        ModColors.AlertColor));
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
                        ($"{troopName} {troopInfo.Name} {killInfo}",
                            ModColors.MainColor));
                    }
                }
            }
        }

        public void SyncData(IDataStore dataStore) {
            dataStore.SyncData("_troopNameMapping", ref _troopNameMapping);
            dataStore.SyncData("_troopGraveyard", ref _troopGraveyard);
        }
    }

    class RecruitTroopInquiryManager {
        private List<CharacterObject> _textPromptsToShow = new List<CharacterObject>();
        private Task _flushTextPromptsTask;
        private readonly Action<CharacterObject, string> _onTroopRecruited;

        public RecruitTroopInquiryManager(
            Action<CharacterObject, string> onTroopRecruited) {
            _onTroopRecruited = onTroopRecruited;
        }

        public void AddTextPrompt(CharacterObject unit) {
            _textPromptsToShow.Add(unit);
            if (_flushTextPromptsTask == null) {
                _flushTextPromptsTask = Task.Run(async delegate {
                    await Task.Delay(500);
                    FlushTextPrompts();
                });
            }
        }

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
                            _onTroopRecruited(unit, customName);
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
    }
}
