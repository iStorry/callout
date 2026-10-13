using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Callout.Interop;
using Callout.ViewModels;

namespace Callout.Views;

internal partial class ChatWindow : Window
{
    private readonly ChatViewModel _viewModel;
    private bool _followLatest = true;
    private bool _closingForGood;

    public ChatWindow(ChatViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        DarkTitleBar.Apply(this);
    }

    /// <summary>Closes the window on app exit; the title-bar close button only hides it.</summary>
    public void CloseForGood()
    {
        _closingForGood = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!_closingForGood)
        {
            e.Cancel = true;
            _viewModel.IsOpen = false;
        }
    }

    /// <summary>Sticks to the newest line unless the user scrolled up to read history.</summary>
    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.OriginalSource is not ScrollViewer viewer)
        {
            return;
        }

        if (e.ExtentHeightChange == 0)
        {
            _followLatest = viewer.VerticalOffset >= viewer.ScrollableHeight;
        }
        else if (_followLatest)
        {
            viewer.ScrollToEnd();
        }
    }
}
