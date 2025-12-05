using _422_Dubkova_UP_Ads.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
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
    /// Логика взаимодействия для CompletedAdsPage.xaml
    /// </summary>
    public partial class CompletedAdsPage : Page
    {
        private Entities _context;
        private bool _isLoading = false;
        private decimal _totalProfit = 0;

        public CompletedAdsPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private void InitializePage()
        {
            try
            {
                _context = new Entities();
                LoadCompletedAds();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка инициализации страницы: {ex.Message}");
            }
        }


        // ================= ZAGRUZKA OBIAVLENII =================
        private async void LoadCompletedAds()
        {
            if (_isLoading) return;

            _isLoading = true;
            ShowLoadingState("Загрузка завершенных объявлений...");

            try
            {
                int currentUserId = AuthService.CurrentUser?.id ?? -1;

                var completedAds = await _context.ads_data
                    .Include(a => a.city)
                    .Include(a => a.category1)
                    .Include(a => a.type)
                    .Include(a => a.status)
                    .Where(a => a.user_id == currentUserId && a.ad_status_id == 2)
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                _totalProfit = completedAds.Sum(ad => ad.price);

                var adsList = completedAds.Select(ad => new
                {
                    ad.id,
                    ad.ad_title,
                    ad.ad_description,
                    ad.ad_post_date,
                    ad.city_id,
                    ad.category,
                    ad.ad_type_id,
                    ad.ad_status_id,
                    ad.price,
                    ad.user_id,
                    ad.ad_image_path,

                    City = ad.city,
                    Category = ad.category1,
                    AdType = ad.type,
                    AdStatus = ad.status,

                    HasImage = !string.IsNullOrEmpty(ad.ad_image_path),
                    ImageSource = LoadImageSafe(ad.ad_image_path),
                    ProfitAmount = ad.price
                }).ToList();

                Dispatcher.Invoke(() =>
                {
                    itemsCompletedAds.ItemsSource = adsList;
                    UpdateUIState(adsList.Count, _totalProfit);
                    lblStatus.Text = "Готово";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    ShowErrorMessage($"Ошибка загрузки данных: {ex.Message}");
                    UpdateUIState(0, 0);
                });
            }
            finally
            {
                _isLoading = false;
                HideLoadingState();
            }
        }



        // ================= ZAGRUZKA IZOBRAZHENII =================
        private ImageSource LoadImageSafe(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return null;

                string normalized = path.TrimStart('/', '\\');

                string[] possible =
                {
                    System.IO.Path.Combine(GetProjectRoot(), "Images", normalized),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", normalized),
                    System.IO.Path.Combine(GetProjectRoot(), "Images", "ads", normalized),
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "ads", normalized)
                };

                foreach (string p in possible)
                {
                    if (File.Exists(p))
                    {
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(p, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }


        // путь к корню проекта
        private string GetProjectRoot()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            DirectoryInfo dir = new DirectoryInfo(System.IO.Path.GetDirectoryName(exePath));

            // bin/Debug/
            if (dir.Parent != null && dir.Parent.Parent != null)
                return dir.Parent.Parent.FullName;

            return Environment.CurrentDirectory;
        }



        // ================= UI STATE =================
        private void UpdateUIState(int count, decimal profit)
        {
            lblCount.Text = $"Завершенных объявлений: {count}";
            lblTotalProfit.Text = $"Общая прибыль: {profit}₽";
            lblProfitInfo.Text = $"Общая прибыль: {profit}₽";

            emptyStatePanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            itemsCompletedAds.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowLoadingState(string msg)
        {
            loadingPanel.Visibility = Visibility.Visible;
            lblLoading.Text = msg;
            itemsCompletedAds.IsEnabled = false;
        }

        private void HideLoadingState()
        {
            loadingPanel.Visibility = Visibility.Collapsed;
            itemsCompletedAds.IsEnabled = true;
        }



        // ================= BUTTONS =================
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadCompletedAds();
        }

        private void ManageAdsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AdsManagementPage());
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                NavigationService.Navigate(new AdsManagementPage());
        }



        // ================= ERRORS =================
        private void ShowErrorMessage(string msg)
        {
            MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            lblStatus.Text = "Ошибка";
        }
    }
}
