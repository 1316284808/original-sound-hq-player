using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinUIMusicPlayer.Helper;
using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.ViewModel;

namespace WinUIMusicPlayer.View.Controls;

/// <summary>
/// 播放列表卡片：封面 / 名称 / 曲目数 + 悬停操作（播放、重命名、导出、删除）。
/// 由音乐库页与播放列表页共用，避免两处各维护一套卡片模板与处理逻辑。
/// </summary>
public sealed partial class PlayListGridCardControl : UserControl
{
    public static readonly DependencyProperty PlayListProperty = DependencyProperty.Register(
        nameof(PlayList), typeof(PlayList), typeof(PlayListGridCardControl), new PropertyMetadata(null));

    public PlayList? PlayList
    {
        get => (PlayList?)GetValue(PlayListProperty);
        set => SetValue(PlayListProperty, value);
    }

    private PlayListViewModel ViewModel { get; }

    public PlayListGridCardControl()
    {
        ViewModel = App.Services.GetRequiredService<PlayListViewModel>();
        InitializeComponent();
    }

    private void OnCoverPointerEntered(object sender, PointerRoutedEventArgs e) => SetActionButtonsVisibility(Visibility.Visible);

    private void OnCoverPointerExited(object sender, PointerRoutedEventArgs e) => SetActionButtonsVisibility(Visibility.Collapsed);

    private void SetActionButtonsVisibility(Visibility visibility)
    {
        PlayBtn.Visibility = visibility;
        MoreBtn.Visibility = visibility;
    }

    private void PlayPlayList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PlayList playList)
        {
            _ = ViewModel.PlayPlayList(playList);
        }
    }

    private async void RemovePlayList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is PlayList playList
            && await DialogHelper.ShowConfirmAsync(XamlRoot, "AreUSureDeletePlayList"))
        {
            await ViewModel.RemovePlayList(playList);
        }
    }

    private void EditPlayListName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is PlayList playList)
        {
            _ = ViewModel.AppViewModel.EditPlayListName(playList,
                () => DialogHelper.ShowInputAsync(XamlRoot, "ModifyPlaylist", playList.Name));
        }
    }

    private void ExportPlayList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is PlayList playList)
        {
            _ = ViewModel.ExportPlayList(playList);
        }
    }
}
