using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;
using WinUIMusicPlayer.Utils;

namespace WinUIMusicPlayer.ViewModel;

public partial class AppViewModel
{
    public string WindowBackgroundImagePath
    {
        get => State.Preferences.WindowBackgroundImagePath;
        set => State.Preferences.WindowBackgroundImagePath = value;
    }

    public double WindowBackgroundBlurAmount
    {
        get => State.Preferences.WindowBackgroundBlurAmount;
        set => State.Preferences.WindowBackgroundBlurAmount = value;
    }

    public string WindowBackgroundError { get; private set => SetProperty(ref field, value); } = string.Empty;

    [RelayCommand]
    private async Task ChooseWindowBackgroundAsync()
    {
        if (!State.Lifecycle.IsReady) return;
        try
        {
            var picker = new FileOpenPicker(App.MainWindow.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary
            };
            foreach (string extension in new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" })
                picker.FileTypeFilter.Add(extension);
            var result = await picker.PickSingleFileAsync();
            if (result is null || !State.Lifecycle.IsReady) return;
            // Re-selecting a repaired file at the same path must retry the failed load.
            if (WindowBackgroundError.Length != 0 && WindowBackgroundImagePath == result.Path)
                WindowBackgroundImagePath = string.Empty;
            WindowBackgroundError = string.Empty;
            WindowBackgroundImagePath = result.Path;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "选择主窗口背景图片失败");
            if (State.Lifecycle.IsReady) ReportWindowBackgroundError();
        }
    }

    [RelayCommand]
    private void ClearWindowBackground()
    {
        if (!State.Lifecycle.IsReady) return;
        WindowBackgroundImagePath = string.Empty;
        WindowBackgroundError = string.Empty;
    }

    public void ReportWindowBackgroundError() => WindowBackgroundError = ToolUtils.GetString("WindowBackgroundLoadError");
}
