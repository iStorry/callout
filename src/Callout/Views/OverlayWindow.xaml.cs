using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Callout.Interop;
using Callout.ViewModels;

namespace Callout.Views;

/// <summary>
/// A separate top-most window drawn over the game. It never touches the game process,
/// so anti-cheat has nothing to object to; it needs the game in Windowed Fullscreen.
/// </summary>
internal partial class OverlayWindow : Window
{
    public static readonly DependencyProperty IsClickThroughProperty = DependencyProperty.Register(
        nameof(IsClickThrough),
        typeof(bool),
        typeof(OverlayWindow),
        new PropertyMetadata(false, OnIsClickThroughChanged));

    public OverlayWindow(OverlayViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        SetBinding(IsClickThroughProperty, new Binding(nameof(OverlayViewModel.IsLocked)));
    }

    public bool IsClickThrough
    {
        get => (bool)GetValue(IsClickThroughProperty);
        set => SetValue(IsClickThroughProperty, value);
    }

    private nint Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Hidden from Alt+Tab and never steals focus from the game.
        WindowStyles.Set(Handle, WindowStyles.Extended.ToolWindow | WindowStyles.Extended.NoActivate, enabled: true);
        ApplyClickThrough();
    }

    private static void OnIsClickThroughChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is OverlayWindow overlay)
        {
            overlay.ApplyClickThrough();
        }
    }

    private void ApplyClickThrough()
    {
        if (Handle != 0)
        {
            WindowStyles.Set(Handle, WindowStyles.Extended.Transparent, IsClickThrough);
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
