using System.Windows;
using Callout.Interop;
using Callout.ViewModels;

namespace Callout.Views;

internal partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        DarkTitleBar.Apply(this);
    }
}
