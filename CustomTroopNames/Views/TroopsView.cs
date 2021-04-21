using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screen;

namespace CustomTroopNames.Views {
    // public interface CustomStateHandler { }

    public class CustomState : GameState {
        // public CustomStateHandler Handler { get; set; }

        public override bool IsMenuState => true;
    }

    // public interface CustomNavigationHandler : INavigationHandler {
    //     void OpenTroopsScreen();
    // }
    //
    // public class CustomNavigation : MapNavigationHandler, CustomNavigationHandler {
    //     private readonly Game _game;
    //
    //     public CustomNavigation() {
    //         _game = Game.Current;
    //     }
    //
    //     public void OpenTroopsScreen() {
    //         _game.GameStateManager.PushState(_game.GameStateManager.CreateState<CustomState>());
    //     }
    // }

    public class TroopsVM : ViewModel {
        [DataSourceProperty]
        private void CloseCustomScreen() {
            Game.Current.GameStateManager.PopState(0);
        }
    }

    [GameStateScreen(typeof(CustomState))]
    public class TroopsScreen : ScreenBase {
        private GauntletLayer _gauntletLayer;
        private readonly CustomState _customState;
        private TroopsVM _dataSource;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            _dataSource = new TroopsVM();
            _gauntletLayer = new GauntletLayer(100)
            {
                IsFocusLayer = true
            };
            AddLayer(_gauntletLayer);
            _gauntletLayer.InputRestrictions.SetInputRestrictions();
            _gauntletLayer.LoadMovie("ClanScreen", _dataSource);
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
