# 功能变更记录

新条目加在最上方。

## 2026-10-05 任务栏歌词：修复开启逐字歌词后歌词不显示

- `DesktopLyrics/DesktopLyricsWindow.xaml`：`RendererHost` 的 `ContentPresenter` 由 `Width="auto" HorizontalAlignment="Left" VerticalAlignment="Center"` 改回 `HorizontalAlignment="Stretch" VerticalAlignment="Stretch"`。`CanvasLyricsRenderer` 的内容是 `CanvasAnimatedControl`，无固有尺寸；在 `Width="auto"`+`Left/Center` 下 `ContentPresenter` 被压成 0×0，导致 `RebuildLine` 因 `width < 60` 提前返回而不绘制（文本渲染器因 `TextBlock` 有固有尺寸仍正常）。逐字渲染依赖满尺寸画布，故开启逐字后整行歌词消失。

## 2026-10-04 任务栏歌词：去掉系统阴影/圆角/细边框，按钮与尺寸微调

- `Helper/WindowHelper.cs`：新增 `RemoveWindowDecoration(hwnd)`，经 `DwmSetWindowAttribute` 关闭非客户区渲染（去掉 DWM 系统投影）、设 `DWMWA_WINDOW_CORNER_PREFERENCE=Donotround`、`DWMWA_BORDER_COLOR=None`，去除 Win11 默认圆角与 1px 细边框。
- `DesktopLyrics/DesktopLyricsWindow.xaml.cs`：`ApplyOverlayStyle()` 在 GWL_STYLE/`SetBorderAndTitleBar` 变更后调用 `RemoveWindowDecoration`（此前无边框窗仍在轮廓外合成柔和阴影，仅靠 XAML 去不掉）；窗口固定尺寸由 560 高随任务栏改为 **400×60**（`FixedWidth`/新增 `BarHeight`，底边对齐任务栏底部），移除 `MinBarHeight/MaxBarHeight/FallbackHeight`。
- `DesktopLyrics/DesktopLyricsWindow.xaml`：移除 `RootGrid` 的 `BorderBrush="Black" BorderThickness="1"`（此前那圈"最外层边框"是 XAML 自己画的）；新增 `Button` 样式 `Width/Height=20`、`Margin=2`、`FontSize=10`、`Padding=0`，4 个 `FontIcon` 字号 14→10；歌词 `ContentPresenter` `MinWidth=300` 保留。
- 验证：未实机确认（需确认轮廓阴影/圆角消失、透明背景正常、点击穿透与置顶不受影响）。

## 2026-10-04 桌面歌词改造为「任务栏歌词」条带

- 定位：不再浮动，打开即贴靠主任务栏（高度=任务栏高度、固定宽度 560、贴任务栏左侧 +8px）；仅横向任务栏贴靠，否则回退主屏工作区底部。`DesktopLyrics/TaskbarInfo.cs` 新增 `TaskbarInfoProvider.GetPrimary()`（`FindWindow("Shell_TrayWnd")` + `GetWindowRect` + `GetDpiForWindow` + `SHAppBarMessage(ABM_GETSTATE)` 自动隐藏判定），`Helper/WindowHelper.cs` 补 `FindWindow`/`GetWindowRect`/`RECT`/`GetDpiForWindow`/`SHAppBarMessage`/`MonitorFromWindow` 等原语。750ms `DispatcherQueueTimer` 周期复述顶（`SetWindowPos(HWND_TOPMOST, NOMOVE|NOSIZE|NOACTIVATE|NOOWNERZORDER)`）+ 任务栏矩形/HWND 变化才重新贴靠 + 自动隐藏收起时同步隐藏、弹出时 `LyricsSyncRequestBus.Request()` 重拉。参考实现：MusicBar（MIT，本地 `C:\Users\fly\Downloads\MusicBar-main`）。
- 布局：根 `Grid` 改为两列（歌词区 + 右侧按钮区）。新增 4 个控制按钮：上一曲/播放暂停/下一曲/关闭，绑定 `PlaybackCommands`（自带 `CanPlay`/`CanSwitch` 守卫，可用态自动同步）；图标复用 `BindUtils.PlayStatusToGlyphConverter` + `IconKind.Previous/Next` + 关闭字形；补 `AutomationProperties.Name`（新增资源键 `TaskbarLyricsPrevious`/`TaskbarLyricsClose`，7 语言）。
- 删除：窗口 `Pointer*` 拖动逻辑、`ApplyLock`/`ConfigureWindow`/`ApplyDefaultBounds`/`OnViewModelPropertyChanged` 的 `IsLocked` 分支、`_originalWindowStyle` 边界缓存、`OnAppWindowChanged` 写回边界、`OnWindowClosed` 的 `PersistBounds`；`DesktopLyricsViewModel` 的 `IsLocked`/`BoundsState`/`PersistBounds`/`EnsureBoundsLoaded`；`State/DesktopLyricsState.IsLocked`；`AppSettings`/`SaveSettings`/`SettingsSnapshotFactory` 的 `IsDesktopLyricsLocked`；`Model/SaveDesktopLyricsState.cs` + `Helper/DesktopLyricsStateJsonContext.cs`（删除）+ `MusicDatabaseService.Load/SaveDesktopLyricsState`（删除，连带移除状态文件锁与路径方法）；托盘「锁定」「重置边界」两项及 `TrayViewModel` 对应命令；`HotKeyService` 的 `ToggleDesktopLyricsLock`/`ResetDesktopLyrics` 注册与 `HotKeyHelper.ShortcutId`/`HotKeyState`/`AppViewModel.Settings`/`ShortcutsSettingsControl.xaml` 中对应项；`DesktopLyricsManager.ResetWindowBounds`。
- **未删除（易误删）**：主窗口的 `AppViewModel.ResetWindowBoundsCommand` → `SettingsActions.ResetWindowBounds`（→ `CenterOnScreen`）与 `GeneralSettingsControl.xaml` 的 `ResetWindowBounds`、resw `ResetWindowBounds.Content`，属主窗口功能，与歌词无关。
- 用户可见文案：「桌面歌词」分区标题/托盘子菜单标题统一经 `IconDesktopLyrics.Text` 改为「任务栏歌词」（7 语言）。
- 验证：构建通过（0 错误）。人工实测项：贴靠（高度/宽度/左偏移/跟随任务栏高度/分辨率变化）、z 序（切应用/最大化/开始菜单后仍在任务栏之上且不抢焦点）、按钮（4 个点击生效、播放图标随态切换、无曲目禁用、移开恢复穿透）、穿透（光标不在按钮上时任务栏图标/开始菜单/托盘可点）、自动隐藏同步、高 DPI 125%/150% 无偏移。

## 2026-10-04 WebDAV 两个对话框补上圆角

- `View/SubView/WebDavConnectionDialog.xaml`（添加/编辑来源）、`View/SubView/WebDavBrowserDialog.xaml`（浏览目录）：根元素补 `CornerRadius="10"`，与项目内其它对话框（`AddPlayListDialog`、`EqualizerDialog`、`SettingsDialog`、`ProgressDialog`、`ConvolutionCurveDialog`、`UpdateHistoryDialog`）一致。
- 原因：这两个对话框是后加的，未沿用其它对话框的 `CornerRadius`，走了 WinUI 默认（`OverlayCornerRadius`=8 且模板实际未生效于该内容结构），看起来就是直角。

## 2026-10-04 修复：全部歌曲页右键菜单丢失

- `View/SongListPage.xaml`：`ListViewItemStyle` 补回 `ContextFlyout`（`MenuFlyout` + `extensions:MenuFlyoutExtensions.PrepareCommand="{x:Bind ViewModel.PrepareMenuCommand}"` / `ItemsSource="{x:Bind ViewModel.MenuOptions, Mode=OneWay}"`），并重新加回 `xmlns:extensions`。
- 原因：此前把容器样式从 `DefaultListViewItemStyle` 换成 `MusicRowListViewItemStyle` 时，只保留了 `Margin`，`ContextFlyout` 的 Setter 连同 `xmlns:extensions` 一起被删掉，右键只剩 `MusicListView_RightTapped` 里的选中逻辑，菜单无从触发。ViewModel 侧（`SongListViewModel.MenuOptions` / `PrepareMenuCommand`）一直是好的，属纯 XAML 接线丢失。

## 2026-10-03 浏览页搜索框：亮色主题下输入光标不可见的规避

- `View/MusicBrowsePage/MusicBrowsePage.xaml`：`SearchAutoSuggestBox` 的背景由 `Transparent` 改为 `{ThemeResource TextControlBackground}`，并在 `AutoSuggestBox.Resources` 里用 `ResourceDictionary.ThemeDictionaries` 按主题给出「近乎透明」的底色：亮色 `#0AFFFFFF`、暗色 `#0A000000`（Alpha=0x0A≈4%，视觉上等同透明，但 Alpha≠0）；四个 `TextControlBackground*` 都要给，因为 PointerOver/Focused 视觉状态会重新赋值。边框画刷仍为全透明。
- 原因：WinUI 已知 bug [microsoft-ui-xaml#9005](https://github.com/microsoft/microsoft-ui-xaml/issues/9005)（已 closed as not planned）——TextBox/RichEditBox 背景 Alpha=0 时，输入光标（caret）按背景反色计算退化成白色，亮色主题下与背景同色而看不见；暗色主题下正好相反所以能看见闪烁。
- 验证：未实机确认，需确认亮色下光标变深、暗色下仍为浅色，且底色加 4% 白/黑后不可察觉；若光标仍是白色，把 Alpha 再调大（如 `#1A`）即可。

## 2026-10-03 浏览页搜索按钮：展开/折叠时高度不再变化

- `View/MusicBrowsePage/MusicBrowsePage.xaml`：搜索按钮加 `Height="35"`（与同行 `MenuButtonStyle` 按钮的 `MinHeight` 一致）、`Padding="8,0"`、`VerticalContentAlignment="Center"`；`SearchAutoSuggestBox` 加 `MinHeight="0"`，并在其 `Resources` 里把 `TextControlThemeMinHeight` 压到 30（内嵌 `TextBox` 的默认最小高度来自该主题资源，不压下来即使外层设了 `Height="30"` 仍会被顶到 32 左右）。
- 原因：按钮此前没有固定高度，折叠时高度由搜索图标（约 19px）+ 默认内边距决定，展开后由输入框决定，两者不等于是出现高度抖动；宽度变化是预期内的（`Width` 绑定 `IsSearchExpanded`）。

## 2026-10-03 浏览页搜索框：常态/悬浮/聚焦都保持透明背景

- `View/MusicBrowsePage/MusicBrowsePage.xaml`：`SearchAutoSuggestBox` 在 `AutoSuggestBox.Resources` 里本地覆盖 `TextControlBackground`、`TextControlBackgroundPointerOver`、`TextControlBackgroundFocused`、`TextControlBackgroundDisabled` 以及四个 `TextControlBorderBrush*` 为 `Transparent`。
- 原因：`AutoSuggestBox` 的背景主要由内嵌 `TextBox` 绘制，而默认模板在 PointerOver/Focused 等视觉状态里会用 `TextControlBackground*` 重新赋值；视觉状态的优先级高于样式 Setter 和元素本地值，所以只写 `Background="Transparent"`（原本就有）在聚焦时会被覆盖回来。
- 影响范围：只作用于该控件实例及其模板子树（资源沿视觉链向上查找），不影响页面其它 `TextBox`/`AutoSuggestBox`。

## 2026-10-03 侧边栏汉堡按钮：改为自定义模板，图标随开合切换 InHome / BackHome

- `View/MainPage.xaml`：`NavigationView` 新增 `PaneToggleButtonStyle`，自包含 `Style`（无 `BasedOn`——`NavigationViewPaneToggleButtonStyle` 这个键不存在，写 `BasedOn` 会在加载时抛 `XamlParseException: Cannot find a Resource with the Name/Key ...`），用自写 `ControlTemplate` 渲染单个 `FontIcon`（`Glyph` 绑定 `ViewModel.NavButtonGlyph`，字体 `Segoe Fluent Icons`，16 号，含 Normal/PointerOver/Pressed/Disabled 四个视觉状态）；样式同时显式给出 `Height=40`、`Padding/BorderThickness=0`、`Foreground=NavigationViewItemForeground`、`CornerRadius`、`UseSystemFocusVisuals`，不依赖基样式默认值。
- `ViewModel/Pages/MainViewModel.cs`：新增 `NavButtonGlyph`（= `IconService.GetIconChar(NavButtonIcon)`），与 `NavButtonIcon` 一起在 `AreOtherButtonsVisible` 变化时通知；字形仍以 `IconService` 为唯一真源（展开 `InHome` = `\uE10F`，折叠 `BackHome` = `\uE72B`）。
- `View/MainPage.xaml.cs`：图标切换的触发源由 `PaneOpened`/`PaneClosed` 事件改为 `NavigationView.IsPaneOpen` 的属性变化回调（`RegisterPropertyChangedCallback`，令牌在 `MainPage_Unloaded` 注销）。`PaneOpened`/`PaneClosed` 是 `SplitView` 开合动画结束后才转发的，用它驱动会让图标在动画结束时才变（肉眼可见滞后）；`IsPaneOpen` 在点击瞬间翻转，且同样覆盖选中项后自动收起、轻触消失等入口。
- `View/MainPage.xaml.cs`：删除 `UpdatePaneToggleIcon()` 及其两处调用——原实现按名字 `PaneToggleButton` 在视觉树里找按钮并替换内部 `FontIcon`，而 WinUI 模板里该按钮名为 `TogglePaneButton` 且内容是 `TextBlock`（`PaneTitleTextBlock`），查找必然落空、图标从未生效；现在由模板声明式完成。`CollapsePaneCloseButton()` 保留不动（按 `PaneCloseButton` 查找，实际模板名为 `NavigationViewCloseButton`，同为无效调用，未在本改动范围内处理）。
- 验证：模板内无法使用 `x:Bind`（`ControlTemplate` 无 `x:DataType`），`Glyph` 走继承 `DataContext`（页面 → `ViewModel`）的传统 `Binding`；宽度仍来自 `NavigationView` 模板上的 `MinWidth`（绑定 `TemplateSettings.SmallerPaneToggleButtonWidth`），高度由本样式给出 40。未做设备上实机确认，需确认两个字形在 Segoe Fluent Icons 中存在（缺失会显示空白/豆腐块）。

## 2026-10-03 顶栏新增亮/暗主题切换按钮

- `View/MainPage.xaml`：`AppTitleBar` 右侧 `TopRightButtons` 中第一个按钮 `ThemeToggleTitleBarButton`（`TitleBarButtonStyle`），命令绑定 `AppViewModel.ToggleThemeCommand`，图标为两个 `FontIcon`（`helper:IconGlyph.IconKind="LightMode"/"DarkMode"`）按 `IsDarkMode` 切换 Opacity（与最大化按钮同样的做法），显示的是点击后进入的模式。
- `Helper/IconKind.cs` / `Helper/IconService.cs`：新增 `LightMode`（`\uF08C`）、`DarkMode`（`\uF0CE`）。
- `Services/SettingsActions.cs`：新增 `OnToggleTheme`（按当前生效的 `IsDarkMode` 在 `"Light"`/`"Dark"` 间切换 `ThemeType`）；`ViewModel/AppViewModel.Settings.cs` 暴露 `ToggleThemeCommand`。应用与保存仍走既有 `SettingsCoordinator` 的 `ThemeType` 分支；跟随系统（`Default`）时按系统当前深浅决定目标。
- `Utils/BindUtils.cs` 与 `Strings/*/Resources.resw`：新增提示文案转换 `ThemeToggleTextConverter` 与资源键 `ThemeToggleToLight` / `ThemeToggleToDark`（7 种语言）。

## 2026-10-03 按钮悬浮/按下底色统一为默认 Button 的取值

- `Style/BtnStyle.xaml`：`MenuButtonStyle`、`PlayButtonStyle` 的 PointerOver / Pressed 底色由 `AppBarButtonBackgroundPointerOver` / `Pressed`（= `SubtleFillColorSecondary/Tertiary`，暗色 6%/4% 白、浅色 3.5%/2.4% 黑，极淡）改为 `ButtonBackgroundPointerOver` / `Pressed`（= `ControlFillColorSecondary/Tertiary`，暗色 8%/3% 白、浅色 50%/30% 浅灰），即无样式按钮（如返回按钮）的取值；常态底色 `AppBarButtonBackground`（透明）与前景色不变。
- `Style/BtnStyle.xaml`：`CircularButtonStyle` 的悬浮底色由 `SubtleFillColorTertiaryBrush`（比常态还淡）改为 `ButtonBackgroundPointerOver`，按下改为 `ButtonBackgroundPressed`。
- 未改动：`SimpleHoverButtonStyle` / `PlayHoverButtonStyle`（封面上的黑色叠加 + 箭头，另有用途）、`NoHoverButtonStyle`（刻意无悬浮）、以及使用 WinUI `SubtleButtonStyle` 的按钮（如 `App.xaml` 的 `LibraryCardActionButtonStyle`）——后者悬浮仍为 `SubtleFillColor*`，要一起统一需改它们的样式或全局覆盖 `SubtleFillColor*Brush`。

## 2026-10-03 歌曲行高统一：三处详情/最爱列表与歌曲列表页一致

- `App.xaml`：新增共享样式 `MusicRowListViewItemStyle`（`BasedOn` 默认样式），集中定义 `CornerRadius=8`、横向/纵向 `Stretch`、`Padding="16,5,12,5"` 与重写模板（默认模板未把 CornerRadius 暴露给 `ListViewItemPresenter`，需显式 TemplateBinding 才能圆角化选中/悬停背景）。纵向 5 + `MusicListRowControl` 的 `MinHeight=50` = 60，与 `SongListPage` 行高一致（此前三处用默认 `Padding="16,0,12,0"`，行高只有 50，视觉比歌曲列表页矮 10px）。
- `View/Controls/MusicGroupDetailControl.xaml`、`View/Controls/PlaylistDetailControl.xaml`、`View/FavouritePlayListPage.xaml`：各自的 `ListViewItemStyle` 改为 `BasedOn="{StaticResource MusicRowListViewItemStyle}"`，只保留各自的右键菜单 `ContextFlyout`，删除三份重复的模板（约 47 行 × 3）。
- `View/SongListPage.xaml`：列表容器改为 `BasedOn` 同一个共享样式（+ `Margin="0,0,15,0"`），删除内联容器样式里手写的 `#4DFFFFFF`（悬浮）/`#80FFFFFF`（选中）视觉状态——这两项是硬编码白色，浅色主题下几乎不可见，也和其它列表的系统画刷不一致；歌曲行的 `BorderBrush/BorderThickness` 无渲染效果，一并移除。现在四处歌曲列表的悬浮/选中/按下/禁用底色全部来自 `ListViewItemBackground*` / `ListViewItemForeground*` 主题画刷。
- `App.xaml`：`MusicRowListViewItemStyle` 模板里 `ListViewItemPresenter` 的 `PointerOverBackground` / `SelectedBackground` / `SelectedPointerOverBackground` / `SelectedPressedBackground` 由主题画刷改为固定白色叠加 `#4DFFFFFF`（悬浮，30%）与 `#80FFFFFF`（选中，50%），即 SongListPage 原来的取值；现在四处歌曲列表共用同一套强调色。前景色（`PointerOverForeground` / `SelectedForeground`）与按下态仍走主题画刷。
- 兼容性：三处详情/最爱列表行高由 50 变为 60，悬浮/选中高亮由系统画刷改为上面的白色叠加，与歌曲列表页原表现一致。注意白色叠加在浅色主题下对比很弱（原 SongListPage 亦如此），要改成随主题变化就把这几个值换回 `ThemeResource` 或改用 `ThemeDictionaries`，改一处即可全局生效。

## 2026-10-03 专辑卡片：改为横版圆角卡片（左封面 + 右侧信息 + 悬浮播放）

- `View/Controls/AlbumGridCardControl.xaml`：卡片由「150 方封面 + 下方专辑名 / 歌手 / 曲目数」改为参考项目 `AlbumControl` 的 280×120 横版卡片（`CornerRadius=8`）；左侧 120×120 封面（`x:Name="CoverBorder"` 保留，供 `AlbumPage` 的 ConnectedAnimation 查找），封面图内缩 10px 装在 100×100 / `CornerRadius=8` 的内层 Border 里，四周露出包边；右侧专辑名（14 SemiBold / 最多 110 宽换行省略 / ToolTip）+「N 首」（沿用既有 `AlbumSongs` 资源键，与歌手卡片一致，不再单独显示歌手名）；播放按钮常驻悬浮在封面正中（30×30 / `CornerRadius=15`）。配色走系统主题画刷：卡片 `CardBackgroundFillColorDefaultBrush` + `CardStrokeColorDefaultBrush`，文字 `TextFillColorPrimaryBrush` / `TextFillColorSecondaryBrush`，封面包边 `SolidBackgroundFillColorSecondaryBrush`，按钮 `SubtleFillColorSecondaryBrush` + `ControlStrokeColorDefaultBrush`。WinUI 无 `Border.Effect`，参考项目的 `DropShadowEffect` 未移植。
- `View/Controls/AlbumGridCardControl.xaml.cs`：构造注入 `AlbumViewModel`，点击播放按钮调用 `PlayAlbum(Music)`；按钮 `Tapped` 标记 `Handled` 防止冒泡成 GridView 的 `ItemClick` 而同时进入详情。
- `ViewModel/Pages/AlbumViewModel.cs`：`Play()` 的取歌/排序/播放逻辑抽成 `public Task PlayAlbum(Music album)`（按 `TrackNumber` 排序不变），右键菜单与卡片播放按钮共用。

## 2026-10-03 歌手卡片：改为胶囊布局（左圆封面 + 右侧信息 + 悬停播放）

- `View/Controls/ArtistGridCardControl.xaml`：卡片由「150 圆形封面 + 下方居中歌手名」改为参考项目的 280×120 胶囊（`CornerRadius=60`）；左侧 120×120 圆形封面（`x:Name="CoverBorder"` 保留，供 `ArtistPage` 的 ConnectedAnimation 查找）：封面图内缩 10px 装在 100×100 / `CornerRadius=50` 的内层 Border 里单独裁圆，外圈 10px 黑色包边遮住裁切毛边；右侧歌手名（14 SemiBold / 最多 110 宽换行省略 / ToolTip）+「N 首」（沿用既有 `AlbumSongs` 资源键）；播放按钮常驻悬浮在封面正中（30×30 / `CornerRadius=15` / `#80FFFFFF`）。WinUI 无 `Border.Effect`，参考项目的 `DropShadowEffect` 未移植。
- `View/Controls/ArtistGridCardControl.xaml`：卡片配色改为本项目统一的系统主题画刷（不再沿用参考项目的固定浅色）——卡片底 `CardBackgroundFillColorDefaultBrush` + 描边 `CardStrokeColorDefaultBrush`（亮色白卡片 / 暗色灰卡片），歌手名 `TextFillColorPrimaryBrush`、曲目数 `TextFillColorSecondaryBrush`，封面外圈 `SolidBackgroundFillColorSecondaryBrush`，悬浮播放按钮 `SubtleFillColorSecondaryBrush` + `ControlStrokeColorDefaultBrush` 描边（并显式 `MinWidth/MinHeight/Padding/Margin=0`，避免全局隐式 Button 样式把 30×30 撑成 40×35），设备角标前景同样改为 `TextFillColorPrimaryBrush`。主题由 `ThemeStyleHelper` 设置窗口根元素 `RequestedTheme`，`ThemeResource` 自动跟随。
- `View/Controls/ArtistGridCardControl.xaml.cs`：构造注入 `ArtistViewModel`，点击播放按钮调用 `PlayArtist(Music)`；按钮 `Tapped` 标记 `Handled` 防止冒泡成 GridView 的 `ItemClick` 而同时进入详情。
- `ViewModel/Pages/ArtistViewModel.cs`：`Play()` 的取歌/排序/播放逻辑抽成 `public Task PlayArtist(Music artist)`，右键菜单与卡片播放按钮共用（行为不变：`CanStartPlayback` 守卫、按专辑 `CjkStringComparer` 排序、`IsChangeList: true`）。
- `Services/LibraryQueries.cs` / `ViewModel/AppViewModel.cs` / `Utils/BindUtils.cs`：新增歌手曲目数链路 `_artistSongCounts` → `GetArtistSongCount` → `ArtistSongsConverter`；计数在 `AddArtistIndexEntry` 内复用已拆分的歌手名累加，不增加额外扫描，同样跟随来源过滤。

## 2026-10-03 底部播放控制栏：MainPage 与播放详情页布局统一

- `View/MainPage.xaml`：底部控制栏按播放详情页 `ControlsStack` 的风格重整——左区保持 60 旋转封面 + 三行歌曲信息（标题 18 Bold / 专辑 12 / 歌手 12，去掉原先固定 16 高度避免截断，`MaxWidth 240`）；中区顺序与尺寸对齐详情页（收藏 18 / 进度数显 `已播放 / 剩余` / 上一首 18 / 播放暂停 32×26 / 下一首 18 / 停止 18）；右区按钮顺序改为 桌面歌词 → 均衡器 → 音量 → 播放模式 → 播放列表，`Margin=5`、图标 `FontSize=18`。旋转封面的 storyboard 目标元素 `AlbumCoverRotateTransform` 保持不变。
- `View/MainPage.xaml`：`NavigationView` 新增 `OpenPaneLength="216"` / `CompactPaneLength="48"`，收窄过宽的侧边导航项（此前用默认值 320）。

## 2026-10-03 返回路由：歌单详情与「最爱」子页一步回到音乐库页

- `View/MainPage.xaml.cs`：`HandleBackNavigation` 新增 `ReturnToMusicLibraryPage()`（后退栈在每次 `NavigateTo` 后被清空，只能重新导航）；`PlayListPage` 分支改为退出详情后直接回到音乐库页，不再退回本页的歌单浏览网格。
- `View/PlayListPage.xaml.cs`：`CollapseDetail()`（含返回 ConnectedAnimation）替换为 `LeaveDetailForLibrary()`——详情不再有停留场景，直接清 `CurrentPlayList` / 详情页详情态；随之删除不再使用的 SetExitTransitions。
- `View/MainPage.xaml.cs`：`NavigateToMusicBrowseSubPage` 记录子页来源 `_browsePageReturnToLibrary`（从音乐库页进入时为真），回来时先让浏览页消费详情态，否则回到音乐库页；`ViewModel/Pages/FavouritePlayListViewModel.cs`：`ReceiveNavigation` 由 `IsBackBtnEnable=false` 改为 `true`（最爱无独立导航项，入口在音乐库页）。
- `View/MusicBrowsePage/MusicBrowsePage.xaml.cs`：新增只读属性 `IsSubPageInDetailMode`（内容区子页是否处于详情态），供 MainPage 判断返回键由谁消费。

## 2026-10-03 音乐库页：两分区重排 + WebDAV 卡片与添加入口拆分

- `View/MusicLibraryPage.xaml`：页面重排为「音乐来源 / 播放列表」两个分区，去掉页头三个快捷入口按钮（最爱改作播放列表分区内置磁贴，全部歌曲与文件夹浏览分别有侧栏入口与来源行内的打开位置）；来源区标题只保留标题，卡片内各自显示数量。
- `View/SubView/WebDavSourcesControl.xaml`：WebDAV 改为与本地文件夹同规格的卡片容器（`App.xaml` 的 `LibraryCardStyle` / `LibraryGroupTitleStyle` / `LibraryCardActionButtonStyle`），行项目不再是独立小卡，标题行新增「+」= 新建网络来源（`Add_Click`）。
- `View/MusicLibraryPage.xaml`：本地文件夹卡片标题行新增「+」= 添加文件夹（绑定 `AddFolderCommand`），移除原「添加来源」DropDownButton 与空态里的重复按钮；删除图标由 `Share` 改为 `Delete`。
- `View/Controls/PlayListGridCardControl.xaml(.cs)`：新增共用的播放列表网格卡片（封面 + 悬停播放 / 更多菜单：重命名、导出、删除），`MusicLibraryPage` 与 `PlayListPage` 共用，后者的卡片模板与重复处理器随之删除。
- `App.xaml`：新增共享样式 `LibraryCardStyle` / `LibraryGroupTitleStyle` / `LibraryCardActionButtonStyle`（原本为音乐库页私有）。
- `Strings/*/Resources.resw`：新增独立资源键 `WebDavAddSource`（7 种语言），用于新建网络来源按钮的名称与提示。

## 2026-10-02 播放详情页：歌曲信息移至底部控制栏并可点击退出

- `View/PlayingDetailPage.xaml`：移除左侧面板下方的歌曲信息区（标题 / 专辑 / 歌手 / 采样率 / HQ 徽章，含 Win2D 动画文本与悬停滚动），左侧面板行定义简化为 `*,6*,50,*`；在底部控制栏左侧（设置按钮前）新增歌曲信息按钮：三行文本（标题 18 Bold / 专辑 12 / 歌手 12，与 MainPage 控制栏一致，MaxWidth 300 自动截断），点击等同标题栏 `CancelPlayingDetailButton`，退出播放详情页返回浏览页。设置 / 歌词偏移 / 均衡器三个按钮移至右侧控制组（播放列表 / 音量 / 播放模式之前）。
- `View/PlayingDetailPage.xaml.cs`：新增 `MusicInfoButtonPlayingDetail_Click`；清理对已移除元素的引用（Win2D 特效初始化、`AutoScrollHover_*` 处理器、`textScale`）；横竖屏布局同步调整——竖屏左侧面板简化为单列、封面居中，横屏行定义改为 4 行；`ChangeControlsFontSize` 仅保留歌词字号与布局切换。**注意**：播放详情“文本对齐”设置（`EffectivePlayingDetailAlignment`）不再有显示消费者，设置项暂保留。
- `ViewModel/Pages/PlayingDetailViewModel.cs`：删除无消费者的 `TitleFontSize` / `ArtistAlbumFontSize` / `InfoFontSize` 属性。

## 2026-10-02 右键菜单：修复 ListViewItem 应用样式时空引用崩溃

- `Extensions/MenuFlyoutExtensions.cs`：为 `SetPrepareCommand` / `SetItemsSource` 增加 `target`/`d` 为 null 时的防护。WinUI 3 在 `Style` 的 `Setter.Value` 内联 `MenuFlyout` 上对附加属性（`PrepareCommand`/`ItemsSource`）做 `x:Bind` 时，个别 `ListViewItem` 容器（虚拟化新项 / 回收项）会以 null 作为 `target` 调用这两个 setter，原代码直接 `target.SetValue` 触发 `NullReferenceException` 导致相关列表崩溃。

## 2026-10-02 歌曲列表选中项：高亮背景改为圆角 8

- `View/SongListPage.xaml` / `View/FavouritePlayListPage.xaml` / `View/Controls/PlaylistDetailControl.xaml` / `View/Controls/MusicGroupDetailControl.xaml`：在各自的 `ListViewItemStyle` 中重写 `ControlTemplate`，使用 `ListViewItemPresenter` 并显式 `CornerRadius="{TemplateBinding CornerRadius}"`（配合 `CornerRadius=8`）。WinUI 3 默认模板未将 `CornerRadius` 经 `TemplateBinding` 暴露给 `ListViewItemPresenter`，仅设 `CornerRadius` 属性对选中/悬停背景无效；重写后选中与悬停高亮真正圆角，并完整保留多选勾选框、拖拽/重排、按下/焦点等原生视觉，右键菜单（ContextFlyout）不受影响。

## 2026-10-02 浏览页大标题：动态显示当前路由项文本

- `ViewModel/AppViewModel.cs`：`PageType` 由原自动属性改为通知属性（CommunityToolkit `SetProperty`），新增计算属性 `PageTitle`；按 `PageType`（song/album/artist/folder/favourite，浏览态 `albumBrowse` 等去 `Browse` 后缀）映射到**新建的无后缀独立资源键**（`PageTitleAllSongs`/`PageTitleAlbumList`/`PageTitleArtistList`/`PageTitleFolder`/`PageTitleFavourite`），经 `ToolUtils.GetString` 读取。`GetString` 基于 `ResourceLoader` 仅可靠解析无后缀独立键，侧边栏用的 `.Text` 属性资源名（`AllSongs.Text` 等）直接传入会被原样返回（即显示键名），故改用独立键；该组键已写入 zh-CN/en/de/es/ja/ru/tr 七种语言（值取自对应 `.Text` 翻译）。
- `View/MusicBrowsePage/MusicBrowsePage.xaml`：大标题 `TextBlock` 由 `x:Uid="AllSongsTitle"`（资源键缺失导致一直空白）改为绑定 `ViewModel.AppViewModel.PageTitle`，随子页切换（全部歌曲/专辑列表/歌手列表/文件夹/最爱）动态更新。

## 2026-10-02 浏览页操作行：改用 Grid 列定义布局（修正 coldef 错误语法）

- `View/MusicBrowsePage/MusicBrowsePage.xaml`：将 `Grid.Row="1"` 操作行由 `Horizontal StackPanel` 改为 `Grid` + `ColumnDefinitions="Auto,Auto,Auto,Auto,*,Auto,Auto,Auto"`；每个控件用 `Grid.Column` 定位——`全部播放(0)/随机播放(1)/搜索按钮(2)/搜索框(3)` 紧贴左侧，第 4 列 `*` 为弹性空列把 `排序(5)/USB(6)/远程来源(7)` 推到最右；逐元素补 `VerticalAlignment="Center"` 避免 Grid 默认 Stretch 撑高。原 `coldef="auto,..."` 非 WinUI 语法已修正。

## 2026-10-02 浏览页顶栏：全部来源下拉并入操作行，Row=0 仅留大标题

- `View/MusicBrowsePage/MusicBrowsePage.xaml`：将 `UsbDeviceCombox`（USB 设备）与远程来源 `ComboBox` 从顶部 `Grid.Row="0"` 右侧移至 `Grid.Row="1"` 操作行，排在排序选择之后；`Grid.Row="0"` 现在**仅保留大标题**（如全部歌曲、音乐库、专辑列表、歌手列表等随导航切换的 `AllSongsTitle`），不再含任何操作控件。`x:Name="UsbDeviceCombox"` 保留，后台无引用，移动不影响功能。

## 2026-10-02 浏览页顶栏：排序选择并入操作行，与播放/搜索同排

- `View/MusicBrowsePage/MusicBrowsePage.xaml`：将 `SortByComboBox`（排序选择）从顶部 `Grid.Row="0"` 右侧移至 `Grid.Row="1"` 操作行，与「全部播放 / 随机播放 / 搜索」同一行。`x:Name="SortByComboBox"` 保留，后台无引用，移动不影响功能。

## 2026-10-02 标题栏：返回按钮与应用标题合并为单按钮（图标 + 名称）

- `View/MainPage.xaml`：将标题栏左侧独立的 `BackButton`（仅返回图标）与 `AppTitle` 文本块合并为同一个 `Button`，内部用横向 `StackPanel`（左 `FontIcon` 返回图标 + 右 `x:Uid="AppTitle"` 应用名）排布；`BackButton_Click`、`IsEnabled`、`ToolTip` 等行为保持不变。进度指示器（`ProgressRing` + 百分比）仍在该按钮之后独立显示。

## 2026-10-02 全局按钮基础样式：隐式 Button 样式统一兜底

- `App.xaml`：`Application.Resources` 新增隐式（无 `x:Key`）`Button` 基础样式，统一 `Margin=5 / MinHeight=35 / MinWidth=40 / FontSize=12 / CornerRadius=8 / Background=Transparent / BorderThickness=0`，对所有未显式指定样式的 `Button` 生效并保留默认模板；具名按钮样式（如 `SimpleHoverButtonStyle`/`PlayButtonStyle`/`MenuButtonStyle` 等）因显式赋值 `Style` 不受影响。注意：会同时作用于内置控件（ComboBox/DatePicker/NumberBox 等）内部的 `Button`。

## 2026-10-02 侧边栏“统计”导航项：补充选中态图标切换

- `Helper/IconKind.cs`：`Stats1` 标注为选中态实心字形（`\ued0d`），`Stats`（`\ued0c`）为未选中态。
- `View/MainPage.xaml.cs`：`NavIconMap` 的 `Stats` 由 `(Stats, Stats)` 改为 `(Stats, Stats1)`，使统计项点击选中后图标由 `\ued0c` 切换到 `\ued0d`，与其余导航项（Outline/Filled）的选中态切换行为一致。

## 2026-10-02 Mica 背景：默认更浓郁 + 修复属性未生效时序

- `Helper/CustomMicaSystemBackdrop.cs`：默认 `MicaKind` 由 `BaseAlt` 改为 `Base`（BaseAlt 是给侧栏等次级表面用的轻量变体，壁纸透得更多，看起来偏淡）；默认 `TintOpacity` 由 `0.01f` 改为 `0.8f`；新增 `FallbackColor`（Mica 不支持/未激活时回退的纯色，避免回退透明）。`SetMicaProperties` 改为在 `AddSystemBackdropTarget` 之前**同步**应用属性（原实现经 `DispatcherQueue.TryEnqueue` 异步入队、且在激活目标之后才入队，存在属性未落到控制器上的时序风险），并补设 `FallbackColor`。
- 说明：Mica 本质为掺入桌面壁纸的半透明材质，不会变成纯实心色；若需完全浓郁、可自定义的纯色背景，请改用应用内已有的 `CustomAcrylicStyle`（Acrylic + `TintOpacity=1.0` + `LuminosityOpacity=0`）。

## 2026-10-02 播放详情页控制栏：进度条独立成行、时间数显紧随收藏、移除快进/快退10秒

- `View/PlayingDetailPage.xaml`：进度条（`ProgressSliderPlayingDetail`）拆为独立第一行，移除原两侧 `ElapsedTimeText`/`RemainingTimeText` 文本；第二行播放按钮区移除 `FastBackwardButton` 与 `FastForwardButton`（快退/快进 10 秒），改用 6 列布局（收藏 / 时间数显 / 上一首 / 播放暂停 / 下一首 / 停止）。进度时间数显（`ElapsedTimeText` + `/` + `RemainingTimeText`）以 `StackPanel` 放到收藏按钮后面。
- 说明：`AppViewModel.FastBackwardButtonCommand` / `FastForwardButtonCommand` 现仅在此页引用，移除按钮后命令变为未被引用（保留定义未删，避免改动范围扩大）；如需彻底清理可一并删除。

## 2026-10-02 标题栏：新增“返回上一页”按钮、移除 AppLogo、修正最小化图标

- `View/MainPage.xaml`：移除左侧 `AppLogo` 图标（`Image Source=".../icon.ico"`）；在同位置新增“返回上一页”按钮（`BackButton`，字符图标 `IconKind.Back`），与 `AppTitle` 合并为始终显示；`IsEnabled` 绑定 `AppViewModel.IsBackBtnEnable`（一级页面禁用=点击无反应，二级页面启用=返回上一页），去掉原先的 `Visibility` 绑定（不再按层级隐藏）。布局对齐参考项目 `TitleBarControl.xaml` 的左侧（返回图标 + 标题）。另将最小化按钮误用的 `VolumeAlt` 图标改为正确的 `Minimize` 字符图标。
- `View/MainPage.xaml.cs`：新增 `BackButton_Click`，调用既有 `HandleBackNavigation()`（覆盖 PlayListPage 详情收起与 MusicBrowsePage 返回）。
- `Helper/IconKind.cs` / `Helper/IconService.cs`：新增 `Minimize` 成员并映射字形 `E949`（ChromeMinimize）。
- 说明：参考项目 `TitleBarControl` 为 WPF（`CustomIcon` + `Style.Triggers`），本处用现有 `IconGlyph.IconKind` + ViewModel 状态绑定等价实现；窗口控制按钮（全屏/最小化/最大化）沿用本项目既有交互，仅修正最小化图标字形。

## 2026-10-02 浏览页搜索框改为可折叠（参考项目 PlaylistControl 效果）

- `View/MusicBrowsePage/MusicBrowsePage.xaml`：将原本常驻的 `AutoSuggestBox` 改为「搜索图标按钮（`AppViewModel.SearchToggleCommand`）+ 可折叠 `AutoSuggestBox`」结构；`AutoSuggestBox` 的 `Width`/`Visibility` 绑定到 `AppViewModel.IsSearchExpanded`（经 `BoolToDoubleConverter`/`BoolToVisibilityConverter`）。`x:Uid="SearchMusic"` 保留，占位符仍走本地化资源。
- `ViewModel/AppViewModel.cs`：新增 `IsSearchExpanded`（折叠/展开状态，跨页保持）与 `SearchToggleCommand`（首次点击展开、再次点击清空 `SearchText` 并收起）。
- `View/MusicBrowsePage/MusicBrowsePage.xaml.cs`：订阅 `AppViewModel.PropertyChanged`，`IsSearchExpanded` 变 true 时通过 `DispatcherQueue` 聚焦 `SearchAutoSuggestBox`。
- `Converters/BoolToVisibilityConverter.cs` / `Converters/BoolToDoubleConverter.cs`：新增两个转换器，并在 `Style/ConverterDictionary.xaml` 注册（`BoolToVisibilityConverter`、`BoolToDoubleConverter`）。
- 说明：参考项目 WPF 的 `Style.Triggers` 占位符在 WinUI 中改由 `AutoSuggestBox.PlaceholderText`（经 `x:Uid` 资源）原生实现；聚焦用 `Focus(FocusState.Keyboard)` 替代 WPF 的消息机制。

## 2026-10-02 侧边栏：保留汉堡按钮（换图标）+ 搜索框移至浏览页

- `View/MainPage.xaml`：保留默认 `IsPaneToggleButtonVisible`（汉堡按钮直接用于开合导航栏），仅隐藏返回按钮 `IsBackButtonVisible="Collapsed"`；从 `NavigationView` 移除原 `AutoSuggestBox`，不再在侧边栏显示搜索框。
- `View/MainPage.xaml.cs`：窗格开合（`PaneOpened`/`PaneClosed`）时，通过视觉树替换默认汉堡按钮（`PaneToggleButton`）内部 `FontIcon` 字形为 `InHome`/`BackHome`（按 `AreOtherButtonsVisible` 切换），并折叠默认的收起箭头按钮（`PaneCloseButton`）以避免重复。
- `View/MusicBrowsePage/MusicBrowsePage.xaml`：将搜索框（`AutoSuggestBox`，绑定 `AppViewModel.SearchText`）移到“全部播放 / 随机播放”按钮之后（同处一行；同时将该行 `Grid.Row` 由 0 修正为 1，使其位于标题行下方，修正原本与标题重叠的问题）。
- `ViewModel/Pages/MainViewModel.cs`：新增 `AreOtherButtonsVisible`（侧边栏展开时为 true）与计算属性 `NavButtonIcon`（在 `InHome`/`BackHome` 间切换）。
- `Helper/IconKind.cs` / `Helper/IconService.cs`：新增 `InHome`/`BackHome` 两个成员（**字形为占位** `\uE10F` / `\uE72B`，需按参考项目调整）。
- 说明：参考项目的 `CustomIcon` + `Style.Triggers` 为 WPF 写法，WinUI 3 不支持；此处改用 `IconGlyph.IconKind` + ViewModel 计算属性等价实现。汉堡按钮图标与 `AreOtherButtonsVisible` 判定条件（当前取 `IsPaneOpen`）后续可按参考项目微调。

## 2026-10-02 字符图标集中化：批量替换剩余 XAML 的 Glyph 字面量

- 在 `Helper/IconKind.cs` 与 `Helper/IconService.cs` 补充 36 个成员及映射，覆盖此前散落在 24 个 XAML 文件中的 111 处静态 `Glyph="&#xXXXX;"` 字形（含 `Music/Image/Save/More/Close/Import/...` 及少数含义待定、暂以 `GlyphE...` 命名的成员）。
- 通过脚本将 24 个 XAML 文件中全部静态字形改为 `helper:IconGlyph.IconKind="..."` 引用，缺失 `xmlns:helper` 的文件自动补上命名空间；`StatsPage` 原显式 `SymbolThemeFontFamily` 让位于统一的 Fluent 字体。
- 说明：仅替换静态字面量；`Glyph="{x:Bind ...}"` 等动态绑定（如收藏/来源/WebDAV 树节点）保持原样不变。集中后所有字形字形唯一真源在 `IconService`；`GlyphE...` 占位名可在后续按语义改名。

## 2026-10-02 集中式字符图标服务（IconKind + IconService）

- `Helper/IconKind.cs`（新增）：字符图标类型枚举，作为图标的唯一类型来源。
- `Helper/IconService.cs`（新增）：静态 `GetIconChar(IconKind)`，集中所有 Segoe Fluent Icons 字形（`\uXXXX` 字面量唯一真源）。
- `Helper/IconGlyph.cs`（新增）：WinUI `FontIcon` 附加属性 `IconGlyph.IconKind`，XAML 用枚举名引用图标并自动套用 Segoe Fluent Icons 字体，`<FontIcon helper:IconGlyph.IconKind="Play"/>`。
- `Utils/BindUtils.cs`：播放/暂停、播放模式、收藏、音量、全屏、音乐来源、锁定、桌面歌词、逐字、存在设备共十余处字形字面量收敛到 `IconService`，消除散落硬编码。
- `View/MainPage.xaml` + `View/MainPage.xaml.cs`：侧边栏 5 个导航项与设置项改用 `IconGlyph.IconKind`；新增 `ApplyNavIconSelectionStates`（订阅 `SelectionChanged`）实现选中态图标随变（沿用参考项目 MusicPlayer 的字形方向：选中=空心）。仅示范迁移，其余 XAML 的 `Glyph` 字面量留待后续逐一替换。

## 2026-10-02 侧边栏导航重组：全部歌曲/音乐库/歌手列表/专辑列表

- `View/MainPage.xaml`：NavigationView 菜单重组为"全部歌曲、音乐库、歌手列表、专辑列表、统计"（+设置），替代原"音乐来源/音乐浏览/播放列表/统计"；图标选用 Audio/Library/People/唱片。
- `View/MainPage.xaml.cs`：`NavigationView_ItemInvoked` 改为新导航映射（歌曲/歌手/专辑项导航到 MusicBrowsePage 并切换对应子页）；新增 `NavigateToMusicBrowseSubPage`、`SyncNavigationSelection`（子页变化同步导航选中态，folder/favourite 保持"音乐库"选中）、`SetSelectedNavItemByTag`（按 Tag 查找，替换 `MenuItems[1]` 硬编码）；`NavigateToDefaultPage` 按新 tag 映射并兼容旧配置值。
- `View/MusicBrowsePage/MusicBrowsePage.xaml`：移除顶部 SelectorBar；标题改"全部歌曲"；顶栏第二行新增"播放全部/随机播放"按钮（Play/Shuffle 图标）；USB 设备/远程源/排序下拉维持原样。
- `View/MusicBrowsePage/MusicBrowsePage.xaml.cs`：删除 `SelectPage_SelectionChanged`/`ForceSelectorBarSelection`/`_syncingSelectorBar`；`SelectBarItem(tag)` 保留为侧边导航/启动恢复/交叉链接的统一入口。
- `ViewModel/Pages/MusicBrowseViewModel.cs`：`SelectedPage`（SelectorBarItem）重构为 `SelectedPageTag`（字符串 tag）驱动，`OnSelectionChanged` 按 tag 分发并回调 `MainPage.SyncNavigationSelection`；新增 `PlayAllCommand`/`ShufflePlayCommand`（队列=SongsSource，随机播放先切随机模式再替换队列）。
- `View/MusicLibraryPage.xaml/.cs`（新增）：音乐库页，合并原音乐来源管理（本地文件夹/WebDAV/拖放，绑定 AddFolderViewModel 单例）与播放列表（收藏、文件夹浏览入口置顶，歌单点击进入 PlayListPage 详情）。
- `View/AddFolderPage.xaml/.cs`（删除）、`WinUIMusicPlayer.csproj`：来源管理功能并入音乐库页，移除页面与注册条目。
- `View/SubView/Settings/GeneralSettingsControl.xaml`：默认启动页下拉改为新导航项（全部歌曲/音乐库/歌手列表/专辑列表/统计）。
- `State/GeneralPreferencesState.cs`、`Model/SaveSettings.cs`：默认启动页默认值改为 AllSongs。
- `Services/MusicDatabaseService.cs`：载入设置时将旧版启动页 tag（AddFolder/PlayLists→MusicLibrary、MusicBrowse→AllSongs）一次性迁移。
- `Strings/*/Resources.resw`：七语言新增 AllSongs/MusicLibrary/ArtistList/AlbumList/ShufflePlay 及启动页下拉键；清理 SelectorBar 与 AddFolderPage 遗留的无引用键。
- 兼容性：旧版"默认启动页"配置自动迁移到语义最接近的新导航项；"默认音乐页"（song/album/artist/folder/favourite）设置继续生效。
- 验证：x64 构建 0 错误；七语言新键与死键清理静态检查通过。UI 交互（导航选中态同步、cross-link、收藏/文件夹入口）待运行验证。

## 2026-10-02 底栏封面改为圆形旋转（对齐 WPF 参考）

- `View/MainPage.xaml`：收藏按钮封面改为 60×60 圆形（`CornerRadius=30` + 圆形 `RectangleGeometry` 裁剪），并加入 `AlbumCoverRotateTransform`。
- `View/MainPage.xaml.cs`：订阅 `AppViewModel.IsPlaying` 变化，播放时让封面以 90 秒/圈持续旋转，暂停/停止时 `Pause()` 保留当前角度，恢复播放时无缝衔接；页面加载时按当前状态初始化。

## 2026-10-02 底部播放栏布局对齐 WPF 参考（贯穿式进度条）

- `View/MainPage.xaml`：进度条改为贯穿式（独占顶部整行铺满）；进度时间/歌曲总时间紧随收藏按钮之后内联显示（与参考项目一致），不再与进度条同行；移除快进 10 秒、快退 10 秒两个按钮，其余按钮重新索引。
- `Style/SilderDictionary.xaml`：`PlaybackSliderStyle` 滑块默认隐藏，仅在鼠标悬停、按下或获得焦点时淡入显示（Opacity=0 仍可交互）。

## 2026-09-27 关于页版权年份改为动态

- `AboutSettingsControl.xaml`、`AboutSettingsControl.xaml.cs`：两处 `© 2026 Sennpei Studio` 硬编码改为 `x:Bind` 函数绑定，运行时取 `DateTime.Now.Year`，跨年无需发版更新。

## 2026-09-27 合并关于页缓存设置并统计缓存总量

- `AboutSettingsControl.xaml`：WebDAV 播放缓存并入同一个“缓存”展开卡片，移除独立控件和重复路径；保留位置、播放缓存开关/上限及两种清理入口。
- `CacheSizeCalculator.cs`、`WebDavSourcesViewModel.cs`：汇总当前缓存位置的网络封面、封面子目录及 WebDAV 子目录（含未完成下载），按容量显示 B/KiB/MiB/GiB；后台串行统计，换目录丢弃旧结果，清理后刷新。
- `Strings/*/Resources.resw`：七种语言区分封面与播放缓存清理按钮；用户原始音乐文件不计入，已有清理范围保持不变。
- 验证：x64 构建、缓存计数/目录切换/清理刷新/退出回归及七种语言资源键静态检查通过。

## 2026-09-27 修复封面缓存失败时丢失可用封面

- `ImageSwitcher.xaml.cs`、`ImageHelper.cs`：展示缓存生成或解码失败时直接读取原图，按 EXIF 方向限制最长边 1536px，取消不触发兜底。
- `CoverPresentationService.cs`、`SystemMediaControlsService.cs`：展示缓存失败保留原图，文件不可用时使用已取得的封面字节；正常热缓存仍只传路径，兜底沿用切歌取消与退出屏障。
- `_tools/SmtcCoverRegression`、`_tools/SmtcCoverUiRegression`：增加缓存发布/替换失败、原图写入及打开失败、限尺寸解码、字节兜底、取消与退出回归。
- 验证：主项目 x64 构建、真实 WIC/SMTC 与 WinUI 控件/展示管线回归通过；未进行完整播放器及系统媒体面板的人工视觉验证。

## 2026-09-27 SMTC 与详情页共享高分辨率封面

- `PlaybackCoverImage.cs`、`ImageSwitcher.xaml.cs`、`ImageHelper.cs`：详情页与 SMTC 共用高分辨率封面文件，大图按比例限制最长边 1536px，小图不放大；串行生成并原子发布缓存，直接从文件流解码。
- `CoverPresentationService.cs`、`SystemMediaControlsService.cs`、`ReadOnlyMappedStream.cs`：热缓存避免读取原图大数组；SMTC 克隆共享文件页，UI 队列只保留路径，清空缓存不破坏在途读取，退出等待全部更新并释放流。
- `ToolUtils.cs`、`WebDavLibraryService.cs`：高分辨率缓存纳入启动保留、远程容量裁剪和清理。
- 验证：真实 WIC/SMTC 与 ImageSwitcher 回归通过，覆盖 JPEG 方向、PNG 透明度、取消、快速切歌、删除缓存和退出；1536px 噪声测试图热提交的托管分配约 7.42MB → 40KB/次，10 次文件流提交无 GC，仅代表此路径；完整系统媒体面板外观与整机 GC 停顿未测。

## 2026-09-27 修复 1.2.9.0 以来审查问题并优化无缝预载

- `PlaybackEngine.Gapless.cs`、`GaplessPreloader.cs`：按剩余实际播放时间 10 秒延迟创建待播会话，未知时长提前准备；单个工作任务合并过期计划，统一迟到会话与退出清理，PCM 环容量不变。
- `PlaybackEngine.cs`：普通 DSP、设备校正及试听更新当前与待播会话，避免重复销毁/解码/分配；倍速、seek、暂停、输出重建及关闭无缝仍使准备失效。
- `PlaybackQueueState.cs`、`PlaybackCoordinator.Gapless.cs`、IPC：队列版本缓存候选，已确认计划不轮询重发；提交失败退避，取消回执确认曲目身份后再替换，保护手动选曲及迟到通知。
- `FFmpegAudioConverter.cs`：重采样器重建失败释放新原生上下文；`WebDavRegression.csproj` 改用完整共享 IPC 项目，修复回归工具缺失依赖。
- `TempoProcessor.cs`：明确输入帧、输出帧及搜索窗口命名；`Player/AudioPlayer.exe` 同步 NativeAOT 产物，存量偏好保持不变。
- 验证：317/317 播放回归、导航/远程/WebDAV TLS 回归、真实 WinUI 调度及主程序构建；NativeAOT 在真实 WASAPI 共享输出通过 0.25× / 5× 无缝衔接、暂停恢复与退出。未实测 ASIO/独占硬件及完整主界面交互。

## 2026-09-27 修复展开图片背景设置时闪退

- `GeneralSettingsControl.xaml`：将背景错误 InfoBar 移到 SettingsExpander.Items 外，避免展开时应用 SettingsCard 样式导致 COMException；无错误时隐藏提示。
- `_tools/AppearanceUiRegression`：直接提取生产 XAML，在真实 WinUI/Toolkit 模板中复现原异常并验证展开、反复折叠/展开及错误提示显示/隐藏；主程序构建通过。

## 2026-09-27 主窗口图片背景与倍速默认选择

- `MainWindow.xaml`、`Controls/WindowBackgroundImage.cs`：新增铺满窗口的自定义图片背景与 0–100 模糊；按需读取、限制解码尺寸，换图/清除/退出释放合成资源，缺失或损坏图片回退原窗口材质，高对比度下隐藏图片。
- `GeneralSettingsControl.xaml`、`AppViewModel.WindowBackground.cs`、设置状态/保存链路、`Strings/*`：常规设置新增选择/清除图片和模糊滑块/数值输入；保存路径与模糊值，旧设置保持原背景，默认模糊 20；补齐七种语言独立资源键。
- `DspSettingsViewModel.Playback.cs`：绑定初始化直接读取已保存倍速，恢复偏好期间忽略临时选择写回；无设置时默认选中 1×，保留已有档位和旧自定义倍速。
- 验证：主程序构建、真实 WinUI 倍速绑定/图片合成与生命周期回归、设置持久化及七种语言资源检查；完整设置页、图片选择对话框与系统高对比度切换未做人工验证。

## 2026-09-27 固定倍速档位

- `DspSettingsControl.xaml`、`DspSettingsViewModel.Playback.cs`：倍速改为不可编辑下拉框，提供 0.25、0.5、0.75、1、1.5、2、3、4、5×；避免显示浮点尾数，旧自定义倍速保留至用户重新选择。
- `DspSettings.cs`、`PlaybackSnapshots.cs`、`RemotePlaybackService.cs`、`TempoProcessor.cs`：播放与遥测范围扩展为 0.25–5×，调整分析缓冲容量；同步七种语言说明及播放端产物。
- 验证：主程序与 NativeAOT 构建通过；九档真实解码时长、音调和 seek 重置通过；0.25× / 5× 的真实 WASAPI 无缝切歌、暂停恢复通过，七种语言取词键静态检查通过。设置页外观未做人工验证。

## 2026-09-27 无缝播放、保调倍速与动态压限

- `External/AudioPlayer/Playback`：预载下一首兼容本地 PCM，在同一输出回调内无停顿衔接；取消、暂停、seek、格式回退与退出保留明确的会话所有权。
- `Services/PlaybackCoordinator.Gapless.cs`、IPC：按真实队列及循环模式预载，带身份的切歌通知/进度同步曲目、歌词和统计；取消确认处理已经开始的切歌，手动选曲优先。
- `TempoProcessor.cs`、`DynamicsProcessor.cs`：加入 0.5–2× 保调倍速、声道联动软拐点压缩、补偿增益及 −1 dBFS 采样峰值限幅；进度与网络遥测按原曲时间计量。
- `DspSettingsViewModel`、`DspSettingsControl.xaml`、`Strings/*`：新增设置并保存偏好，补齐七种语言的独立资源键；旧配置默认 1×、压限关闭、无缝开启。不能 seek 的网络流与位流不启用倍速。
- `Player/AudioPlayer.exe`、共享协议：NativeAOT 产物同步更新；管道握手 v3，主程序与播放端须一同部署，DSP 设置兼容旧版本。
- 验证：313 项播放回归通过，另通过偏好持久化、远程恢复、真实 WinUI 调度及 NativeAOT + WASAPI 共享输出测试；九个新增取词键覆盖七种语言。尚未实测 ASIO/独占硬件及设置页视觉布局。

## 2026-09-27 AudioPlayer IPC 统一为持久 Named Pipe

- `External/BassPlayerIpc.Shared`：移除命令、进度、DSP 和设备校正的 MMF/信号量实现；统一版本化分帧、实例握手、有序确认、有界队列与取消/迟到响应的缓冲所有权。
- `External/AudioPlayer/PlayerIpcService.cs`、`StreamingServer.cs`、`Services/IpcService.cs`：常规命令与流媒体控制独立执行，进度/DSP 推送到本地缓存；关键通知保序，断开及退出等待在途 I/O 后释放资源。
- `External/BassPlayerIpc.Shared/Streaming.cs`、`Services/RemotePlaybackService.cs`：流媒体控制复用持久连接，分离状态查询、准备、seek/refresh 与播放控制；会话停止时关闭连接，不自动重放失败命令。
- `Player/AudioPlayer.exe`、播放端说明文档：更新 NativeAOT 分发产物；主程序与播放端必须同时更新，不兼容旧 MMF 传输，存量音频设置保持原格式。
- `_tools/PlaybackSwitchRegression`、`AudioPlayerSmokeTest`、`RemotePlaybackRegression`：迁移 IPC 用例并覆盖分帧、超时/取消、队列背压、跨进程快照与断开退出；305 项播放回归、远程恢复回归、真实 NativeAOT HTTP/DSF 集成及 WASAPI 共享输出冒烟通过。未执行 WinUI 交互、ASIO/独占硬件验证。

## 2026-09-26 旋转网格背景模糊随窗口尺寸缩放

- `External/AnimatedWin2dControls/AnimatedWin2dControls/Renderer/Background/RotatingMeshBackgroundRenderer.cs`：模糊半径按画布面积的平方根（几何平均边长）同比缩放，以 1920×1080 时的原有效果为基准，避免小窗口过度模糊；横竖屏同面积下模糊强度一致，按宽度缩放会导致竖屏相对失准。裁切、网格和其他渲染参数保持不变。

## 2026-09-26 修复 OpenList 网盘歌曲误报变化及保存来源卡顿

- `Services/WebDav/WebDavTransport.cs`、`RemoteResourceVersion.cs`、`HttpRangeReadStream.cs`、`RemoteReadSession.cs`：区分 DAV 目录与重定向下载资源的版本，修复播放、标签及封面读取误报“远程文件已变化”；在同一读取中继续校验下载 ETag、修改时间和长度，缺 ETag 不再误判 Range 不支持。
- `Services/WebDav/RemoteReadSession.cs`：下载版本无法对应目录强版本时停止自动缓存补全且不发布音频磁盘缓存，保留有界内存播放及定位，避免把直链数据错误归入目录版本缓存。
- `Services/WebDavLibraryService.Sources.cs`、`WebDavLibraryService.cs`：凭据和数据库保存移到后台；新增来源由扫描批次发布曲目，省去保存时整库刷新；通知回到 UI 线程，退出等待在途保存且抑制迟到通知。
- `_tools/WebDavRegression`、`_tools/WebDavSaveUiRegression`：增加重定向版本/长度/Range 回归及真实 WinUI 凭据保存、UI 心跳、失败和退出收尾验证；真实 OpenList 目录 77 首元信息全部通过，抽样验证标签、封面、音频字节及实际 AudioPlayer 解码和定位。

## 2026-09-25 播放进度条右侧改显剩余时间

- `Services/PlaybackProgressService.cs`：滑块右侧文本由总时长改为剩余时间（总时长 − 当前进度，倒数归零），`curMs` 越过 `totalMs` 时钳到 0；SMTC 时间线仍上报总时长。
- `State/PlaybackState.cs`、`ViewModel/AppViewModel.cs`：`TotalTimeText` 更名 `RemainingTimeText`，初始值不变。
- `View/MainPage.xaml`、`View/PlayingDetailPage.xaml`：`PlayTimeTextBlock`/`PlayTimeTextBlockPlayingDetail` 绑定改指 `RemainingTimeText`。

## 2026-09-25 修复来源探活误停播放及待播期间暂停失效

- `ViewModel/Pages/WebDavSourcesViewModel.cs`：来源状态通知只更新来源展示，播放服务继续负责真实断流的停止与自动恢复，完整缓存播放不再被探活失败打断。
- `Services/PlaybackCommands.cs`：播放栏切换按钮实际请求暂停时取消待播选择，避免迟到的 WebDAV 探活再次启动歌曲。
- `_tools/PlaybackNavigationRegression`：覆盖来源通知、断流后自动切歌及探活期间暂停的交互回归。

## 2026-09-25 修正播放进度条滑块端点裁切

- `Style/SilderDictionary.xaml`：移除滑块负边距，使其留在 Slider 行程内；将轨道与已播放段对齐到滑块中心，修正两端显示。

## 2026-09-25 修正 WebDAV 续播时进度条短暂归零

- `Services/PlaybackCoordinator.cs`、`Services/BassPlayerCommandService.cs`：断流后的队列恢复耗尽时保留当前进度和失败状态，停止播放器时不清空进度条。
- `Services/RemotePlaybackService.cs`、`ViewModel/Pages/WebDavSourcesViewModel.cs`：来源离线导致会话停止后继续向进度轮询提供最后位置，续播会话接管后再切换快照；明确停止仍清除保留状态。
- `_tools/PlaybackNavigationRegression`、`_tools/RemotePlaybackRegression`：验证恢复耗尽、来源停止与显式停止时的进度行为。

## 2026-09-25 修复 WebDAV 断流后播放从头开始

- `Services/RemotePlaybackService.cs`、`Services/PlaybackCoordinator.cs`、播放命令入口：保留断流前最后一次解码进度，连接恢复后点击播放恢复同一首的当前位置；主动重新选曲仍从头开始。
- `External/BassPlayerIpc.Shared/Streaming.cs`、`External/AudioPlayer/Playback/PlaybackEngine.Streaming.cs`、`Player/AudioPlayer.exe`：准备远程流时携带起播位置，在解码器就绪前完成定位，并同步更新发布用播放器。
- `_tools/RemotePlaybackRegression`、`_tools/StreamingRegression`：覆盖断网、重连、恢复进度和主动选曲语义。

## 2026-09-25 修正播放进度条两侧时间文字对齐

- `View/MainPage.xaml`、`View/PlayingDetailPage.xaml`：移除时间文字的 6 px 下边距，使其与紧凑进度条的轨道垂直居中。

## 2026-09-25 收紧播放进度条垂直占位

- `Style/SilderDictionary.xaml`、`View/MainPage.xaml`：播放进度条模板高度从 44 px 收至 24 px，并收紧主播放栏最小高度，缩小两处进度条下方的留白。

## 2026-09-25 修正播放进度条外观并撤回不准确的缓冲显示

- `Style/SilderDictionary.xaml`、`View/MainPage.xaml`、`View/PlayingDetailPage.xaml`：恢复 HyPlayer 的 12 px 圆形滑块和中性色轨道，对齐两侧时间文字。
- `Services/RemotePlaybackService.cs`、`State/PlaybackState.cs`、`ViewModel/AppViewModel.cs`：撤回以解码器待播帧绘制的缓冲亮段；断网后解码仍可能消耗已下载字节，现有链路缺少可靠的下载字节到播放时间映射，避免误示网络缓冲。

## 2026-09-25 播放进度条移植 HyPlayer 布局并显示网络缓冲

- `View/MainPage.xaml`、`View/PlayingDetailPage.xaml`、`Style/SilderDictionary.xaml`：进度条采用已播时间、强调色滑块、总时长的三段布局；轨道叠加低亮度的网络缓冲进度。
- `State/PlaybackState.cs`、`ViewModel/AppViewModel.cs`、`Services/PlaybackProgressService.cs`、`Services/RemotePlaybackService.cs`：从远程状态的当前位置与已解码时长计算缓冲终点，按会话代次发布到 UI；选曲、失败和停止时清除旧缓冲。

## 2026-09-25 新增土耳其语界面

- `Strings/tr/Resources.resw`：新增土耳其语资源 783 条，键集合与 en 完全一致，占位符逐条校验无差异。
- `App.xaml.cs`：系统语言检测链新增 `tr` 分支，系统语言为土耳其语时自动切换应用语言。

## 2026-09-25 协议同意记录迁至文档目录

- `Services/AgreementAcceptanceStore.cs`：`agreement.json` 从本地应用数据目录（MSIX 下被虚拟化重定向，部分用户环境无法读写）迁至 `Documents\OriginalSoundPlayer\Agreement\`，与数据库/设置同根；不回退读取旧位置，升级后存量用户需重新确认一次协议。
- `Legal/legal.zh-CN.json`、`Legal/legal.en.json`：隐私文档中的存储位置声明同步更新。

## 2026-09-25 WebDAV 扫描无可见变更不再整库重建；本地来源行图标对齐

- `Services/MusicDatabaseService.WebDav.cs`：目录批次提交返回可见新增数（新插入 + 缺失复现）；目录/来源收尾标记返回本次转入缺失的行数（`Missing` 条件收紧为仅 0→1）。
- `Services/WebDavLibraryService.cs`：目录阶段结束的整库重载改为仅在存在可见增删时执行，服务端无变化时不再重建曲目列表（消除启动自动扫描结束时的一次闪烁）。
- `View/AddFolderPage.xaml`：本地文件夹行的重扫/移除按钮改为 34×34、图标字号 16、面板 Spacing 2，与 WebDAV 来源行一致。

## 2026-09-25 消除 WebDAV 启动扫描列表闪烁并常驻来源曲目数

- `Services/WebDavLibraryService.cs`：扫描批次发布不再逐批 `NotifySongsSourceChanged`（原先每个目录/每 64 首/每 16 条元数据整表 Reset 曲目列表，启动自动扫描期间持续闪烁），新增曲目沿用本地扫描的增量追加口径；元数据阶段实际写入后收敛一次排序与各页投影。
- `Services/MusicDatabaseService.WebDav.cs`：新增按来源统计可见远程曲目数（`Missing = 0`）的查询。
- `ViewModel/Pages/WebDavSourcesViewModel.cs`、`View/SubView/WebDavSourcesControl.xaml`：WebDAV 来源行常驻显示曲目数（与本地文件夹行同款式、复用 `FolderNumberOfSongs` 文案），加载时填充、扫描结束（完成/失败/取消）后刷新。

## 2026-09-24 分离 WebDAV 选中状态、缓存可播放性与断流恢复

- `PlaybackState.cs`、`PlaybackCoordinator.cs`、列表 ViewModel：待播行立即选中，实际播放信息延后提交；连续切歌取消旧等待，停止取消待播，旧结果不会覆盖新选择。
- `WebDavLibraryService.Availability.cs`、`WebDavAvailability.cs`：完整缓存优先于来源探活；来源失败后按单调时钟暂缓自动重试 15 秒，直接点选可立即重试，迟到探活不能抹掉新的断流状态。
- `RemoteReadSession.cs`、`RemotePlaybackService.cs`：缓存读取不取凭据、不联网；源端读失败发布来源状态并触发有界队列恢复，文件/解码错误不误报整台服务离线；当前会话与待播探测独立取消。
- `Music.cs`、`BindUtils.cs`、列表行和播放页：缓存歌曲在来源离线时保留 CloudDownload 图标、正常透明度和播放控制；停止按钮在离线状态仍可用。
- `_tools/PlaybackNavigationUiRegression`、`RemotePlaybackRegression`、`WebDavRegression`：验证真实 WinUI ListView 即时选中、实际读取链路断流恢复、离线缓存、缓存清除、重试期限和会话取消边界；播放协议端为可控测试端，未替代真实 NAS 与音频设备验证。

## 2026-09-24 修复 WebDAV 切歌探活并标记完整音频缓存

- `PlaybackCoordinator.cs`、`PlaybackCommands.cs`、`BassPlayerCommandService.cs`：上下首重新确认 WebDAV 实际在线状态；失败继续按队列方向寻找下一首，同次请求不重复探测失败来源，全部不可用时有界结束。
- `MusicCommands.cs`、`WebDavLibraryService.cs`：探活期间保留切歌入口，再按上下首从正在探测的歌曲继续，新选择立即取消旧选择的等待；共享探活不会被旧调用方取消而误报在线。
- `PlaybackCoordinator.cs`、`RemotePlaybackService.cs`：远程凭据、准备及缓存会话收尾在后台执行，避免同步磁盘操作拖住 UI；停止代次同步登记，迟到停止不覆盖新选曲。
- `RemoteAudioCache.cs`、`WebDavLibraryService.cs`、`Music.cs`、列表行及播放栏：完整落盘的当前版本音频显示 CloudDownload（EBD3），缓存清理、目录更换和版本变化同步更新；新增六语言提示词。
- `_tools/PlaybackNavigationRegression`、`PlaybackNavigationUiRegression`、`WebDavRegression`：增加失联/恢复、连续失败、取消、UI 调度和完整缓存状态回归。

## 2026-09-24 放开离线曲目手动播放并弱化列表行

- `Services/MusicCommands.cs`、`Services/PlaybackCommands.cs`、`View/MainPage.xaml`、`View/PlayingDetailPage.xaml`：手动播放按钮允许离线 WebDAV 曲目进入播放前探活流程，探活失败后仍停止播放；其它传输控制继续遵循在线状态守卫。
- `View/Controls/MusicListRowControl.xaml`、`Utils/BindUtils.cs`：离线歌曲行降低透明度但保留播放点击入口。
- `Utils/ToolUtils.cs`：右键“播放”保留为可用，转换、歌词、打开位置和 USB 等需要读取内容的操作继续置灰。

## 2026-09-24 抽取音乐列表/网格模板并同步 WebDAV 探活信息

- `View/Controls/MusicListRowControl.xaml`、列表页：将普通列表行、分组详情行和播放列表行抽成可配置行控件，保留外层 `ListView` 虚拟化与选择/右键行为。
- `View/Controls/*GridCardControl.xaml`、专辑/艺术家/文件夹页：按页面分别抽取 GridView 卡片模板，保留外层 `GridView` 的虚拟化和语义缩放。
- `ViewModel/Pages/WebDavSourcesViewModel.cs`：来源管理页在加载和探活状态变化时刷新 WebDAV 信息，离线状态不再只更新播放守卫而遗漏来源卡片。

## 2026-09-24 独立探测 WebDAV 在线状态并同步右键菜单

- `Services/WebDav/WebDavTransport.cs`、`WebDavLibraryService.cs`：使用 `PROPFIND Depth:0` 轻量探活，启动检查所有启用来源并按前台播放状态自适应轮询；在线状态不再依赖目录扫描结果。
- `Model/MenuModel.cs`、`Extensions/MenuFlyoutExtensions.cs`、`Utils/ToolUtils.cs`、各列表/网格 ViewModel：WebDAV 离线时将播放、转换、歌词、资源打开和 USB 发送等需要读取内容的右键操作置灰。
- `Services/PlaybackCoordinator.cs`、`ViewModel/Pages/WebDavBrowserViewModel.cs`：播放和 WebDAV 浏览前执行在线检查，避免探活状态过期后绕过离线守卫。

## 2026-09-24 标记离线 WebDAV 曲目并禁止播放

- `Services/WebDavLibraryService.cs`、`Model/Music.cs`：启动扫描失败时发布来源离线状态，同步到曲目运行时状态；恢复连接后清除。
- `View/*`、`Utils/BindUtils.cs`：离线来源标识改用 `F384`，列表播放入口和当前播放控制同步置灰。
- `Services/PlaybackCoordinator.cs`、`PlaybackCommands.cs`、`BassPlayerCommandService.cs`：在播放协调器、手动切歌和自动切歌路径统一跳过离线曲目。

## 2026-09-24 合并本地与 WebDAV 的同名专辑

- `Services/LibraryProjectionService.cs`、`ViewModel/Pages/AlbumViewModel.cs`、`MusicGroupDetailViewModel.cs`：本地与 WebDAV 同名专辑只显示一张卡片，进入详情后跨来源显示所有歌曲；不同来源的同名曲目也保留。
- `Services/LibraryQueries.cs`、`_tools/SharedStateRegression`：专辑歌曲数按来源分别计数，并增加跨来源专辑投影、详情和同名曲目回归检查。

## 2026-09-24 修复 WebDAV 网络源响度偏低

- `External/AudioPlayer/Playback/Session.cs`、`PcmEffects.cs`、`LoudnessScanner.cs`：WebDAV HTTP 会话纳入与本地文件相同的 EBU R128 后台响度分析；分析完成前使用中性增益，避免原先固定 −12 dB 保守衰减导致网络歌曲整体偏低。
- `External/BassPlayerIpc.Shared/Streaming.cs`、`Services/RemotePlaybackService.cs`：传递远程文件长度和 ETag 作为响度缓存版本，文件更新后自动重新分析；后台分析不阻塞首次播放，慢速网络仍可先播放。

## 2026-09-23 修复切歌时封面与 Win2D 资源持续累积

- `Controls/ImageSwitcher`、`PlayingDetailPage.xaml`：隐藏详情页时取消封面读取，原图解码宽度限制为 1536，过渡完成后释放上一张图源，并保留快速恢复所需的当前图像。
- `CoverLoadQueue`、`AlbumCoverBehavior`、`FadeImageBehavior`：缩略图任务共享像素数据而非共享 `SoftwareBitmapSource`，各控件独立释放 WinRT 图像源；取消或替换时及时回收资源。
- `CoverPresentationService`、`SystemMediaControlsService`、`ToolUtils`：切歌时取消过期封面/媒体控制任务及原图读取，避免大封面被旧任务链延迟持有。
- `LyricsRenderCoordinator`、`AlbumArtControl`、`NowPlayingCanvas`：连续换词时立即释放被覆盖的 Win2D 待销毁行，修复丢弃池化帧和卸载时的 Win2D 视觉树引用，关停时完整清理文本布局、几何和缓存效果。

## 2026-09-23 修复 WebDAV DSF 采样率显示为八分之一

- `Services/WebDav/RemoteMetadataProbe.cs`：将 FFmpeg 对 DSF/DFF 暴露的 DSD 字节率换算为实际 DSD 采样率，并按一位样本写入元数据。
- `Services/MusicDatabaseService.WebDav.cs`：后续元数据扫描自动重读旧版本已保存的八分之一采样率。
- `_tools/WebDavRegression`：增加 DSF 64/128/256 回归检查，覆盖采样率、位深、时长和有界读取。

## 2026-09-23 精简 WebDAV 来源卡片操作

- `View/SubView/WebDavSourcesControl.xaml/.cs`：移除无用的“歌曲”跳转按钮；来源图标由纯装饰 Border 改为与 AddFolderPage `OpenFolderButton` 同款的可点击按钮，直接打开 WebDAV 目录浏览，操作行不再保留重复的浏览按钮；清理不再使用的 using。
- `Strings/*/Resources.resw`：删除全部语言中不再使用的 `WebDavSongsAction` 资源。

## 2026-09-23 修复 WebDAV 目录选择、曲库范围和来源回退

- `WebDavConnectionDialog.xaml/.cs`、`WebDavConnectionViewModel.cs`、`WebDavTreeItem.cs`：模型管理父子级联与部分选中，懒加载子目录继承选择；扫描根独立保存，勾选所有子目录不会意外扩大到父目录，取消父目录清空子树，取消单个子目录保留其余范围。
- `MusicDatabaseService.WebDav.cs`、`MusicDatabaseService.cs`、`WebDavLibraryService.cs`：保存来源时立即排除范围外的旧索引，曲库读取过滤缺失曲目并刷新当前视图；重新扫描纳入的歌曲复用原 ID，保留收藏和歌单映射。
- `WebDavSourcesViewModel.cs`：当前来源被移除且 ComboBox 清空选项后，选择和过滤器统一回退到“全部来源”；移除其他来源保留当前选择。
- `_tools/WebDavTreeUiRegression`：链接生产对话框、ViewModel、SQLite 方法，覆盖真实复选框点击、级联与保存、范围缩小及重载、曲目身份保留和真实 ComboBox 删除回退。已被扩大并保存的旧配置仍需重新选择原目录。

## 2026-09-23 修复升级后播放列表卡片封面为空

- `Model/PlayList.cs`、`View/PlayListPage.xaml`：封面绑定改为可通知的 `CoverMusic`，修复界面先于歌曲映射加载时，按不变 ID 取图后不再刷新的问题；该属性不写入数据库，无需重建歌单。
- `Services/PlaylistSummaryProjection.cs`、`ViewModel/AppViewModel.cs`、`Services/MusicDatabaseService.cs`：在 UI 线程随音乐库、歌单成员和顺序更新封面及数量，单次遍历成员选择默认排序的首曲；删除后读取最新成员，移除详情页重复查询。
- `_tools/PlaylistCoverRegression`：链接真实页面 XAML、图片行为和投影，覆盖延迟加载、同数量排序、移除曲目、新建/空歌单及通知；1.2.5.0 原始 XAML 复现失败，修复后通过。主程序 x64 Release 构建 0 错误。

## 2026-09-23 修复本地 FLAC 曲尾错误导致播放停住

- `External/AudioPlayer/Decode/PcmDecoder.cs`：有限次恢复无效编码帧并继续读取到真实 EOF，修复曲尾错误导致不发结束通知、自动切歌及 seek 失效；真实 I/O 失败仍保留失败语义。
- `Player/AudioPlayer.exe`：更新 NativeAOT 播放器产物。
- `_tools/PlaybackSwitchRegression`、`_tools/AudioPlayerSmokeTest`：新增自行生成的曲尾损坏 FLAC、完整解码及 EOF 后 seek、DirectSound 淡入淡出和原生结束通知验证；用户文件完整播放 292.667 秒通过，播放回归 295/295、网络回归 50 项通过。
- `docs/PlaybackEndInvestigation.md`：记录复现证据及 1.2.2.0 以来播放改动的目的，定位回归来源为 `636f4844` 的共用解码错误处理。

## 2026-09-23 修复 WebDAV 树节点显示类型名

- `WebDavBrowserDialog`、`WebDavConnectionDialog`：模板按实际的 `TreeViewNode` 类型读取 `Content` 中的数据并创建名称和图标，修复控件类型名直出和折叠后复用视图引发的异常。
- `_tools/WebDavTreeUiRegression`：链接生产 XAML 和节点模型，运行真实 WinUI 对话框验证显示、多选和展开行为。

## 2026-09-23 WebDAV 目录对话框改为多选树

- `WebDavConnectionDialog`、`WebDavConnectionViewModel`、`WebDavTreeItem`：按展开加载多层目录，恢复已保存的深层音乐根目录，并对父子重复选择去重。
- `WebDavBrowserDialog`、`WebDavBrowserViewModel`：按展开浏览远端目录，勾选多首已入库歌曲后按选择顺序建立播放队列；六种语言资源同步更新。
- `WebDavConnectionDialog`、`WebDavBrowserDialog`：通过节点映射关联目录数据，将 `SelectedNodes` 的勾选变化同步到 ViewModel。
- `_tools/WebDavRegression`：增加目录树深层选择、保存与恢复回归。
- `docs/WebDAV接入设计方案.md`：同步目录树与多选播放交互说明。

## 2026-09-23 修复短曲目偶发结束后不自动切歌

- `Services/BassPlayerCommandService.cs`、`Services/AutoAdvanceGate.cs`：自动切歌执行中保留一次新的结束请求，待当前选曲与界面状态完成后继续处理；执行异常记录日志，不让单飞状态卡住。
- `_tools/AutoAdvanceRegression`：覆盖在途切歌期间再次结束、重复结束合并和退出清理。

## 2026-09-22 卷积曲线预设改存用户文档目录

- `Services/CurvePresetService.cs`：`ConvolutionCurves.json` 由 MSIX LocalState 改存 `Documents\OriginalSoundPlayer\Settings\`（与 Settings.json、AudioCorrections.json 同目录），卸载重装/重新部署不再丢失预设；Documents 不可用时回退 LocalState。不做旧位置迁移。
- `External/AudioPlayer/DSP.md`：同步预设存储位置说明。

## 2026-09-22 WebDAV 大 DSF 的内存、定位、位流与封面修复

- `AudioPlayer/Playback/AudioRingMemory.cs`、`Ring.cs`、`Session.cs`：网络 PCM/DoP/Native DSD 大环缓冲由会话独占的原生内存承载，停止时在读写锁内释放；拒绝迟到读写、唤醒等待生产者，避免每次切歌的多 MB LOH 分配等待 GC。未增加生产环境强制 GC。
- `Decode/FfmpegHttpInput.cs`、`DsdRawReader.cs`、`PlaybackEngine.Streaming.cs`、`Streaming.cs`、`RemotePlaybackService.cs`：远程 DSF/DFF 按输出设置选择 Native DSD/DoP，匿名桥接 URL 通过扩展名提示保留格式；共用超时、取消、预缓冲/欠载恢复及定位能力，保持 Native → DoP → PCM 设备回退。
- `build/ffmpeg-dsf-seek.patch`、`Libraries/FFmpeg/x64/avformat-63.dll`：从现有 FFmpeg 源码重编译，DSF 按固定块直接定位并使用样本数计算时长，不再为了建立索引遍历未播放音频；构建脚本自动检查/应用补丁。
- `Reader/AudioCoverReader.cs`、`Services/WebDavLibraryService.cs`：按 DSF 头部偏移读取尾部 ID3/APIC；对旧 DSF 空封面缓存做有界重试，复用原图缓存。
- `_tools/PlaybackSwitchRegression`、`_tools/StreamingRegression`：新增大文件定位、精确包位置/EOF、DSD 模式、取消、原生缓冲释放与正式 NativeAOT 连续切换回归；`Player/AudioPlayer.exe` 已更新。
- 验证：NAS 的 1,534,642,268 字节 DSF 封面读取成功；跳到 80% 并预缓冲约 77–78 ms、传输约 5.8–6.2 MB。真实 FiiO ASIO 的 DSD256 Native 和 DSD64 DoP 输出成功；此设备拒绝 DSD256 DoP 所需的 705.6 kHz。测量范围、命令与硬件边界见 `docs/WebDAV接入设计方案.md` 第 15 节。
- 构建：NativeAOT 与主程序 x64 Release 通过；本机缺少符号转换工具，主程序验证通过命令行关闭符号包和包签名（未更改项目发布默认值），现有平台/裁剪等警告保留。

## 2026-09-22 WebDAV 按来源确认 NAS 自签名证书

- `Services/WebDav/WebDavCertificates.cs`、`WebDavTransport.cs`：HTTPS 失败提供证书信息和 SHA-256 指纹；确认后仅放行指定来源地址的同一张有效证书，证书变化需重新确认，过期证书继续拒绝。连接池按信任状态隔离，不修改系统信任或放开其他来源。
- `Model/WebDavSource.cs`、`Services/WebDavLibraryService.cs`：保存来源时持久化证书地址与指纹，目录同步、元数据、封面和播放共用；已有来源默认无证书例外。
- `WebDavConnectionViewModel.cs`、`WebDavConnectionDialog.xaml`、`Utils/WebDavText.cs`、六种语言资源：增加证书详情、确认重连及忘记入口，区分证书未受信任、证书变化与其他 TLS 错误；修改连接信息或关闭窗口会取消测试并拒绝迟到结果。
- `_tools/WebDavRegression`：增加真实 TLS 目录/Range、证书与连接池隔离、过期、来源保存恢复、SQLite 迁移和表单取消回归。NAS 实测为 fnOS 自签名证书且名称不包含访问 IP；固定该证书后 TLS 成功进入 HTTP 认证层，无密码测试返回预期 401。
- 验证：50 项回归、六种语言资源键及格式占位符检查、Release 构建通过；现有编译/裁剪警告仍在。未替换当前运行实例，WinUI 证书确认交互需在新构建验证。

## 2026-09-22 统一 WebDAV 缓存位置并修复播放详情原图

- `Services/WebDavLibraryService.cs`、`Services/WebDav/WebDavCachePaths.cs`、`RemoteAudioCache.cs`：统一使用原有可配置缓存根目录，远程音频和原图分别写入 `WebDav/Audio`、`WebDav/Covers`；缩略图继续共用 `Cache`。取消独立 WebDAV 路径配置，升级及换目录按需重建，旧缓存保留、不批量搬运。
- `Controls/ImageSwitcher.xaml.cs`、`Behaviors/FadeImageBehavior.cs`、`Utils/ToolUtils.cs`、`Helper/PlaybackCoverCache.cs`：展示与取图按同一标识读取同一份远程原图；临时文件完整发布后才刷新封面，不重复保存原图、不重复请求网络。
- `Services/CoverPresentationService.cs`、`SettingsActions.cs`、`ViewModel/Pages/WebDavSourcesViewModel.cs`、关于页及六种语言资源：统一入口更改目录，WebDAV 位置只读展示；切换后刷新封面和缓存占用，清理封面包含远程原图，下载音频仍独立清理。
- `RemoteAudioCache.cs`：换目录使旧写入失效，提交时再次核对代次；旧目录的预留不占用新目录额度，清理新目录不删除旧目录仍在读取的音频。
- `_tools/LyricsCoverRegression`、`_tools/WebDavRegression`：增加同目录原图解析、完整发布、命中、并发、取消/失败、空封面，以及跨目录额度、提交和活动读取回归。
- 验证：歌词/封面 27 项、WebDAV 核心 31 项通过，六种语言设置资源检查通过；x64 安装包构建通过。当前运行中的 Release 实例未替换，统一缓存位置及大封面需使用新构建验证界面。

## 2026-09-22 WebDAV 音乐来源、网络播放与可选缓存

- `Services/WebDav`、`WebDavLibraryService`、`MusicDatabaseService.WebDav`：只读目录同步、稳定来源索引、分批补全标签；FLAC 使用 ATL Stream 跳过封面，其余格式使用有界 FFmpeg 探测，封面优先复用自研读取器。
- `RemotePlaybackService`、`PlaybackCoordinator`、`IpcService`：连接现有 StreamingClient，主程序管理鉴权与 loopback 桥接、暂停/定位/切换和资源收尾；完整音频缓存可复用，自动下载默认关闭、10 GiB 上限。
- `View/AddFolderPage`、`WebDavSourcesControl`：统一音乐来源标题、添加菜单及本地/WebDAV 卡片样式；音乐库增加来源筛选，歌曲/收藏/歌单/分组列表增加来源图标列。
- `View/MainPage`、`View/SubView/Settings/AboutSettingsControl`：网络标识放到播放栏歌曲信息与音频格式同一行，状态以提示显示；缓存设置放到关于页的可展开卡片，不增加独立设置分类。
- `Model/Music`、相关详情/转换/导出入口：远程文件只读，收藏、歌单、统计继续使用统一 Music.Id；本地扫描不清理远程记录，相同专辑名按来源区分。
- `Strings/*/Resources.resw`：新增界面与错误信息覆盖六种语言，程序取词采用独立资源键。
- `Player/AudioPlayer.exe`、`TrimmerRoots.xml`、项目文件：更新 NativeAOT 播放器，避免远程结束重复走旧通知；保留 SQLite 模型与命名管道依赖供裁剪后的应用使用。
- 验证：x64 安装包构建成功；OpenList 实际 224 首元数据通过；WebDAV 核心 22 项、网络播放器 39 项、本地切换 281 项、共享曲库和歌词封面回归通过；实际界面验证来源添加、扫描、播放/暂停和约 33 MiB 缓存落盘。NAS、广域网与近 100 GB 长时间负载尚未验证，性能测量范围见设计文档第 14 节。

## 2026-09-21 移除 Atmos / 5.1 状态卡的“恢复普通播放”按钮

- `View/SubView/Settings/GeneralSettingsControl.xaml`：删除 Atmos 直通与 5.1 环绕状态提示条上的按钮及随之失去意义的 `ContentAlignment="Right"`，状态文本保留，关闭功能直接用各卡片自身的 toggle。
- `ViewModel/AppViewModel.Atmos.cs`、`ViewModel/AppViewModel.Surround.cs`：删除 `UseOrdinaryPlaybackCommand` / `UseOrdinarySurroundPlaybackCommand`，实现只是把对应开关设为 false，与 toggle 完全等价；同步移除不再使用的 `CommunityToolkit.Mvvm.Input` using。
- `Strings/*/Resources.resw`：移除 6 种语言的 `AtmosUseOrdinary`、`SurroundUseOrdinary` 资源键。

## 2026-09-21 AudioPlayer 网络播放基础

- `External/AudioPlayer`、`External/BassPlayerIpc.Shared/Streaming.cs`：参考 Plugins 最后提交 df6f9d99，加入 HTTP(S) 描述符、鉴权请求头、独立管道客户端、异步准备、播放/暂停、Range 定位、URL 刷新及缓存状态，为 WebDAV GET 播放准备接口。
- `Playback/Session.cs`、`Playback/Ring.cs`、`Decode/PcmDecoder.cs`：有界预缓冲和断流恢复、原生 I/O 超时取消、失败与自然结束分离；网络源不触发本地响度扫描，退出阻止新请求并等待准备任务收尾。
- `Playback/PlaybackEngine.Streaming.cs`：补齐停止时恢复计划失效、在途准备并发上限、seek ID 同步及越界拒绝；刷新保留位置和暂停意图。
- `Libraries/FFmpeg/x64`、`build/ffmpeg-network.sh`：使用 Plugins 四个 DLL；实测同为 9.0.1，编解码器/封装器/解封装器列表相同，输入协议新增 httpproxy；更新构建来源与 SHA-256。
- `Player/AudioPlayer.exe`：更新 NativeAOT 发布产物；`External/AudioPlayer/README.md` 补充接入示例和能力边界。暂不包含 WebDAV UI、目录浏览或账号管理。
- 验证：主程序 x64 构建及播放器 NativeAOT 发布成功；本地播放回归 281/281，网络集成 39/39；真实 AOT 进程网络集成覆盖 HTTP、Range、鉴权头描述符、401、截断、重试、超时、TLS 不可信证书拒绝、实际设备播放、暂停刷新、自然结束与打开期间退出；未验证真实 WebDAV 服务和 HTTP 代理服务器。

## 2026-09-21 FFmpeg DLL 换为支持网络播放的最小化构建

- `Libraries/FFmpeg/x64/*.dll`：基于同一 n9.0.1 源码（commit bf1b838f）重编，去掉 `--disable-network`，协议在 file 基础上新增 http/https/tcp/tls，TLS 用 Windows 原生 SChannel；demuxer/decoder/encoder/muxer/parser 清单与原版完全一致（schannel 会自动带入 dtls/udp，为 configure 上游行为）。
- `Libraries/FFmpeg/build-audio-net.sh`：新增网络版构建脚本，与原 `build-audio.sh` 仅上述三处差异；`Libraries/FFmpeg/x64/BUILD_INFO.txt` 更新工具链（MSYS2 UCRT64 gcc 16.2.0，D:\code\msys64）、SHA256 与验证记录。
- 验证：PlaybackSwitchRegression 281/281；本地 HTTP（E-AC-3 5.1 M4A）与公网 HTTPS MP3 实际经 libavformat 打开解码，旧 DLL 对 http:// 正确拒绝。License 不变（LGPL-2.1+，schannel 为系统组件）。

## 2026-09-21 退出时取消在途歌词请求

- `Services/LyricsLoader.cs`、`Services/LyricsRefreshService.cs`：将应用停止令牌与切歌取消合并；每次解析在实际结束后释放自身 CTS，取消后停止后续搜词，不再让退出等待完整网络超时与回退链路。
- `WebService/LrcService.cs`、`External/Lyricify.Lyrics.Helper`：网易云/QQ 搜索、歌词下载和 HTTP 读写贯通取消；保留原调用重载，取消不触发备用搜索，释放请求内容和响应。
- `_tools/LyricsCoverRegression`：新增挂起请求取消、退出等待实际清理、取消后重试，以及网易云新搜索和两家歌词下载的取消回归。

## 2026-09-20 更新对话框警告精简并补充系统美化软件不兼容

- `Strings/*/Resources.resw`：`RtssWarningTitle` 放宽为覆盖 FPS 监控与系统美化两类注入软件；`RtssWarningBody` 精简为一段，保留「桌面歌词＋着色器背景」与 DXGI 挂钩冲突的触发条件，并补充 Windhawk、StartAllBack 等注入式系统美化软件同样可能导致崩溃，均建议关闭或将本程序加入排除列表。全部 6 种语言同步，键名与 XAML/GetString 未变。

## 2026-09-20 歌词 Helper 迁移到可剪裁的 System.Text.Json

- `External/Lyricify.Lyrics.Helper`：移除 Newtonsoft.Json，使用源生成 JSON 元数据迁移各提供商、KRC/YRC/Spotify/Musixmatch；兼容数字字符串、布尔值、请求转义与浮点输出，Musixmatch 使用可释放的 JsonDocument。
- `WinUIMusicPlayer.csproj`：移除 Lyricify.Lyrics.Helper 的 TrimmerRootAssembly；库启用剪裁分析，外部自定义 DTO 可传入 JsonTypeInfo。
- `_tools/LyricsJsonRegression`：保存迁移前 191 个模型、请求载荷及解析/生成器输出基线，禁用反射并验证全剪裁发布；公共 ToJson 缩进参数改为 bool，未指定类型的 JSON 对象改为 JsonElement，其他兼容边界与测量见其 README。

## 2026-09-20 修复外部导入重开、文件夹显示与并发创建

- `Services/OneShotPlaybackService.cs`：每次显式打开重新解析库内身份，移除后重开和失败后重试不再复用旧结果；通过文件夹 ViewModel 统一发布导入状态。
- `ViewModel/Pages/AddFolderViewModel.cs`：导入歌曲按 Id 去重发布，同步数据库中的文件夹行、计数与空状态，首次导入立即显示虚拟文件夹。
- `Services/MusicDatabaseService.ExternalImports.cs`：从数据库主文件移出外部导入方法供真实代码回归复用；虚拟文件夹查询与创建在同一事务中完成，避免并发查空后重复插入。
- `_tools/FolderScanRegression`：编译生产解析与导入代码，覆盖移除后同路径重开、失败后重试、首次发布、重复发布计数和 30 轮并发创建；平台桩不替代真实 WinUI 文件激活与派发验证。

## 2026-09-20 修复双击导入后歌曲/专辑/艺术家列表与播放队列不刷新

- `Services/OneShotPlaybackService.cs`：入库与播放派发存在竞速——派发时另行按路径查库，而解析任务内的入库写仍在飞行，查空即误入一次性分支（`PlayMusic` 只换当前曲：不发布 SongsSource/页面投影、不建文件夹队列）；歌曲/专辑/艺术家列表要等重启后的全量加载才正确。修复：删除派发时查库，分支一律以 await 后的解析结果 Id 判定（Id>0=库内行，统一发布+`PlayMusicWithFolderQueue`）；固定盘先查库再解析元数据（库内已有文件免重复解析）；发布路径改为顺序等待扫描批发布完成（SongsSource/ListSongs/文件夹计数）后触发 `NotifySongsSourceChanged`，当前页投影按库版本重建、导入即时按序可见。
- 验证：主工程构建 0 错误；FolderScanRegression（含外部导入场景）与 LifecycleRegression 全部通过；OneShot 派发竞速需真机复测（首装双击导入立即出现在歌曲/专辑/艺术家页且进入当前播放队列、连续导入多首逐一可见）。

## 2026-09-20 双击打开的外部文件入库为库内条目（外部导入虚拟文件夹）

- `Services/OneShotPlaybackService.cs`：解析阶段判定位置——固定本地盘且库内无同路径行时经 `AddExternalFileAsync` 入库并返回库内权威行，播放、统计、歌词、当前曲存档均为标准库内语义；可移动盘/网络盘/UNC 及入库失败回退原一次性播放（Id=0 不写库不统计，`OneShotLyricsCache` 继续服务）；引擎首推探询拿到的即库内行，保留省一次恢复曲加载的优化；首次入库条目经扫描批发布路径增量进 SongsSource/文件夹计数。
- `Services/MusicDatabaseService.cs`：新增 `WhenInitialized`（早于 Host 启动的一次性解析在入库前等待）与 `AddExternalFileAsync`（确保虚拟行存在 + 复用提交批核心，事务内同路径查重防并发扫描/转换重复落库，失败按路径回查）；`Initialize` 失败以异常完成信号。
- `Services/MusicDatabaseService.Scanning.cs`：外部导入虚拟文件夹（`Folder.Type="external"` 哨兵行）管理——归属按路径现算（不在任何本地扫描根内的行），`GetFoldersWithSongCountsAsync` 现算虚拟行计数，`CheckFolderBeforeAdd` 重叠判定排除虚拟行；`RescanFolder`/`RemoveFolder` 对虚拟行改为存在性对账/按归属移除；新增 `ReconcileExternalImportsAsync`（盘根不可达整组保留、确认缺失行连同歌词删除）。
- `Services/InitialFileScan.cs`、`AutoRescanService.cs`、`LibraryWatcherService.cs`：枚举/监视跳过虚拟哨兵行；启动扫描末尾统一执行外部导入对账。
- `Services/LibraryPath.cs`：新增 `IsFixedLocalDrive`（可移动/网络/UNC/光驱/无法判定返回 false，与 UsbDeviceMusic 设备音乐体系保持边界）。
- `Model/Folder.cs`：`Type` 常量（`TypeLocal` 保持存量字面量、`TypeExternal`）、哨兵路径常量、`IsExternalImport`/`CanOpenInExplorer` 派生属性。
- `ViewModel/Pages/AddFolderViewModel.cs`、`Services/FolderCommands.cs`、`View/AddFolderPage.xaml`：`ApplyBatchAsync` 改 internal 并同步虚拟行计数；虚拟行显示名在展示层注入资源（库内不固化本地化文本）、隐藏"打开所在位置"；移除确认按类型区分文案。
- `Strings/*/Resources.resw`（六语言）：新增 `ExternalImportsFolderName`、`RemoveExternalImportsTitle`。
- 行为：从资源管理器双击固定盘音乐文件 = 入库 + 按文件夹页语义播放（同文件夹入队列）；所在目录之后加入扫描不重复（归属移交扫描根）、移除扫描根随路径删除、文件从磁盘删除由启动对账清理；多选文件仍只取第一个；U 盘/网络路径双击维持不入库的一次性播放。
- `_tools/FolderScanRegression`：新增外部导入回归（归属现算/移交、扫描根加入去重与移除、对账删除/保留、虚拟行移除）；`Program.cs` 末尾目录清理加句柄晚释放重试兜底，消除偶发非零退出。`_tools/LifecycleRegression`：桩 `Folder` 补 `IsExternalImport` 对齐生产模型。

## 2026-09-20 悬停滚动开关默认关闭并更名

- `State/AppearancePreferencesState.cs`、`Model/SaveSettings.cs`：`IsHoverScrollEnabled` 默认值由开改为关；设置文件中已保存该键的用户不受影响，仅新装机或无该键的存档默认关闭。控件侧 `AnimatedTextBlock.IsHoverScrollEnabled` 本就默认关，保持一致。
- `Strings/*/Resources.resw`（六语言）：`AnimatedTextHoverScroll` 显示名改为「Win2d动画文本悬停滚动」，作用域由标题表达——AutoScrollView 包裹的普通 TextBlock（回退路径与各列表页）的悬停滚动是默认常开、不受此开关控制的既有行为，避免名称误导；描述保持一句行为说明不变。
- `_tools/SettingsPersistenceRegression/Program.cs`：默认值断言改为默认关闭，改为验证显式开启值可往返保存；已运行通过。
- 范围说明：该开关唯一运行时消费点是 `View/PlayingDetailPage.xaml` 的 `AnimatedTextBlock` 绑定；同页 Win2D 文本关闭时的 `AutoScrollView` 回退路径及各列表页的悬停滚动不受它控制（既有行为，未改动）。

## 2026-09-20 继续迁移窗口、队列与展示生命周期

- `State/`、`Services/HotKeyService.cs`、`ShellService.cs`、`OutputDeviceService.cs`：共享快捷键、窗口和设备状态；原生注册、枚举与解绑移出 AppViewModel，退出等待枚举结束并屏蔽迟到结果。
- `Services/SettingsCoordinator.cs`、`SettingsSnapshotFactory.cs`、`SettingsActions.cs`：设置效果和保存快照解除 AppViewModel 反向依赖，目录操作由设置命令服务承担；保留旧绑定名称和默认值。
- `State/PlaybackQueueState.cs`、`Services/PlaybackCoordinator.cs`、`Model/SavePlayState.cs`：为重复歌曲建立独立条目身份，统一前后切歌及队列双击入口；保存随机顺序与游标，旧文件或不匹配的存档安全回退；库刷新批量保留条目身份。
- `Services/LibraryBrowseCoordinator.cs`、`LibraryProjectionService.cs`：迁移库展示集合与刷新调度；UI 捕获字段快照，后台执行列表搜索/排序，每个目标合并请求、全局串行计算，停止后不发布结果，池数组可靠清空归还。
- `Services/CoverPresentationService.cs`、`LyricsLoader.cs`、`LibraryTrackActions.cs`：封面、歌词展示及网络歌词任务移出根/浏览 VM，并纳入任务屏障；菜单打开时才准备歌单和 USB 子项，不再预先创建所有页面 VM。
- `Services/EditorSessions.cs`、DSP/卷积/频响 VM、`SystemMediaControlsService.cs`：退出等待编辑器提交、导入/预设操作与频响计算；SMTC 只提交当前版本元数据，显式持有并释放封面原生流。
- 验证：七组回归通过（播放切换 281/281）；本轮新增共享快捷键、重复条目恢复、编辑器等待/失败隔离、查询合并/停止发布回归。用户已通过上一轮人工验收；本轮 WinUI 菜单、设置即时退出、真实设备/SMTC 和 Release GC 对照仍需验收。

## 2026-09-20 动画文本空字形回调修复

- `External/AnimatedWin2dControls/AnimatedWin2dControls/Controls/AnimatedTextBlock/Internals/ShapedText.cs`：跳过 `null` 和空字形数组，修复切换文本时 `DrawGlyphRun` 访问 `glyphs.Length` 引发的空引用异常。

## 2026-09-20 共享状态迁移与异步收尾

- `State/`、`ViewModel/AppViewModel*.cs`：新增单例 `AppState`，迁移播放进度/队列、浏览状态、输出状态及 71 个分域偏好；旧绑定通过同一实例转发通知，保留默认值与现有布局。
- `Services/PlaybackCoordinator.cs`、`PlaybackProgressService.cs`、`ApplicationTasks.cs`、`ShutdownCoordinator.cs`：提取选曲用例与进度轮询，拒绝退出后的播放，等待在途任务，退出步骤记录名称与耗时。
- `Services/SettingsCoordinator.cs`、`SettingsSaveQueue.cs`、`SettingsSnapshotFactory.cs`：分离偏好副作用和持久化快照，合并设置写入及异常观察任务；退出提交防抖期间的最终值。
- `DesktopLyrics/DesktopLyricsViewModel.cs`：桌面歌词公共开关使用共享状态，修复托盘/快捷键与设置页逐字开关不同步。
- `State/PlaybackQueueState.cs`、`Services/LibraryQueries.cs`、`LibraryProjectionService.cs`：随机追加同时更新规范队列和播放顺序；库索引按版本重建，过滤/分组缓存查询键并延后隐藏页刷新，引用池数组归还时清空。
- `ViewModel/StatsViewModel.cs`：慢查询期间保留最新筛选请求，拒绝旧结果，离页停订阅和刷新；查询发布保持 UI 上下文。
- `Services/UsbExportCoordinator.cs`、`Helper/UsbWriterHelper.cs`：USB 操作独立身份和退出屏障，只登记成功复制/转换的实际格式；取消在当前文件实际完成后停止下一项。
- 详情页 VM 改用可等待命令，快照化删除/重排输入，离页解绑；文件夹 VM 构造不查库，激活加载并等待进行中操作收尾。
- `_tools/SharedStateRegression`：覆盖共享通知、随机追加、合并写盘/失败重试、停止屏障、真实文件复制及统计页异步竞态。WinUI 实机交互和 Release GC/延迟测量尚未执行；完整迁移状态见 `docs/ServiceRefactoringPlan.md`。

## 2026-09-20 文件夹兼容回退与退出异常隔离

- `Services/FolderAccessService.cs`、`ViewModel/Pages/AddFolderViewModel.cs`、`App.xaml.cs`：提取文件夹平台交互服务，按路径打开目录并补 Explorer 回退；选择器 COM/不支持异常时回退 HWND 绑定的 WinRT 选择器，取消不重复弹窗。
- `Services/AppLifecycle.cs`、`Services/ShutdownCoordinator.cs`：退出时隔离取消及状态订阅者异常，继续保存与清理，避免停留在 Stopping 而不再执行收尾。
- `_tools/LifecycleRegression`、`_tools/FolderScanRegression`：新增退出失败与选择器回退回归；修正播放意图测试的引擎就绪前置条件。
- `TODO.md`、`docs/ServiceRefactoringPlan.md`：记录分阶段重构方案及验收边界；第 3 项已于 2026-09-20 经用户确认验收，原生退出崩溃仍待定位。

## 2026-09-19 5.1 自动独占与输出状态布局调整

- `External/AudioPlayer/Playback/PlaybackEngine.Surround.cs`、`PlaybackEngine.cs`、`Decode/PcmDecoder.cs`：共享偏好下仅兼容 5.1 PCM 曲目自动切独占，保留音量和静音；ASIO 保持原模式，失败在同设备回退普通 PCM，设备变化暂停，普通曲目恢复原输出偏好；Atmos 优先且回退不触发二次独占。
- `External/AudioPlayer/Interop/WasapiInterop.cs`：Atmos 与 5.1 共用稳定端点解析，显式设备失效时不改用默认扬声器。
- `External/BassPlayerIpc.Shared`、`Services/IpcService.Atmos.cs`、`ViewModel/AppViewModel.Surround.cs`：发布 5.1 实际状态并复用统一失败系统通知；DSP 状态邮箱升级为 v8，主程序与 `Player/AudioPlayer.exe` 配套更新。
- `View/SubView/Settings/GeneralSettingsControl.xaml`、`ViewModel/AppViewModel.Atmos.cs`、`Strings/*/Resources.resw`：去掉 Atmos 状态文案的强制换行，状态文字与操作按钮左右排列，窄窗口自然折行；增加 5.1 状态与关闭入口，补齐六种语言。
- 验证：播放回归包含自动切换、音量/静音、暂停保进度、同设备回退、无效端点、ASIO 及 Atmos 组合；WinUI 构建与资源键静态校验。真实六声道出声、通知横幅及新布局实机视觉尚未验证。

## 2026-09-19 Atmos 自动独占直通与统一系统通知

- `External/AudioPlayer/Playback/PlaybackEngine.Atmos.cs`、`PlaybackEngine.cs`、`Interop/WasapiOutput.cs`：兼容曲目临时使用 WASAPI 独占，保留普通输出偏好；固定目标端点协商格式，失败保进度回退同设备 PCM，断开设备暂停，避免重复抢占；能力查询与初始化共用超时及资源收尾。
- `External/BassPlayerIpc.Shared`、`Services/IpcService.Atmos.cs`、`ViewModel/AppViewModel.Atmos.cs`、`View/SubView/Settings/GeneralSettingsControl.xaml`：新增可选 Atmos 专用设备、实际状态和恢复普通播放入口；ASIO 需明确指定设备，旧配置沿用当前输出；六种语言补齐独立资源键。
- `Services/NotificationService.cs`、`App.xaml.cs`、`Services/MusicDatabaseService.Metadata.cs`：统一系统通知单例入口，失败通知区分 PCM 回退和停止，30 秒同类去重、有界缓存和异常隔离，退出后拒绝迟到通知。
- `Player/AudioPlayer.exe`：同步发布更新后的 AOT 播放端；DSP 状态邮箱升级，主程序与播放端须配套更新，旧设置载荷仍可读取。
- 验证：主程序构建、播放切换回归、通知网关并发/失败/退出测试、设置持久化、元数据通知回归和 Release 许可门控；真实 HDMI 功放、系统通知横幅及界面设备交互仍需实机验证。

## 2026-09-19 合并动画文字、增加悬停设置并修复混排动画收尾偏移

- `View/SubView/Settings/CoverBackgroundSettingsControl.xaml`、`ViewModel/AppViewModel.Settings.cs`、`Model/SaveSettings.cs`、`Services/MusicDatabaseService.cs`：在 Win2D 动画文本块下增加悬停滚动开关，即时生效并持久化；旧配置默认开启，保持原页面行为，六种语言补齐独立资源键。
- `View/PlayingDetailPage.xaml(.cs)`、`Utils/BindUtils.cs`：标题与两行专辑/艺术家改为一个 AnimatedTextBlock，保留字号、字重、透明度及固定行高；整组内容一次更新，统一推进动画效果。
- `External/AnimatedWin2dControls/.../AnimatedTextBlock`：新增不可变 Document/Paragraph 输入，共用 Canvas 和动画进度；各截断行独立悬停滚动，切换动画优先，格式变动和卸载释放缓存。
- `External/AnimatedWin2dControls/.../AnimatedTextBlock/Internals`、`Effects`：逐字效果复用完整排版的字形、回退字体和基线，统一静态/动画的像素对齐；修复重复 Stay/Move 操作，保留段落偏移和彩色符号绘制。
- `_tools/AnimatedTextRegression`、`_tools/SettingsPersistenceRegression`：增加单 Canvas、多样式、八种效果、混排末帧图像对比、DPI/RTL/截断/emoji、开关绑定及设置兼容性回归。

## 2026-09-19 修复动画文字在 x:Load 初始化时消失

- `View/PlayingDetailPage.xaml`：字号改为带正值回退的常规 Binding，避免 x:Load 创建控件时生成的 x:Bind setter 写入默认 0，触发 WinUI 参数错误并阻止控件进入可视树；保留 ViewModel 字号联动与固定行高。
- `_tools/AnimatedTextRegression/LayoutRegressionPage.xaml(.cs)`：新增真实编译 XAML 的嵌套 Grid、x:Load、绑定和 Loaded 字号更新用例，验证控件进入可视树、非零尺寸与绘制完成。

## 2026-09-19 修复跨字体切换导致文字动画中断和布局抖动

- `External/AnimatedWin2dControls/.../AnimatedTextBlock`：文本过渡中尺寸变化时重建动画布局，不再直接切入 Idle；新增 `LineHeight`，默认 0 保留自然行高，正值固定行高与基线。
- `View/PlayingDetailPage.xaml(.cs)`：动画文字字号统一绑定 ViewModel，并按字号的 1.4 倍向上取整设置行高，稳定中英文切换及两行专辑/艺术家信息的布局。
- `_tools/AnimatedTextRegression`：复现“祝融 → All In My Head”自然行高 31 → 32 DIP 导致动画中断；补充固定行高、基线与跨行数的 Fade/Default/Wipe 回归。

## 2026-09-19 修复 AnimatedTextBlock 两行信息的悬停滚动

- `External/AnimatedWin2dControls/.../AnimatedTextBlock`：支持专辑与艺术家在同一控件内显式换行；截断行按各自长度滚动，短行保留对齐与基线，切换动画仍优先，复位时统一释放各行布局。
- `_tools/AnimatedTextRegression`：补充 CRLF/LF、单行溢出/双行溢出、空行、对齐、RTL 和两行切换动画的真实 WinUI 回归。

## 2026-09-19 AnimatedTextBlock 自动测量与悬停滚动

- `External/AnimatedWin2dControls/.../AnimatedTextBlock`：按 Win2D 文字布局测量自身尺寸，统一依赖属性变更处理，恢复卸载后重新加载的资源与事件。
- `External/AnimatedWin2dControls/.../AnimatedTextBlock`：新增 `IsHoverScrollEnabled`（默认关闭），单行横排文字实际截断时悬停往返滚动；切换动画优先，移出、文字/格式/尺寸变化及卸载时复位。
- `View/PlayingDetailPage.xaml`：启用动画文字悬停滚动，普通文字分支改为 Collapsed，不再用透明文字撑高。

## 2026-09-18 外部文件路径匹配库内条目时直接按库内曲目播放

- `Services/MusicDatabaseService.cs`：新增 `FindMusicByPathAsync`——按路径 NOCASE 匹配库内条目（走 `IX_Music_Path_NoCase` 索引）返回完整 Music；数据库在引擎就绪前必已初始化，无需新增启动顺序
- `Services/OneShotPlaybackService.cs`：播放派发时先按路径查库——命中则播放库内条目（优先取 SongsSource 实例与库内列表/收藏同源，索引未同步时退回数据库行实例），统计、歌词、当前曲存档均为标准库内语义；未命中才等待外部解析走一次性播放（Id=0、不写库不统计）；查询失败按未命中回退
- `ViewModel/Pages/MusicBrowseViewModel.cs`：新增 `PlayMusicWithFolderQueue`——库内命中时播放队列替换为同文件夹曲目（`LastLevelFolderPath` 聚合，语义与文件夹页播放一致，沿用库内顺序），从匹配曲目开始；随机播放模式经 `SequentialPlayingList` 赋值自动洗牌；同文件夹条目尚未同步进 SongsSource 时不替换队列仅替换当前曲
- `Services/LyricsRefreshService.cs`：一次性分支只服务纯外部文件（本地 → 内嵌 → OneShotLyricsCache → 在线搜索），移除按路径取库内歌词阶段（匹配文件已改走标准库内链路，该阶段不可达）
- 行为：匹配文件从资源管理器打开 = 按文件夹页语义播放库内对应曲目（队列换为同文件夹歌曲、曲终接续文件夹顺序）；纯外部文件行为不变

## 2026-09-18 修复连续切换一次性外部文件时迟到歌词覆盖当前曲目

- `ViewModel/AppViewModel.cs`：歌词迟到守卫从按 `Music.Id` 匹配改为递增票据（`Interlocked`/`Volatile`）——一次性外部曲目 Id 均为 0，按 Id 匹配会放过上一首的迟到结果，覆盖正在播放曲目的 `UILyrics`

## 2026-09-18 文件关联一次性播放：系统"打开方式"入口直接播放外部文件

- `Package.appxmanifest`：新增 `windows.fileTypeAssociation`，注册 15 种音频扩展名（与 `ToolUtils.MusicExtensions` 一致），应用出现在系统"打开方式"候选
- `Services/OneShotPlaybackService.cs`（新增）：一次性播放唯一状态源——`CaptureActivationPath` 最早捕获激活文件（MSIX 文件激活 + unpackaged 命令行回退），后台解析出未入库 Music（Id=0）仅替换 `CurrentPlayingMusic` 播放并携带内嵌歌词；引擎就绪采用生命周期/就绪属性事件驱动派发（无固定延时），迟到回调按请求代差丢弃；失败 Toast 提示
- `Services/LyricsRefreshService.cs`：未入库曲目（Id=0）歌词链路补全——文件旁本地歌词 → 内嵌歌词 → KRC/LRC 在线搜索，全部不查询/写入数据库、不递增播放计数；在线搜索仍受"自动获取歌词"设置与熔断器约束；`Model/Music.cs` 新增 `[Ignore] EmbeddedLyrics` 内存字段承载
- `Services/OneShotLyricsCache.cs`（新增）+ `Helper/AppJsonSerializerContextHelper.cs`：外部文件独立歌词缓存——按路径 SHA-256 哈希存 JSON（`LocalFolder/OneShotLyricsCache`，与数据库无关），命中免在线搜索、搜到即写回；明文路径校验防哈希碰撞、临时文件原子替换、300 条上限按最后写入时间淘汰，读写失败静默不影响播放
- `App.xaml.cs`：`OnLaunched` 最早期捕获激活路径；第二实例带路径转发，第一实例立即 `Begin` 解析（与 Host/IPC/数据库初始化并行）；DI 注册服务
- `Helper/SingleInstanceHelper.cs`：`ActivateExistingInstance` 增加 `filePath` 参数，经 `WM_COPYDATA`（魔数 + UTF-16 路径）同步转发给现有实例主窗口
- `MainWindow.xaml.cs`：`NewWindowProc` 新增 `WM_COPYDATA` 分支——WndProc 内同步拷贝负载（SendMessage 语义要求）后转 UI 线程 `PlayNow`
- `Services/StartupCoordinator.cs`：引擎轨首推前探询一次性文件，解析已完成则内核直接加载外部文件（省一次恢复曲加载）；注册服务清理
- `Services/BassPlayerCommandService.cs`：`AutoPlayNextTrack` 列表循环分支补空队列守卫——一次性曲目曲终按现有 `index=-1→nextIndex=0` 语义从播放队列第一首继续；队列空时结束播放防取模零异常
- `Services/PlaybackStatsService.cs`：`StartSession` 跳过未入库曲目（Id≤0），不产生统计孤儿记录
- `Services/PlaybackStatePersistence.cs`：`LastPlayedMusicId` 仅在当前曲已入库（Id>0）时写入，一次性曲目不破坏下次启动的当前曲恢复
- `Strings/*/Resources.resw` ×6：新增 `OneShotOpenFailedTitle`/`OneShotOpenFailedContent`
- 行为：外部文件不入库、不入播放列表、不写统计；歌词为内存态（本地/内嵌/在线，不落库）；单曲循环仍重播该文件；手动"下一首"进入队列播放；多选文件只播第一个
- 兼容：无数据库/设置结构变化；MSIX 部署后"打开方式"候选生效需重装/更新部署包

## 2026-09-18 评审回修：扫描变更判定计入 UPDATE，播放命令恢复退出守卫

- `Services/MusicDatabaseService.Scanning.cs`：`CommitScanBatchAsync` 返回新增行与 UPDATE 命中行数，`RescanFolderCoreAsync` 一并计入变更数——修复既有文件仅元数据更新（外部改标签/重写）被误判"无变更"、启动后 UI 不刷新且 `UpdateTime` 已前移导致后续启动不再补偿的问题
- `Services/PlaybackCommands.cs`：`CanPlay` 恢复 `_lifecycle.IsReady` 守卫——`IsPlaybackEngineReady` 只在置位时包含 Ready，退出转 Stopping 后不复位，此前退出窗口期播放命令仍可达后端；统一成员缩进
- `_tools/FolderScanRegression/RegressionSuite.cs`：修正断言（插入时已保存精确文件时间，紧邻扫描本就无变更），新增"文件时间戳变化→报告有变更"正向用例
- `_tools/LifecycleRegression/Program.cs`、`Stubs.cs`：Ready 后置 `IsPlaybackEngineReady`（对齐引擎轨置位时机），桩属性改为可通知
- `docs/ApplicationLifecycle.md`、`docs/LegalAndStartup.md`：启动文档同步"音频进程与 IPC 先于协议拉起"新语义；历史验证记录标注改序时间点，改序后实机复验未执行
- 兼容：仅修改变更判定与命令守卫，无数据库结构/设置迁移；两个回归套件实跑全绿、主工程构建 0 错误

## 2026-09-18 全局进度指示：进度环统一收归 MainPage 标题栏，支持多操作并发显示

- `ViewModel/ProgressCenter.cs`：新增全局进行中任务中心（`Begin`/`Report`/`Complete` 按 Key 管理条目，非 UI 线程调用自动转派 DispatcherQueue）；多操作横排并列、各自显示百分比，**不做跨操作加权平均**（新操作加入会使平均值倒退，观感等同进度回退），仅"恰好一个操作且其上报百分比"时进度环用确定进度并显示独立百分比元素，否则不定进度
- `View/MainPage.xaml`：标题栏 AppTitle 旁的任务指示器改为绑定 ProgressCenter——环 + 百分比 + 操作文本横排，承接原 MusicBrowsePage 顶栏环的全部功能（任何页面与播放详情页均可见）
- `View/MusicBrowsePage/MusicBrowsePage.xaml`：移除顶栏传输进度环、百分比文本与常隐的"正在传输"TextBlock
- `ViewModel/AppViewModel.cs`：移除 `ProcessRingVisibility`/`ProcessRingPercent(Text)` 与标题栏任务派生属性（`IsTitleBarTask*`/`TitleBarTaskText`/`LibraryScanPercent*`/`IsLibraryScanning`/`IsLibraryLoading`/`IsIpcConnecting`），新增 `Progress` 中心；`TransmitFileToUsb` 改注册 `UsbTransmitting` 条目
- `Services/StartupCoordinator.cs`：IPC 连接、库加载、启动扫描注册到 ProgressCenter（扫描百分比经 `Report` 上报，单调保护移入中心）
- `Services/LibraryWatcherService.cs`：文件监视重扫注册 `LibraryRescanning` 条目；不再手工 `TryEnqueue` 切换可见性（修复：重扫期间百分比残留上一轮传输值、并发操作互相隐藏进度环）
- `ViewModel/Pages/MusicBrowseViewModel.cs`：移除 `ShowTransmission`/`HideTransmission`
- `Strings/*/Resources.resw` ×6：新增 `ProgressConnecting`/`ProgressLoadingLibrary`/`ProgressScanning`/`ProgressRescanning`/`ProgressTransmitting`；删除 `TitleBarTask*` 与 `Transmitting.Text`
- `_tools/LifecycleRegression/Stubs.cs`：桩同步（`ProgressCenter`/`ToolUtils.GetString` 空桩，移除 `ProcessRingVisibility`）

## 2026-09-18 启动扫描无变更不再刷新页面

- `Services/InitialFileScan.cs`：`InitialScan`/`Deduplication` 改返回 `bool`（本轮是否产生数据库变更）；目录扫描中途失败按有变更保守处理
- `Services/StartupCoordinator.cs`：`ScanLibraryAsync` 仅在扫描有变更（或失败可能已提交批次）时调用 `RefreshSongsSourceAsync`——数据未变时列表不再被 Reset 二次重置，消除启动"闪两次"；完成日志附带变更结果
- `_tools/FolderScanRegression/RegressionSuite.cs`：补断言——重试扫描（有提交）返回 true、随后无变更扫描返回 false
- 兼容：`ScanChangedFolderAsync` 既有返回值（新增/更新/删除计数）透传，无数据库结构变化

## 2026-09-18 移除启动 LoadingGrid：主界面即刻显示

- `MainWindow.xaml(.cs)`：删除 LoadingGrid 过渡层；`ShowMainPage()` 只注入 MainPage
- `Services/StartupCoordinator.cs`：`ShowMainPage()` 提前到窗口内容加载后（用户协议之前，首装弹窗覆盖在主界面之上）；新增 `IsLibraryLoading` 指示（库任务创建前置 true、主轨 WhenAll 后置 false）；版本更新弹窗从 `StartAsync` 末尾移至 `EnableInteraction` 后台弹出（不再阻塞交互启用，关闭后才记录已读版本）
- `ViewModel/AppViewModel.cs`：新增 `IsLibraryLoading`，标题栏任务指示器扩为三态（连接音频引擎/扫描音乐库/加载音乐库）；`NotifySongsSourceChanged` 在未 Ready 时改刷当前页（`RefreshDataSource`）——主界面先行显示后缓存库到达需补刷视图，成本与首次导航相同
- `Strings/*/Resources.resw` ×6：新增 `TitleBarTaskLoadingLibrary`；删除仅 LoadingGrid 使用的 `LoadingText.Text`
- 行为变更：启动无全屏 Loading 过渡，列表数据到达后流入（空库占位防闪逻辑不变）；版本弹窗与后台扫描指示器可能同时出现

## 2026-09-18 启动提速：IPC 连接前置、主界面不再等待播放引擎

- `Services/StartupCoordinator.cs`：AudioPlayer.exe 拉起与 IPC 连移到 `StartAsync` 最前（用户协议之前），与数据库初始化、协议阅读并行；主轨 `Task.WhenAll` 只等许可 + 缓存库，LoadingGrid 不再等 IPC；新增引擎并行轨（等 IPC+许可+缓存库后 `InitializeMusic` 首推），`RetryStartupCorrectionsAsync` 移至引擎轨完成后触发
- `Services/IpcService.cs`：新增 `IsConnected`；重试循环检测退出（Dispose）时静默返回，协议被拒绝不再触发 20 秒失败路径
- `ViewModel/AppViewModel.cs`：新增 `IsPlaybackEngineReady`（Ready + IPC 连接 + 首推完成，由 StartupCoordinator 统一刷新）、`IsIpcConnecting`、`IsTitleBarTaskActive/Text/Indeterminate`
- `Services/PlaybackCommands.cs`：`CanPlay` 改判 `IsPlaybackEngineReady`，并订阅其变化刷新全部命令可用性（SMTC/任务栏随 CanExecuteChanged 联动置灰）
- `ViewModel/Pages/MusicBrowseViewModel.cs`：`PlayMusic` 入口在引擎未就绪时直接返回（双击列表等无按钮入口）
- `Utils/BindUtils.cs`、`View/MainPage.xaml`、`View/PlayingDetailPage.xaml`：新增 `IsPlaybackEntryEnabled`/`IsSwitchEntryEnabled`，主界面与播放详情页播放控制按钮在引擎就绪前置灰；AppTitleBar ProgressRing 改为通用任务指示器，显示当前任务（连接音频引擎/扫描音乐库）+ 扫描百分比
- `Strings/*/Resources.resw` ×6：新增独立键 `TitleBarTaskConnecting`、`TitleBarTaskScanning`
- 行为变更：协议确认前即拉起 AudioPlayer.exe（拒绝协议或启动失败时由 ShutdownCoordinator 清理）；Ready 不再隐含 IPC 已连接，窗口可先显示、按钮置灰至引擎就绪

## 2026-09-18 Win2D 封面移除、动画文本块转正

- `View/PlayingDetailPage.xaml(.cs)`：移除 `aic:AlbumArtControl`、`xmlns:aic`、`CoverCacheBasePath` 赋值与 Dispose；经典 `ImageSwitcher` 封面改为始终加载（原与 Win2D 封面按开关联斥）
- `Model/SaveSettings.cs`、`ViewModel/AppViewModel.Settings.cs`、`Services/MusicDatabaseService.cs`：移除 `IsWin2dCoverImageControlEnable` 定义与读写；`IsWin2dAnimatedText` 默认 `true`
- `View/SubView/Settings/CoverBackgroundSettingsControl.xaml`、`Strings/*/Resources.resw` ×6：删除"Win2d封面"卡片与 `Win2dCover.Text`；`Win2dAnimatedTitle.Text` 去掉"（实验性）"
- `External/AnimatedWin2dControls` 实现按约定保留，仅主程序不再引用
- 兼容：旧 Settings.json 残留键被 System.Text.Json 默认跳过，下次保存自然消失；曾开启 Win2D 封面的用户回到经典封面

## 2026-09-18 逐字歌词默认启用

- `Model/SaveSettings.cs`、`Model/AppSettings.cs`、`ViewModel/AppViewModel.Settings.cs`：`EnableAdvancedLyricsEffect`（主界面逐字特效）、`IsDesktopLyricsKaraokeEnabled`（桌面歌词逐字渲染器）默认 `false` → `true`
- `DesktopLyrics/DesktopLyricsViewModel.cs`：同步"默认关"注释
- 语义：仅对全新安装生效；存量用户 Settings.json 中显式保存的值优先生效，不做强制迁移
