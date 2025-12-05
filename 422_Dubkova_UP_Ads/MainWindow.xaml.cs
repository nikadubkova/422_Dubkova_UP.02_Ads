using _422_Dubkova_UP_Ads.Services;
using AppNav = _422_Dubkova_UP_Ads.Services.NavigationService;
using _422_Dubkova_UP_Ads.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using _422_Dubkova_UP_Ads.Pages;

namespace _422_Dubkova_UP_Ads
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AppNav.Initialize(MainFrame);

            AuthService.UserLoggedIn += OnUserLoggedIn;
            AuthService.UserLoggedOut += OnUserLoggedOut;

            NavigateToWelcomePage();
        }

        private void NavigateToWelcomePage()
        {
            AppNav.NavigateTo(new WelcomePage());
        }

        private void OnUserLoggedIn(user user)
        {
            Dispatcher.Invoke(() =>
            {
                AppNav.NavigateTo(new AdsManagementPage());
            });
        }

        private void OnUserLoggedOut()
        {
            Dispatcher.Invoke(() =>
            {
                while (MainFrame.CanGoBack)
                    MainFrame.RemoveBackEntry();

                NavigateToWelcomePage();
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            // отписываемся от событий
            AuthService.UserLoggedIn -= OnUserLoggedIn;
            AuthService.UserLoggedOut -= OnUserLoggedOut;
            base.OnClosed(e);
        }
    }
}
