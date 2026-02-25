using Microsoft.UI.Xaml;

namespace helvety.screenshots
{
    public partial class App : Application
    {
        private Window? _window;
        internal static Window? MainAppWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            MainAppWindow = _window;
            _window.Activate();
        }
    }
}
