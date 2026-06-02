using Avalonia.Controls;
using System;
using Wauncher.ViewModels;

namespace Wauncher.Views.Controls
{
    public partial class InfoPanel : UserControl
    {
        public event EventHandler? CloseRequested;

        public InfoPanel()
        {
            InitializeComponent();
            DataContext = new InfoWindowViewModel();

            ClosePanelButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
