using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screen;
using TaleWorlds.TwoDimension;

namespace CustomTroopNames.Views {
    // public interface CustomStateHandler { }

    public class CustomState : GameState {
        public override bool IsMenuState => true;
    }

    public class CustomTroopInfoVm: ViewModel {
        public CustomTroopInfoVm(string troopKind, CustomTroopInfo info) {
            Name = info.Name;
            TroopType = troopKind;
            Kills = $"{info.Kills} kills";
        }
        public CustomTroopInfoVm(DeadTroopInfo info): this(info.TroopType, info.Info) {}

        [UsedImplicitly]
        [DataSourceProperty] public string Name { get; set; }

        [UsedImplicitly]
        [DataSourceProperty] public string TroopType { get; set; }

        [UsedImplicitly]
        [DataSourceProperty] public string Kills { get; set; }
    }


    public class TroopsVm : ViewModel {
        public TroopsVm() {
            (CurrentPartyList, GraveyardList) =
                Campaign.Current?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()
                    ?.TroopManager?.GetTroopViews() ??
                (new MBBindingList<CustomTroopInfoVm>(), new MBBindingList<CustomTroopInfoVm>());
        }

        [DataSourceProperty]
        [UsedImplicitly]
        public MBBindingList<CustomTroopInfoVm> CurrentPartyList { get; }

        [DataSourceProperty]
        [UsedImplicitly]
        public MBBindingList<CustomTroopInfoVm> GraveyardList { get; }

        [DataSourceProperty]
        [UsedImplicitly]
        private void CloseCustomScreen() {
            ScreenManager.PopScreen();
        }
    }

    [GameStateScreen(typeof(CustomState))]
    public class TroopsScreen : ScreenBase {
        private GauntletLayer _gauntletLayer;
        private TroopsVm _dataSource;
        private SpriteCategory _questCategory;


        protected override void OnInitialize()
        {
            base.OnInitialize();
            _dataSource = new TroopsVm();
            _gauntletLayer = new GauntletLayer(100) {
                IsFocusLayer = true
            };
            _questCategory = UIResourceManager.SpriteData.SpriteCategories["ui_quest"];
            _questCategory.Load(UIResourceManager.ResourceContext, UIResourceManager.UIResourceDepot);
            _gauntletLayer.InputRestrictions.SetInputRestrictions();
            _gauntletLayer.LoadMovie("CustomTroopsNameScreen", _dataSource);
            AddLayer(_gauntletLayer);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
            _questCategory.Unload();
            _gauntletLayer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(_gauntletLayer);
        }

        protected override void OnFinalize()
        {
            base.OnFinalize();
            RemoveLayer(_gauntletLayer);
            _dataSource = null;
            _gauntletLayer = null;
        }
    }
}
