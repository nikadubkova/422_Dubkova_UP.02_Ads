using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace _422_Dubkova_UP_Ads.Pages
{
    /// <summary>
    /// Логика взаимодействия для MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private Entities _context;
        private List<dynamic> _allAds;
        private bool _isLoading = false;

        public MainPage()
        {
            InitializeComponent();
            InitializePage();
        }

        private async void InitializePage()
        {
            try
            {
                _context = new Entities();
                await LoadFilters();
                await LoadAds();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка инициализации страницы: {ex.Message}", "Ошибка загрузки");
            }
        }

        private async Task LoadFilters()
        {
            try
            {
                var cities = await _context.city.OrderBy(c => c.city1).ToListAsync();
                var categories = await _context.category.OrderBy(c => c.name).ToListAsync();
                var types = await _context.type.OrderBy(t => t.type1).ToListAsync();
                var statuses = await _context.status.OrderBy(s => s.status1).ToListAsync();

                var allCities = new List<city> { new city { id = 0, city1 = "Все города" } };
                allCities.AddRange(cities);

                var allCategories = new List<category> { new category { id = 0, name = "Все категории" } };
                allCategories.AddRange(categories);

                var allTypes = new List<type> { new type { id = 0, type1 = "Все типы" } };
                allTypes.AddRange(types);

                var allStatuses = new List<status> { new status { id = 0, status1 = "Все статусы" } };
                allStatuses.AddRange(statuses);

                cmbCity.ItemsSource = allCities;
                cmbCategory.ItemsSource = allCategories;
                cmbType.ItemsSource = allTypes;
                cmbStatus.ItemsSource = allStatuses;

                cmbCity.SelectedIndex = 0;
                cmbCategory.SelectedIndex = 0;
                cmbType.SelectedIndex = 0;
                cmbStatus.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка загрузки фильтров: {ex.Message}", "Ошибка");
            }
        }

        private async Task LoadAds()
        {
            if (_isLoading) return;

            _isLoading = true;
            ShowLoadingState("Загрузка объявлений...");

            try
            {
                var ads = await _context.ads_data
                    .Include(a => a.city)
                    .Include(a => a.category1)
                    .Include(a => a.type)
                    .Include(a => a.status)
                    .Include(a => a.user)
                    .OrderByDescending(a => a.ad_post_date)
                    .ToListAsync();

                _allAds = ads.Select(ad => new
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
                    User = ad.user,

                    HasImage = !string.IsNullOrEmpty(ad.ad_image_path),
                    ImageSource = SafeLoadImage(ad.ad_image_path),
                    // статусный цвет: используем ресурсы приложения (AccentBrush для активных, SecondaryTextBrush для остальных)
                    StatusColor = (ad.status != null && ad.status.status1 == "Активно")
                        ? (Brush)Application.Current.Resources["SuccessBrush"]
                        : (Brush)Application.Current.Resources["SecondaryTextBrush"]
                }).Cast<dynamic>().ToList();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось загрузить объявления: {ex.Message}", "Ошибка загрузки");
            }
            finally
            {
                _isLoading = false;
                HideLoadingState();
            }
        }

        /// <summary>
        /// Попытка загрузить изображение: возвращает BitmapImage или null.
        /// </summary>
        private ImageSource SafeLoadImage(string imagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                    return LoadDefaultImage();

                // Если путь относительный — ищем в нескольких местах
                var full = GetFullImagePath(imagePath);
                if (!string.IsNullOrEmpty(full) && File.Exists(full))
                    return CreateBitmap(full);

                var alt = FindImageInAlternativeLocations(imagePath);
                if (!string.IsNullOrEmpty(alt) && File.Exists(alt))
                    return CreateBitmap(alt);

                return LoadDefaultImage();
            }
            catch
            {
                return LoadDefaultImage();
            }
        }

        private ImageSource LoadDefaultImage()
        {
            // изображение-заглушка в ресурсах проекта
            try
            {
                var defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "заглушка.jpg");
                if (File.Exists(defaultPath))
                    return CreateBitmap(defaultPath);
            }
            catch { }
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
            catch
            {
                return null;
            }
        }

        private string GetFullImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            if (Path.IsPathRooted(imagePath))
                return imagePath;

            // Пробуем относительный путь относительно каталога приложения или проекта
            var candidate1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imagePath.TrimStart('\\', '/'));
            if (File.Exists(candidate1)) return candidate1;

            var candidate2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", imagePath.TrimStart('\\', '/'));
            if (File.Exists(candidate2)) return candidate2;

            // Попытка собрать путь относительно корня проекта (если запуск из bin)
            var projectDir = GetProjectDirectory();
            if (!string.IsNullOrEmpty(projectDir))
            {
                var candidate3 = Path.Combine(projectDir, "Images", imagePath.TrimStart('\\', '/'));
                if (File.Exists(candidate3)) return candidate3;
            }

            return null;
        }

        private string FindImageInAlternativeLocations(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
                return null;

            var clean = imagePath.TrimStart('\\', '/');
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, clean),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", clean),
                Path.Combine(GetProjectDirectory() ?? string.Empty, "Images", clean),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "_422_Dubkova_UP_Ads", "Images", clean),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "ads", Path.GetFileName(clean))
            };

            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && File.Exists(c))
                    return c;
            }

            return null;
        }

        private string GetProjectDirectory()
        {
            try
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var binDir = Path.GetDirectoryName(exePath);
                var dir = Directory.GetParent(binDir)?.Parent; // ...\bin\Debug\net...\ -> parent -> project root
                return dir?.FullName ?? Directory.GetCurrentDirectory();
            }
            catch
            {
                return Directory.GetCurrentDirectory();
            }
        }

        private void ApplyFilters()
        {
            if (_allAds == null) return;

            var filtered = _allAds.AsEnumerable();

            // Поиск
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                var search = txtSearch.Text.Trim().ToLowerInvariant();
                filtered = filtered.Where(ad =>
                    (ad.ad_title != null && ad.ad_title.ToLowerInvariant().Contains(search)) ||
                    (ad.ad_description != null && ad.ad_description.ToLowerInvariant().Contains(search)));
            }

            // Город
            if (cmbCity.SelectedItem is city selCity && selCity.id != 0)
            {
                filtered = filtered.Where(ad => ad.city_id == selCity.id);
            }

            // Категория
            if (cmbCategory.SelectedItem is category selCat && selCat.id != 0)
            {
                filtered = filtered.Where(ad => ad.category == selCat.id);
            }

            // Тип
            if (cmbType.SelectedItem is type selType && selType.id != 0)
            {
                filtered = filtered.Where(ad => ad.ad_type_id == selType.id);
            }

            // Статус
            if (cmbStatus.SelectedItem is status selStatus && selStatus.id != 0)
            {
                filtered = filtered.Where(ad => ad.ad_status_id == selStatus.id);
            }

            var list = filtered.ToList();
            itemsAds.ItemsSource = list;
            UpdateStatusBar(list.Count);
        }

        private void UpdateStatusBar(int count)
        {
            lblCount.Text = $"Найдено объявлений: {count}";

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                parts.Add($"поиск: \"{txtSearch.Text}\"");

            if (cmbCity.SelectedItem is city c && c.id != 0)
                parts.Add($"город: {c.city1}");
            if (cmbCategory.SelectedItem is category cat && cat.id != 0)
                parts.Add($"категория: {cat.name}");
            if (cmbType.SelectedItem is type t && t.id != 0)
                parts.Add($"тип: {t.type1}");
            if (cmbStatus.SelectedItem is status s && s.id != 0)
                parts.Add($"статус: {s.status1}");

            lblFilterInfo.Text = parts.Any() ? $"Фильтры: {string.Join(", ", parts)}" : "Фильтры не применены";
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtSearch.Text))
                txtHintSearch.Visibility = Visibility.Collapsed;
            else
                txtHintSearch.Visibility = Visibility.Visible;

            if (_allAds != null)
                ApplyFilters();
        }

        private void txtHintSearch_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            txtSearch.Focus();
        }


        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allAds != null)
                ApplyFilters();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = string.Empty;
            cmbCity.SelectedIndex = 0;
            cmbCategory.SelectedIndex = 0;
            cmbType.SelectedIndex = 0;
            cmbStatus.SelectedIndex = 0;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadAds();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var loginPage = new LoginPage();
                NavigationService.Navigate(loginPage);
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Не удалось открыть страницу авторизации: {ex.Message}", "Ошибка");
            }
        }

        private void ShowLoadingState(string message)
        {
            lblStatus.Text = message;
        }

        private void HideLoadingState()
        {
            lblStatus.Text = "Готово";
        }

        private void ShowErrorMessage(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            lblStatus.Text = "Произошла ошибка";
        }
    }
}
