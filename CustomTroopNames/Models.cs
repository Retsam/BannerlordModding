using System.Collections.Generic;
using JetBrains.Annotations;
using TaleWorlds.SaveSystem;

namespace CustomTroopNames {
    public class DeadTroopInfo {
        public DeadTroopInfo(CustomTroopInfo info, string troopType, string causeOfDeath) {
            Info = info;
            TroopType = troopType;
            CauseOfDeath = causeOfDeath;
        }

        [SaveableField(1)] public readonly CustomTroopInfo Info;

        // Type of troop, at time of death
        [SaveableField(2)] public readonly string TroopType;

        [SaveableField(3)] public readonly string CauseOfDeath;
    }

    public class CustomTroopInfo {
        public CustomTroopInfo(string name) {
            Name = name;
        }

        [SaveableField(1)] public readonly string Name;

        [SaveableField(2)] public int Kills = 0;
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
}
