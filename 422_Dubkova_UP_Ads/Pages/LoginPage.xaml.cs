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

namespace _422_Dubkova_UP_Ads.Pages
{
    /// <summary>
    /// Логика взаимодействия для LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Page
    {
        private bool _isLoggingIn = false;

        public LoginPage()
        {
            InitializeComponent();
            txtLogin.Focus();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn) return;

            string login = txtLogin.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(login))
            {
                ShowError("Введите логин!");
                txtLogin.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Введите пароль!");
                txtPassword.Focus();
                return;
            }

            SetLoginState(true, sender);

            try
            {
                bool success = await Task.Run(() => AuthService.Login(login, password));

                if (!success)
                {
                    ShowError("Неверный логин или пароль");
                    txtPassword.Password = "";
                    txtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError($"Произошла ошибка при подключении к базе данных: {ex.Message}. " +
                          "Проверьте подключение к серверу и повторите попытку.");
            }
            finally
            {
                SetLoginState(false, sender);
            }
        }

        private void SetLoginState(bool loggingIn, object sender)
        {
            _isLoggingIn = loggingIn;

            var button = sender as Button;

            txtLogin.IsEnabled = !loggingIn;
            txtPassword.IsEnabled = !loggingIn;

            if (button != null)
            {
                button.Content = "Войти";
                button.IsEnabled = true;
                button.Background = (Brush)Application.Current.Resources["AccentBrush"];
            }
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            errorBorder.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            errorBorder.Visibility = Visibility.Collapsed;
        }

        // подсветка полей при фокусе

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            HideError();
            if (sender is TextBox tb)
            {
                tb.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.BorderBrush = (Brush)Application.Current.Resources["ControlBorderBrush"];
            }
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            HideError();
            if (sender is PasswordBox pb)
            {
                pb.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
            }
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox pb)
            {
                pb.BorderBrush = (Brush)Application.Current.Resources["ControlBorderBrush"];
            }
        }

        // ENTER

        private void txtLogin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                txtPassword.Focus();
        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isLoggingIn)
                LoginButton_Click(sender, e);
        }

    }
}
