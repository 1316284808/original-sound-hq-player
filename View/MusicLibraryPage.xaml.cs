using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.ApplicationModel.DataTransfer;
using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.ViewModel;
using ZLinq;

namespace WinUIMusicPlayer.View
{
    /// <summary>
    /// 音乐库页：音乐来源管理与播放列表的合并入口。
    /// 来源区与 AddFolderViewModel（DI 单例）共用状态，拖放行为与原来源页一致；
    /// 歌单点击预设 CurrentPlayList 后进入 PlayListPage 详情（OnNavigatedTo 自动恢复）。
    /// </summary>
    public sealed partial class MusicLibraryPage : Page
    {
        private static readonly ILogger<MusicLibraryPage> _logger = App.GetLogger<MusicLibraryPage>();

        public AddFolderViewModel ViewModel { get; }
        public PlayListViewModel PlayListViewModel { get; }

        public MusicLibraryPage()
        {
            InitializeComponent();
            ViewModel = App.Services.GetRequiredService<AddFolderViewModel>();
            PlayListViewModel = App.Services.GetRequiredService<PlayListViewModel>();
            DataContext = this;
            Loaded += (_, _) => ViewModel.Activate();
            Unloaded += (_, _) => ViewModel.Deactivate();
        }

        // 收藏入口：内置列表磁贴，进入 MusicBrowsePage 收藏子页（favourite 无导航项，栏内保持"音乐库"选中）
        private void FavouriteEntry_Click(object sender, RoutedEventArgs e) =>
            App.Services.GetRequiredService<MainPage>().NavigateToMusicBrowseSubPage("favourite");

        // 歌单点击：预设当前歌单后导航 PlayListPage，OnNavigatedTo 按状态进入详情模式
        private void PlayListGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is not PlayList playList) return;
            PlayListViewModel.EnterPlayList(playList);
            App.Services.GetRequiredService<MainPage>().NavigateToPlayListPage();
        }

        private async void AddPlayList_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = App.Services.GetRequiredService<MainPage>();
            var playlistName = await mainPage.AddPlayListDialog.ShowAndGetNameAsync(this.XamlRoot);
            if (playlistName is not null)
            {
                PlayList newPlaylist = new() { Name = playlistName };
                await PlayListViewModel.InsertPlayList(newPlaylist);
                PlayListViewModel.AppViewModel.AllPlayList.Add(newPlaylist);
            }
        }

        // 拖放属视图层交互：DragOver/DragLeave 只驱动 DropOverlay 瞬时反馈，
        // Drop 提取文件夹后转发 ViewModel.DropFoldersAsync（含 Loading 状态与入库逻辑）。

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            if (!ViewModel.IsScanning && e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Link;
                DropOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
                DropOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            // DragLeave 已表示离开目标；不再用页面坐标二次判断，避免提示残留。
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        private void DropTarget_Unloaded(object sender, RoutedEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            if (!ViewModel.IsScanning && e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                try
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    // 筛选出文件夹
                    var folders = items.AsValueEnumerable().Where(item => item.IsOfType(Windows.Storage.StorageItemTypes.Folder)).ToList();

                    if (folders.Count > 0)
                    {
                        await ViewModel.DropFoldersAsync(folders);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Grid_Drop 拖放文件夹失败: {ex.Message}");
                }
            }
        }
    }
}
