using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;

namespace WinUIMusicPlayer.Controls;

/// <summary>静态图片合成层；只在换图时读取文件，调节模糊和缩放不重新解码图片。</summary>
public sealed class WindowBackgroundImage : Grid, IDisposable
{
    public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register(
        nameof(SourcePath), typeof(string), typeof(WindowBackgroundImage), new PropertyMetadata(string.Empty, OnSourceChanged));
    public static readonly DependencyProperty BlurAmountProperty = DependencyProperty.Register(
        nameof(BlurAmount), typeof(double), typeof(WindowBackgroundImage), new PropertyMetadata(20d, OnBlurChanged));
    public static readonly DependencyProperty HasImageProperty = DependencyProperty.Register(
        nameof(HasImage), typeof(bool), typeof(WindowBackgroundImage), new PropertyMetadata(false));

    public string SourcePath { get => (string)GetValue(SourcePathProperty); set => SetValue(SourcePathProperty, value); }
    public double BlurAmount { get => (double)GetValue(BlurAmountProperty); set => SetValue(BlurAmountProperty, value); }
    public bool HasImage { get => (bool)GetValue(HasImageProperty); private set => SetValue(HasImageProperty, value); }
    public event EventHandler? LoadFailed;

    private readonly AccessibilitySettings _accessibility = new();
    private CompositionSurfaceBrush? _surfaceBrush;
    private CompositionEffectBrush? _blurBrush;
    private SpriteVisual? _visual;
    private LoadedImageSurface? _surface;
    private IRandomAccessStream? _stream;
    private int _loadVersion;
    private bool _active, _disposed, _highContrast;

    public WindowBackgroundImage()
    {
        IsHitTestVisible = false;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (WindowBackgroundImage)sender;
        if (control._active) _ = control.LoadImageAsync();
    }

    private static void OnBlurChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((WindowBackgroundImage)sender).UpdateBlur();

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_disposed || _active) return;
        _active = true;
        _highContrast = _accessibility.HighContrast;
        _ = LoadImageAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _active = false;
        ReleaseResources();
    }

    public void RefreshForAccessibility()
    {
        bool highContrast = _accessibility.HighContrast;
        if (_highContrast == highContrast) return;
        _highContrast = highContrast;
        if (_active && !_disposed) _ = LoadImageAsync();
    }

    private async Task LoadImageAsync()
    {
        ReleaseResources();
        int version = _loadVersion;
        if (!_active || _disposed || _accessibility.HighContrast || string.IsNullOrWhiteSpace(SourcePath)) return;
        IRandomAccessStream? stream = null;
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(SourcePath);
            if (version != _loadVersion) return;
            stream = await file.OpenReadAsync();
            if (version != _loadVersion) return;
            // WIC metadata validation reports unsupported/corrupt files even when the
            // composition loader does not publish LoadCompleted for that format.
            await BitmapDecoder.CreateAsync(stream);
            if (version != _loadVersion) return;
            stream.Seek(0);
            var compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _surfaceBrush = compositor.CreateSurfaceBrush();
            _surfaceBrush.Stretch = CompositionStretch.UniformToFill;
            using var effect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 0,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("Image")
            };
            using var factory = compositor.CreateEffectFactory(effect, new[] { "Blur.BlurAmount" });
            _blurBrush = factory.CreateBrush();
            _blurBrush.SetSourceParameter("Image", _surfaceBrush);
            _visual = compositor.CreateSpriteVisual();
            _visual.Brush = _blurBrush;
            _visual.Size = new Vector2((float)ActualWidth, (float)ActualHeight);
            _visual.IsVisible = false;
            // 限制解码尺寸，避免相机原图长期占据全分辨率纹理。
            _surface = LoadedImageSurface.StartLoadFromStream(stream, new Size(2560, 2560));
            _stream = stream;
            stream = null;
            _surface.LoadCompleted += OnLoadCompleted;
            _surfaceBrush.Surface = _surface;
            UpdateBlur();
            ElementCompositionPreview.SetElementChildVisual(this, _visual);
        }
        catch (Exception)
        {
            if (version != _loadVersion) return;
            ReleaseResources();
            LoadFailed?.Invoke(this, EventArgs.Empty);
        }
        finally { stream?.Dispose(); }
    }

    private void OnLoadCompleted(LoadedImageSurface sender, LoadedImageSourceLoadCompletedEventArgs args)
    {
        if (!_active || _disposed || !ReferenceEquals(sender, _surface)) return;
        _stream?.Dispose();
        _stream = null;
        if (args.Status != LoadedImageSourceLoadStatus.Success)
        {
            ReleaseResources();
            LoadFailed?.Invoke(this, EventArgs.Empty);
            return;
        }
        HasImage = true;
        if (_visual is not null) _visual.IsVisible = true;
    }

    private void UpdateBlur()
    {
        if (_blurBrush is not null)
            _blurBrush.Properties.InsertScalar("Blur.BlurAmount", (float)(double.IsFinite(BlurAmount) ? Math.Clamp(BlurAmount, 0, 100) : 20));
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (_visual is not null) _visual.Size = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
    }

    private void ReleaseResources()
    {
        ++_loadVersion;
        HasImage = false;
        ElementCompositionPreview.SetElementChildVisual(this, null);
        if (_surface is not null) _surface.LoadCompleted -= OnLoadCompleted;
        _visual?.Dispose();
        _visual = null;
        _blurBrush?.Dispose();
        _blurBrush = null;
        _surfaceBrush?.Dispose();
        _surfaceBrush = null;
        _surface?.Dispose();
        _surface = null;
        _stream?.Dispose();
        _stream = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _active = false;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnSizeChanged;
        ReleaseResources();
    }
}
