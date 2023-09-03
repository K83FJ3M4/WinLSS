using Microsoft.UI.Xaml;

namespace WinLSS
{

    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
        }

        public void Initialize(Initialize initialize)
        {
            Content.Navigate(typeof(MainPage), initialize);
        }
    }
}
