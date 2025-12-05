using _422_Dubkova_UP_Ads.Services;
using Microsoft.Win32;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace _422_Dubkova_UP_Ads.Pages
{
    public partial class AddEditAdPage : Page
    {
        private Entities _context;
        private ads_data _ad;
        private bool _isNew = true;
        private bool _isLoading = false;

        private string _selectedImagePath = null;   // полный путь выбранного файла (перед сохранением)
        private string _currentImagePath = null;    // относительный путь, сохранённый в базе (например "ads/..")

        // Кэш справочников (как у рабочей команды)
        private static System.Collections.Generic.List<city> _cachedCities;
        private static System.Collections.Generic.List<category> _cachedCategories;
        private static System.Collections.Generic.List<type> _cachedTypes;
        private static System.Collections.Generic.List<status> _cachedStatuses;
        private static DateTime _lastCacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        public event Action AdSaved;

        // Конструктор: если ad == null — создание, иначе редактирование
        public AddEditAdPage(ads_data ad = null)
        {
            InitializeComponent();

            // контекст создаём один раз
            _context = new Entities();

            // Запускаем инициализацию асинхронно (не блокируем UI)
            _ = InitializeAsync(ad);
        }

        private async Task InitializeAsync(ads_data ad)
        {
            _isLoading = true;
            ShowLoadingState("Инициализация...");

            try
            {
                // Загрузим справочники (и кэшируем)
                await LoadReferenceData();

                if (ad == null)
                {
                    // новое объявление
                    _isNew = true;
                    _ad = new ads_data
                    {
                        user_id = AuthService.CurrentUser != null ? AuthService.CurrentUser.id : 0,
                        ad_post_date = DateTime.Today,
                        ad_status_id = 1
                    };

                    lblTitle.Text = "Создание объявления";
                }
                else
                {
                    // редактирование — подгружаем свежий экземпляр из контекста с Include(...)
                    _isNew = false;
                    var loaded = await _context.ads_data
                        .Include(a => a.city)
                        .Include(a => a.category1)
                        .Include(a => a.type)
                        .Include(a => a.status)
                        .FirstOrDefaultAsync(a => a.id == ad.id);

                    if (loaded != null)
                    {
                        _ad = loaded;
                    }
                    else
                    {
                        // на случай, если не удалось загрузить — создаём новый (без падения)
                        _isNew = true;
                        _ad = new ads_data
                        {
                            user_id = AuthService.CurrentUser != null ? AuthService.CurrentUser.id : 0,
                            ad_post_date = DateTime.Today,
                            ad_status_id = 1
                        };
                    }

                    lblTitle.Text = "Редактирование объявления";
                }

                // После того как справочники загружены и модель готова — заполняем UI
                SetupUiFromModel();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка инициализации: {ex.Message}");
            }
            finally
            {
                HideLoadingState();
                _isLoading = false;
            }
        }

        private async Task LoadReferenceData()
        {
            try
            {
                bool refresh =
                    _cachedCities == null ||
                    _cachedCategories == null ||
                    _cachedTypes == null ||
                    _cachedStatuses == null ||
                    (DateTime.Now - _lastCacheTime) > CacheDuration;

                if (refresh)
                {
                    var citiesTask = _context.city.AsNoTracking().ToListAsync();
                    var categoriesTask = _context.category.AsNoTracking().ToListAsync();
                    var typesTask = _context.type.AsNoTracking().ToListAsync();
                    var statusesTask = _context.status.AsNoTracking().ToListAsync();

                    await Task.WhenAll(citiesTask, categoriesTask, typesTask, statusesTask);

                    _cachedCities = citiesTask.Result;
                    _cachedCategories = categoriesTask.Result;
                    _cachedTypes = typesTask.Result;
                    _cachedStatuses = statusesTask.Result;

                    _lastCacheTime = DateTime.Now;
                }

                // Устанавливаем ItemsSource и DisplayMember/SelectedValuePath
                cmbCity.ItemsSource = _cachedCities;
                cmbCity.DisplayMemberPath = "city1";
                cmbCity.SelectedValuePath = "id";

                cmbCategory.ItemsSource = _cachedCategories;
                cmbCategory.DisplayMemberPath = "name";
                cmbCategory.SelectedValuePath = "id";

                cmbType.ItemsSource = _cachedTypes;
                cmbType.DisplayMemberPath = "type1";
                cmbType.SelectedValuePath = "id";

                cmbStatus.ItemsSource = _cachedStatuses;
                cmbStatus.DisplayMemberPath = "status1";
                cmbStatus.SelectedValuePath = "id";
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки справочников: {ex.Message}");
            }
        }

        private void SetupUiFromModel()
        {
            try
            {
                if (_ad == null)
                {
                    _ad = new ads_data
                    {
                        user_id = AuthService.CurrentUser != null ? AuthService.CurrentUser.id : 0,
                        ad_post_date = DateTime.Today,
                        ad_status_id = 1
                    };
                }

                lblTitle.Text = _isNew ? "Создание объявления" : "Редактирование объявления";

                txtTitle.Text = _ad.ad_title ?? "";
                txtDescription.Text = _ad.ad_description ?? "";
                dpDate.SelectedDate = _ad.ad_post_date == DateTime.MinValue ? DateTime.Today : (DateTime?)_ad.ad_post_date;
                txtPrice.Text = _ad.price != 0 ? _ad.price.ToString("F2") : "";

                _currentImagePath = _ad.ad_image_path;

                // Подставляем SelectedValue после того как ItemsSource уже установлен
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_ad.city_id != null && _ad.city_id > 0) cmbCity.SelectedValue = _ad.city_id;
                        if (_ad.category != null && _ad.category > 0) cmbCategory.SelectedValue = _ad.category;
                        if (_ad.ad_type_id != null && _ad.ad_type_id > 0) cmbType.SelectedValue = _ad.ad_type_id;
                        if (_ad.ad_status_id != null && _ad.ad_status_id > 0) cmbStatus.SelectedValue = _ad.ad_status_id;
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                // Загрузка превью изображения (если есть)
                if (!string.IsNullOrEmpty(_currentImagePath))
                {
                    LoadImagePreview(_currentImagePath);
                }
                else
                {
                    imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/заглушка.jpg"));
                    lblImageInfo.Text = "Изображение не выбрано";
                }

                UpdateProfitPanelVisibility();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка установки данных в UI: {ex.Message}");
            }
        }

        #region Image logic

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                    Multiselect = false
                };

                if (dlg.ShowDialog() == true)
                {
                    var fi = new FileInfo(dlg.FileName);
                    if (fi.Length > 5 * 1024 * 1024)
                    {
                        ShowError("Размер изображения не должен превышать 5 МБ");
                        return;
                    }

                    _selectedImagePath = dlg.FileName;
                    LoadImagePreview(_selectedImagePath);
                    lblImageInfo.Text = $"Файл: {fi.Name}\nРазмер: {(fi.Length / 1024.0):F1} КБ";
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка выбора изображения: {ex.Message}");
            }
        }

        private void RemoveImageButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedImagePath = null;
            _currentImagePath = null;
            _ad.ad_image_path = null;
            imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/заглушка.jpg"));
            lblImageInfo.Text = "Изображение удалено";
        }

        private void LoadImagePreview(string relativeOrFull)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativeOrFull))
                {
                    imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/заглушка.jpg"));
                    lblImageInfo.Text = "Изображение не выбрано";
                    return;
                }

                var full = GetFullImagePath(relativeOrFull);
                if (!string.IsNullOrEmpty(full) && File.Exists(full))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(full, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    imgPreview.Source = bmp;

                    var fi = new FileInfo(full);
                    lblImageInfo.Text = $"Файл: {fi.Name}\nРазмер: {(fi.Length / 1024.0):F1} КБ";
                    return;
                }

                // альтернативные места
                var alt = FindImageInAlternativeLocations(relativeOrFull);
                if (!string.IsNullOrEmpty(alt) && File.Exists(alt))
                {
                    var bmp2 = new BitmapImage();
                    bmp2.BeginInit();
                    bmp2.CacheOption = BitmapCacheOption.OnLoad;
                    bmp2.UriSource = new Uri(alt, UriKind.Absolute);
                    bmp2.EndInit();
                    bmp2.Freeze();
                    imgPreview.Source = bmp2;

                    var fi2 = new FileInfo(alt);
                    lblImageInfo.Text = $"Файл: {fi2.Name}\nРазмер: {(fi2.Length / 1024.0):F1} КБ";
                    return;
                }

                imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/заглушка.jpg"));
                lblImageInfo.Text = "Изображение не найдено";
            }
            catch
            {
                imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Images/заглушка.jpg"));
                lblImageInfo.Text = "Ошибка загрузки изображения";
            }
        }

        private string SaveImageToFolder(string sourceImagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceImagePath) || !File.Exists(sourceImagePath)) return null;

                string projectDirectory = GetProjectDirectory();
                string imagesFolder = Path.Combine(projectDirectory, "Images", "ads");
                if (!Directory.Exists(imagesFolder)) Directory.CreateDirectory(imagesFolder);

                string ext = Path.GetExtension(sourceImagePath);
                string fileName = $"ad_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
                string dest = Path.Combine(imagesFolder, fileName);

                File.Copy(sourceImagePath, dest, true);

                return Path.Combine("ads", fileName).Replace("\\", "/");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения изображения: {ex.Message}");
                return null;
            }
        }

        private void DeleteOldImage(string relative)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relative)) return;
                var full = GetFullImagePath(relative);
                if (!string.IsNullOrEmpty(full) && File.Exists(full)) File.Delete(full);

                var alt = FindImageInAlternativeLocations(relative);
                if (!string.IsNullOrEmpty(alt) && File.Exists(alt)) File.Delete(alt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления изображения: {ex.Message}");
            }
        }

        private string GetFullImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return null;

            // абсолютный путь
            try
            {
                if (Path.IsPathRooted(imagePath) && File.Exists(imagePath)) return imagePath;
            }
            catch { }

            // чистим слеши и проверяем разные варианты
            var clean = imagePath.Replace("\\", "/").TrimStart('/');
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", clean),
                Path.Combine(GetProjectDirectory(), "Images", clean),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, clean),
                Path.Combine(GetProjectDirectory(), "Images", "ads", Path.GetFileName(clean))
            };

            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            }

            return null;
        }

        private string FindImageInAlternativeLocations(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return null;
            var clean = imagePath.Replace("\\", "/").TrimStart('/');

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
                return parent?.FullName ?? AppDomain.CurrentDomain.BaseDirectory;
            }
            catch { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        #endregion

        #region Save / Validation

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            try
            {
                ShowLoadingState("Сохранение...");

                // если объект не привязан — создаём новый
                if (_ad == null)
                {
                    _ad = new ads_data
                    {
                        user_id = AuthService.CurrentUser != null ? AuthService.CurrentUser.id : 0,
                        ad_post_date = dpDate.SelectedDate ?? DateTime.Today
                    };
                    _isNew = true;
                }

                // прописываем поля из UI
                _ad.ad_title = txtTitle.Text.Trim();
                _ad.ad_description = txtDescription.Text.Trim();
                _ad.ad_post_date = dpDate.SelectedDate ?? DateTime.Today;

                if (cmbCity.SelectedValue != null) _ad.city_id = (int)cmbCity.SelectedValue;
                if (cmbCategory.SelectedValue != null) _ad.category = (int)cmbCategory.SelectedValue;
                if (cmbType.SelectedValue != null) _ad.ad_type_id = (int)cmbType.SelectedValue;
                if (cmbStatus.SelectedValue != null) _ad.ad_status_id = (int)cmbStatus.SelectedValue;

                if (decimal.TryParse(txtPrice.Text.Trim(), out decimal price)) _ad.price = price;
                else _ad.price = 0m;

                // изображение
                if (!string.IsNullOrEmpty(_selectedImagePath))
                {
                    // удалим старую картинку, если была
                    if (!string.IsNullOrEmpty(_currentImagePath))
                        DeleteOldImage(_currentImagePath);

                    var saved = SaveImageToFolder(_selectedImagePath);
                    if (!string.IsNullOrEmpty(saved))
                    {
                        _ad.ad_image_path = saved;
                        _currentImagePath = saved;
                    }
                }

                // если новый — добавляем
                if (_isNew)
                {
                    _context.ads_data.Add(_ad);
                }
                else
                {
                    try { _context.Entry(_ad).State = EntityState.Modified; } catch { }
                }

                // прибыль если нужно
                if (cmbStatus.SelectedValue != null && (int)cmbStatus.SelectedValue == 2 && int.TryParse(txtProfit.Text, out int profAmt))
                {
                    await ProcessProfit(profAmt);
                }

                await _context.SaveChangesAsync();

                ClearCache();

                AdSaved?.Invoke();

                MessageBox.Show(_isNew ? "Объявление успешно создано!" : "Изменения успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                if (NavigationService != null && NavigationService.CanGoBack) NavigationService.GoBack();
            }
            catch (DbUpdateException dbex)
            {
                string msg = $"Ошибка записи в базу: {dbex.Message}";
                if (dbex.InnerException != null) msg += $"\n{dbex.InnerException.Message}";
                ShowError(msg);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                HideLoadingState();
            }
        }

        private async Task ProcessProfit(int amount)
        {
            try
            {
                var profit = await _context.profit.FirstOrDefaultAsync(p => p.user_id == AuthService.CurrentUser.id);
                if (profit == null)
                {
                    profit = new profit { user_id = AuthService.CurrentUser.id, profit1 = amount };
                    _context.profit.Add(profit);
                }
                else
                {
                    profit.profit1 += amount;
                    _context.Entry(profit).State = EntityState.Modified;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessProfit error: {ex.Message}");
            }
        }

        private void ClearCache()
        {
            _cachedCities = null;
            _cachedCategories = null;
            _cachedTypes = null;
            _cachedStatuses = null;
            _lastCacheTime = DateTime.MinValue;
        }

        private bool ValidateInput()
        {
            HideError();

            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                ShowError("Введите заголовок.");
                txtTitle.Focus();
                return false;
            }

            if (dpDate.SelectedDate == null)
            {
                ShowError("Выберите дату публикации.");
                dpDate.Focus();
                return false;
            }

            if (dpDate.SelectedDate > DateTime.Today)
            {
                ShowError("Дата публикации не может быть в будущем.");
                dpDate.Focus();
                return false;
            }

            if (cmbCity.SelectedItem == null || cmbCategory.SelectedItem == null || cmbType.SelectedItem == null || cmbStatus.SelectedItem == null)
            {
                ShowError("Заполните все справочные поля (город, категория, тип, статус).");
                return false;
            }

            if (!decimal.TryParse(txtPrice.Text.Trim(), out decimal price) || price < 0)
            {
                ShowError("Введите корректную цену.");
                txtPrice.Focus();
                return false;
            }

            if (cmbStatus.SelectedValue != null && (int)cmbStatus.SelectedValue == 2)
            {
                if (string.IsNullOrWhiteSpace(txtProfit.Text) || !int.TryParse(txtProfit.Text.Trim(), out int pf) || pf < 0)
                {
                    ShowError("Для завершённого объявления укажите корректную полученную сумму.");
                    txtProfit.Focus();
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region UI helpers & events

        private void CancelButton_Click(object sender, RoutedEventArgs e) => NavigationService?.GoBack();
        private void BackButton_Click(object sender, RoutedEventArgs e) => CancelButton_Click(sender, e);

        private void ShowLoadingState(string message)
        {
            try { btnSave.Content = "⏳ Сохранение..."; } catch { }
        }
        private void HideLoadingState()
        {
            try { btnSave.Content = "Сохранить"; } catch { }
        }

        private void ShowError(string msg)
        {
            lblError.Text = msg;
            errorBorder.Visibility = Visibility.Visible;
        }

        private void HideError() => errorBorder.Visibility = Visibility.Collapsed;

        private void txtPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text) if (!char.IsDigit(c) && c != '.') { e.Handled = true; return; }
            var tb = sender as TextBox;
            if (tb != null && e.Text == "." && tb.Text.Contains(".")) e.Handled = true;
        }

        private void txtProfit_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text) if (!char.IsDigit(c)) { e.Handled = true; return; }
        }

        private void cmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateProfitPanelVisibility();
        }

        private void UpdateProfitPanelVisibility()
        {
            try
            {
                int sel = -1;
                if (cmbStatus.SelectedValue != null && int.TryParse(cmbStatus.SelectedValue.ToString(), out int id)) sel = id;
                profitPanel.Visibility = sel == 2 ? Visibility.Visible : Visibility.Collapsed;

                if (profitPanel.Visibility == Visibility.Visible && string.IsNullOrWhiteSpace(txtProfit.Text))
                {
                    if (decimal.TryParse(txtPrice.Text, out decimal pr) && pr > 0) txtProfit.Text = ((int)pr).ToString();
                }
            }
            catch { }
        }

        private bool HasUnsavedChanges()
        {
            if (_isNew) return true;

            try
            {
                bool hasChanges = txtTitle.Text != (_ad.ad_title ?? "") ||
                                  txtDescription.Text != (_ad.ad_description ?? "") ||
                                  dpDate.SelectedDate != _ad.ad_post_date ||
                                  (cmbCity.SelectedValue != null && (int)cmbCity.SelectedValue != _ad.city_id) ||
                                  (cmbCategory.SelectedValue != null && (int)cmbCategory.SelectedValue != _ad.category) ||
                                  (cmbType.SelectedValue != null && (int)cmbType.SelectedValue != _ad.ad_type_id) ||
                                  (cmbStatus.SelectedValue != null && (int)cmbStatus.SelectedValue != _ad.ad_status_id) ||
                                  (decimal.TryParse(txtPrice.Text, out decimal cur) && cur != _ad.price);

                bool imageChanged = _selectedImagePath != null || (_selectedImagePath == null && !string.IsNullOrEmpty(_currentImagePath));

                return hasChanges || imageChanged;
            }
            catch { return true; }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try { _context?.Dispose(); } catch { }
        }

        #endregion
    }
}
