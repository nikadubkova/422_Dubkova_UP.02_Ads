using _422_Dubkova_UP_Ads.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading.Tasks;
using Nav = _422_Dubkova_UP_Ads.Services.NavigationService;


namespace _422_Dubkova_UP_Ads.Pages
{
    /// <summary>
    /// Логика взаимодействия для AdsManagementPage.xaml
    /// </summary>
    public partial class AdsManagementPage : Page
    {
        private Entities _context;
        private bool _isLoading = false;

        public AdsManagementPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private void InitializePage()
        {
            try
            {
                _context = new Entities();
                LoadUserInfo();
                _ = LoadAdsAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка инициализации страницы: {ex.Message}");
            }
        }

        private void LoadUserInfo()
        {
            try
            {
                lblUserInfo.Text = $"Пользователь: {AuthService.CurrentUser.user_login}";
            }
            catch
            {
                lblUserInfo.Text = "Пользователь: -";
            }
        }

        private async Task LoadAdsAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            ShowLoadingState("Загрузка объявлений...");

            try
            {
                var userAds = await _context.ads_data
                    .Include(a => a.city)
                    .Include(a => a.category1)
                    .Include(a => a.type)
                    .Include(a => a.status)
                    .Where(a => a.user_id == AuthService.CurrentUser.id)
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                var adsWithDetails = userAds.Select(ad => new
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
                    ImageSource = SafeLoadImage(ad.ad_image_path),
                    StatusColor = (ad.status != null && ad.status.status1 == "Активно")
                        ? (Brush)Application.Current.Resources["SuccessBrush"]
                        : (Brush)Application.Current.Resources["SecondaryTextBrush"]
                }).ToList();

                itemsAds.ItemsSource = adsWithDetails;
                UpdateUIState(adsWithDetails.Count);
                lblStatus.Text = "Данные успешно загружены";
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось загрузить объявления: {ex.Message}");
                UpdateUIState(0);
            }
            finally
            {
                _isLoading = false;
                HideLoadingState();
            }
        }

        private ImageSource SafeLoadImage(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return null;

            try
            {
                string full = GetFullImagePath(imagePath);
                if (File.Exists(full)) return CreateBitmap(full);

                // альтернативные места
                string alt = FindImageInAlternativeLocations(imagePath);
                if (!string.IsNullOrEmpty(alt) && File.Exists(alt)) return CreateBitmap(alt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
            }

            return null;
        }

        private ImageSource CreateBitmap(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private string GetFullImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return null;
            if (Path.IsPathRooted(imagePath)) return imagePath;

            string projectDirectory = GetProjectDirectory();
            string clean = imagePath.TrimStart('\\', '/');
            return Path.Combine(projectDirectory, "Images", clean);
        }

        private string FindImageInAlternativeLocations(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return null;
            string clean = imagePath.TrimStart('\\', '/');

            string[] possible = {
                Path.Combine(GetProjectDirectory(), "Images", clean),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", clean),
                Path.Combine(GetProjectDirectory(), "Images", "ads", Path.GetFileName(clean)),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "ads", Path.GetFileName(clean))
            };

            foreach (var p in possible) if (File.Exists(p)) return p;
            return null;
        }

        private string GetProjectDirectory()
        {
            try
            {
                var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var bin = Path.GetDirectoryName(exe);
                var parent = Directory.GetParent(bin)?.Parent;
                return parent?.FullName ?? Directory.GetCurrentDirectory();
            }
            catch { return Directory.GetCurrentDirectory(); }
        }

        private void UpdateUIState(int count)
        {
            lblCount.Text = $"Объявлений: {count}";
            emptyStatePanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            itemsAds.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowLoadingState(string message)
        {
            loadingPanel.Visibility = Visibility.Visible;
            lblLoading.Text = message;
            itemsAds.IsEnabled = false;
        }

        private void HideLoadingState()
        {
            loadingPanel.Visibility = Visibility.Collapsed;
            itemsAds.IsEnabled = true;
        }

        private void AdCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (sender is FrameworkElement fe && fe.DataContext != null)
                {
                    var idProp = fe.DataContext.GetType().GetProperty("id");
                    if (idProp != null)
                    {
                        int id = (int)idProp.GetValue(fe.DataContext);
                        EditAdById(id);
                    }
                }
            }
        }

        private void EditCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag != null && int.TryParse(b.Tag.ToString(), out int id))
                EditAdById(id);
        }

        private void DeleteCardButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag != null && int.TryParse(b.Tag.ToString(), out int id))
                DeleteAdById(id);
        }

        private void EditAdById(int adId)
        {
            try
            {
                var full = _context.ads_data.Find(adId);
                if (full != null)
                {
                    var page = new AddEditAdPage(full);
                    page.AdSaved += OnAdSaved;
                    Nav.NavigateTo(page);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть объявление: {ex.Message}");
            }
        }

        private void DeleteAdById(int adId)
        {
            try
            {
                var ad = _context.ads_data.Find(adId);
                if (ad == null) return;

                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить объявление?\n\nЗаголовок: {ad.ad_title}\nДата: {ad.ad_post_date:dd.MM.yyyy}\nЦена: {ad.price}₽",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _ = PerformDeleteAdAsync(ad);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка при удалении: {ex.Message}");
            }
        }

        private async Task PerformDeleteAdAsync(ads_data ad)
        {
            ShowLoadingState("Удаление объявления...");
            try
            {
                if (!string.IsNullOrEmpty(ad.ad_image_path))
                    TryDeleteImage(ad.ad_image_path);

                _context.ads_data.Remove(ad);
                await _context.SaveChangesAsync();

                await LoadAdsAsync();
                lblStatus.Text = "Объявление удалено";
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось удалить объявление: {ex.Message}");
            }
            finally
            {
                HideLoadingState();
            }
        }

        private void TryDeleteImage(string imagePath)
        {
            try
            {
                var full = GetFullImagePath(imagePath);
                if (!string.IsNullOrEmpty(full) && File.Exists(full)) File.Delete(full);

                var alt = FindImageInAlternativeLocations(imagePath);
                if (!string.IsNullOrEmpty(alt) && File.Exists(alt)) File.Delete(alt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления файла: {ex.Message}");
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var page = new AddEditAdPage();
                page.AdSaved += OnAdSaved;
                Nav.NavigateTo(page);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть форму создания: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadAdsAsync();
        }

        private void CompletedAdsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var page = new CompletedAdsPage();
                Nav.NavigateTo(page);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть страницу завершенных: {ex.Message}");
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Вы уверены, что хотите выйти?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes) AuthService.Logout();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Nav.CanGoBack) Nav.GoBack();
            else LogoutButton_Click(sender, e);
        }

        private void OnAdSaved() => _ = LoadAdsAsync();

        private void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            lblStatus.Text = "Произошла ошибка";
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _context?.Dispose();
        }

        private void AllAdsButton_Click(object sender, RoutedEventArgs e)
        {
            var allAdsPage = new MainPage();
            NavigationService.Navigate(allAdsPage);
        }
    }
}
