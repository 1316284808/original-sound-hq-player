using CommunityToolkit.Mvvm.ComponentModel;
using WinUIMusicPlayer.DesktopLyrics;
using WinUIMusicPlayer.Helper;
using WinUIMusicPlayer.Services;

namespace WinUIMusicPlayer.ViewModel.Pages
{
    public partial class MainViewModel : ObservableObject
    {
        public AppViewModel AppViewModel { get; }
        public WinUIMusicPlayer.State.AppState State => AppViewModel.State;
        public BassPlayerCommandService PlayerCommandService { get; }
        public MusicBrowseViewModel MusicBrowseVM { get; }
        public DesktopLyricsViewModel DesktopLyrics { get; }

        private bool _areOtherButtonsVisible;

        /// <summary>
        /// 等价于参考项目的 AreOtherButtonsVisible：侧边栏展开（菜单/其他按钮可见）时为 true。
        /// 后续可改为更贴合实际的“其他按钮是否可见”条件。
        /// </summary>
        public bool AreOtherButtonsVisible
        {
            get => _areOtherButtonsVisible;
            set
            {
                if (SetProperty(ref _areOtherButtonsVisible, value))
                {
                    OnPropertyChanged(nameof(NavButtonIcon));
                    OnPropertyChanged(nameof(NavButtonGlyph));
                }
            }
        }

        /// <summary>
        /// 关闭导航按钮图标：根据 AreOtherButtonsVisible 在 InHome / BackHome 间切换（参考项目配对）。
        /// </summary>
        public IconKind NavButtonIcon => AreOtherButtonsVisible ? IconKind.InHome : IconKind.BackHome;

        /// <summary>
        /// 汉堡按钮当前字形（= NavButtonIcon 对应的字符）：控件模板里不能写 x:Bind，
        /// 改用传统 Binding 消费本属性，字形仍以 IconService 为唯一真源。
        /// </summary>
        public string NavButtonGlyph => IconService.GetIconChar(NavButtonIcon);

        public MainViewModel(AppViewModel appViewModel, BassPlayerCommandService playerCommandService, MusicBrowseViewModel musicBrowseVM, DesktopLyricsViewModel desktopLyrics)
        {
            AppViewModel = appViewModel;
            PlayerCommandService = playerCommandService;
            MusicBrowseVM = musicBrowseVM;
            DesktopLyrics = desktopLyrics;
        }
    }
}
