using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screen;

namespace CustomTroopNames.Views {
    // public interface CustomStateHandler { }

    public class CustomState : GameState {
        public override bool IsMenuState => true;
    }

    public class CustomTroopInfoVm: ViewModel {
        public CustomTroopInfoVm(string troopKind, CustomTroopInfo info) {
            Name = info.Name;
        }
        [UsedImplicitly]
        [DataSourceProperty] public string Name { get; set; }
    }

    public class TroopsVm : ViewModel {
        public TroopsVm() {
            CurrentPartyList =
                Campaign.Current?.GetCampaignBehavior<CustomTroopNamesCampaignBehavior>()
                    ?.TroopManager?.GetTroopViews() ??
                new MBBindingList<CustomTroopInfoVm>();
        }

        [DataSourceProperty]
        [UsedImplicitly]
        public MBBindingList<CustomTroopInfoVm> CurrentPartyList { get; }

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

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _dataSource = new TroopsVm();
            _gauntletLayer = new GauntletLayer(100)
            {
                IsFocusLayer = true
            };
            AddLayer(_gauntletLayer);
            _gauntletLayer.InputRestrictions.SetInputRestrictions();
            _gauntletLayer.LoadMovie("CustomTroopsNameScreen", _dataSource);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        protected override void OnDeactivate()
        {
            base.OnDeactivate();
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
