using Microsoft.UI.Xaml.Controls;
using WinUIMusicPlayer.ViewModel.Controls;

namespace AppearanceUiRegression;

public sealed partial class RateControl : UserControl
{
    public DspSettingsViewModel ViewModel { get; }
    public ComboBox Combo => Rate;

    public RateControl(DspSettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Loaded += (_, _) => ViewModel.LoadSavedRate();
    }
}
