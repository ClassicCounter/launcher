using ReactiveUI;
using System.Reactive;

namespace CCLauncher_GUI.ViewModels
{
    public class MainContentViewModel : ReactiveObject
    {
        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }

        private string _currentVersion;
        public string CurrentVersion
        {
            get => _currentVersion;
            set => this.RaiseAndSetIfChanged(ref _currentVersion, value);
        }

        // TABS

        private int _currentTabIndex;
        public int CurrentTabIndex
        {
            get => _currentTabIndex;
            set => this.RaiseAndSetIfChanged(ref _currentTabIndex, value);
        }

        public ReactiveCommand<string, Unit> SelectTabCommand { get; }

        public MainContentViewModel()
        {
            SelectTabCommand = ReactiveCommand.Create<string>(SelectTab);
        }

        private void SelectTab(string tabName)
        {
            CurrentTabIndex = tabName switch
            {
                "news" => 0,
                "play" => 1,
                "servers" => 2,
                "settings" => 3,
                _ => 0
            };
        }
    }
}