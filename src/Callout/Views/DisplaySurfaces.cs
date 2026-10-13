using System.ComponentModel;
using Callout.ViewModels;

namespace Callout.Views;

/// <summary>
/// Shows the chat window or the overlay to match the chosen display mode.
/// Each window is created the first time it is needed, so an unused mode costs nothing.
/// </summary>
internal sealed class DisplaySurfaces
{
    private readonly MainViewModel _main;
    private ChatWindow? _chatWindow;
    private OverlayWindow? _overlayWindow;

    public DisplaySurfaces(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += OnSurfaceStateChanged;
        _main.Chat.PropertyChanged += OnSurfaceStateChanged;
        Update();
    }

    public void CloseAll()
    {
        _main.PropertyChanged -= OnSurfaceStateChanged;
        _main.Chat.PropertyChanged -= OnSurfaceStateChanged;
        _chatWindow?.CloseForGood();
        _overlayWindow?.Close();
    }

    private void OnSurfaceStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsChatMode) or nameof(MainViewModel.IsOverlayMode) or nameof(ChatViewModel.IsOpen))
        {
            Update();
        }
    }

    private void Update()
    {
        if (_main.IsChatMode && _main.Chat.IsOpen)
        {
            _chatWindow ??= new ChatWindow(_main.Chat);
            _chatWindow.Show();
        }
        else
        {
            _chatWindow?.Hide();
        }

        if (_main.IsOverlayMode)
        {
            _overlayWindow ??= new OverlayWindow(_main.Overlay);
            _overlayWindow.Show();
        }
        else
        {
            _overlayWindow?.Hide();
        }
    }
}
