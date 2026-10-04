using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using WinRT;
using WinUIMusicPlayer.Helper;
using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.Services.NavigationService;
using WinUIMusicPlayer.Utils;
using WinUIMusicPlayer.ViewModel;

namespace WinUIMusicPlayer.View
{
    public sealed partial class PlayListPage : Page, INavigatable
    {
        public PlayListViewModel ViewModel { get; }

        public PlayListPage()
        {
            ViewModel = App.Services.GetRequiredService<PlayListViewModel>();
            this.InitializeComponent();
            DataContext = this;
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            ViewModel.AppViewModel.AllPlayList.CollectionChanged += OnAllPlayListChanged;
        }

        private void OnAllPlayListChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(UpdateState);
        }

        private void UpdateState()
        {
            bool hasPlayLists = ViewModel.AppViewModel.AllPlayList.Count > 0;
            EmptyGrid.Visibility = hasPlayLists ? Visibility.Collapsed : Visibility.Visible;
            PlayListGrid.Visibility = hasPlayLists ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ReceiveNavigationParameter(object parameter)
        {
            ViewModel.ReceiveNavigation();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            DetailView.ViewModel.IsClosingForTransition = false;
            ViewModel.ReceiveNavigation();
            UpdateState();
        }

        private void PlayListGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (DetailView.ViewModel.IsClosingForTransition) return;
            var gridView = sender as GridView;
            var item = gridView?.ContainerFromItem(e.ClickedItem)?.As<GridViewItem>();
            var coverBorder = FindCoverBorderInItem(item);
            if (coverBorder is not null)
            {
                ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate("PlaylistCover", coverBorder);
            }
            SetEntryTransitions();
            if (e.ClickedItem is PlayList playList)
            {
                ViewModel.EnterPlayList(playList);
            }
            if (coverBorder is not null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var anim = ConnectedAnimationService.GetForCurrentView().GetAnimation("PlaylistCover");
                    anim?.TryStart(DetailView.DetailCoverBorder);
                });
            }
        }

        private void PlayListGridView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// 返回音乐库页：歌单列表只在音乐库页呈现，这里直接退出详情模式并随导航离开本页。
        /// 不做返回动画——源卡片所在的网格会一起卸载（MainPage.NavigateTo 会清空后退栈，停留在本页将无路可退）。
        /// </summary>
        public void LeaveDetailForLibrary()
        {
            if (!ViewModel.IsInDetailMode) return;
            DetailView.ViewModel.IsClosingForTransition = true;
            if (ViewModel.AppViewModel.CurrentPlayList is not null)
            {
                ViewModel.AppViewModel.CurrentPlayList = null;
            }
            ViewModel.IsInDetailMode = false;
            DetailView.ViewModel.IsClosingForTransition = false;
        }

        public void RefreshDetailView() { }

        private static Border? FindCoverBorderInItem(GridViewItem? item)
        {
            if (item is null) return null;
            return FindVisualChild<Border>(item, b => b.Name == "CoverBorder");
        }

        private static T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? match = null) where T : DependencyObject
        {
            if (parent is null) return null;
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t && (match is null || match(t))) return t;
                var found = FindVisualChild(child, match);
                if (found is not null) return found;
            }
            return null;
        }

        private void SetEntryTransitions()
        {
            DetailView.OpacityTransition = TransitionCache.Slow;
            PlayListGrid.OpacityTransition = TransitionCache.Fast;
        }

        private async void AddPlayList_Click(object sender, RoutedEventArgs e)
        {
            var mainPage = App.Services.GetRequiredService<MainPage>();
            var playlistName = await mainPage.AddPlayListDialog.ShowAndGetNameAsync(this.XamlRoot);
            if (playlistName is not null)
            {
                PlayList newPlaylist = new() { Name = playlistName };
                await ViewModel.InsertPlayList(newPlaylist);
                ViewModel.AppViewModel.AllPlayList.Add(newPlaylist);
            }
        }
    }
}
