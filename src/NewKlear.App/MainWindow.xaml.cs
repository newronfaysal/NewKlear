using NewKlear.App.Services;
using Wpf.Ui.Controls;

namespace NewKlear.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        DialogService.Host = DialogHost;
    }
}
