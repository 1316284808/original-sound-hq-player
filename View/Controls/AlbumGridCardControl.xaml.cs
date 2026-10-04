using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.ViewModel;

namespace WinUIMusicPlayer.View.Controls;

public sealed partial class AlbumGridCardControl : UserControl
{
    public static readonly DependencyProperty MusicProperty = DependencyProperty.Register(
        nameof(Music), typeof(Music), typeof(AlbumGridCardControl), new PropertyMetadata(null));

    public Music? Music
    {
        get => (Music?)GetValue(MusicProperty);
        set => SetValue(MusicProperty, value);
    }

    private AlbumViewModel ViewModel { get; }

    public AlbumGridCardControl()
    {
        ViewModel = App.Services.GetRequiredService<AlbumViewModel>();
        InitializeComponent();
    }

    private void PlayAlbum_Click(object sender, RoutedEventArgs e)
    {
        if (Music is not null)
        {
            _ = ViewModel.PlayAlbum(Music);
        }
    }

    /// <summary>阻止按钮点击冒泡成 GridView 的 ItemClick，避免播放的同时又进入专辑详情。</summary>
    private void PlayAlbum_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;
}
