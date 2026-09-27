using System.Windows;

using MissingFiles.Core;

namespace MissingFiles.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = ProductInfo.DisplayName;
    }
}
