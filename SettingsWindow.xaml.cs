using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Zegar;

public partial class SettingsWindow : Window
{
    private readonly WidgetSettings settings;
    private readonly WeatherService weather;
    private CancellationTokenSource? search;
    private Place? selected;
    private string searchedText = "";
    private bool ready;
    private string backgroundColor = "#171E2B";
    private string fontColor = "";
    private const string DefaultFont = "Domyślna (HTC)";
    public event EventHandler? SettingsApplied;
    public SettingsWindow(WidgetSettings settings, WeatherService weather)
    {
        InitializeComponent(); this.settings = settings; this.weather = weather;
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);
        CityInput.Text = settings.City;
        backgroundColor = settings.BackgroundColor;
        fontColor = settings.FontColor;
        var availableFonts = Fonts.SystemFontFamilies.Select(font => font.Source).Distinct().OrderBy(name => name).ToList();
        if (!string.IsNullOrEmpty(settings.FontFamily) && !availableFonts.Contains(settings.FontFamily)) availableFonts.Insert(0, settings.FontFamily);
        availableFonts.Insert(0, DefaultFont);
        FontInput.ItemsSource = availableFonts;
        FontInput.SelectedItem = string.IsNullOrEmpty(settings.FontFamily) ? DefaultFont : settings.FontFamily;
        UpdateColorSwatch();
        ScaleInput.Value = settings.Scale * 100; OpacityInput.Value = settings.Opacity * 100;
        IntervalInput.ItemsSource = new[] { 5, 10, 15, 30, 60, 120 }.Append(settings.RefreshMinutes).Distinct().Order().ToArray();
        IntervalInput.SelectedItem = settings.RefreshMinutes;
        StartupInput.IsChecked = Autostart.Enabled;
        TopmostInput.IsChecked = settings.Topmost; LockInput.IsChecked = settings.Locked;
        ready = true;
        Closed += (_, _) => { search?.Cancel(); };
    }
    private async void Search(object sender, RoutedEventArgs e)
    {
        string query = CityInput.Text.Trim();
        if (query.Length < 2) { SearchStatus.Text = "Wpisz przynajmniej 2 znaki."; return; }
        search?.Cancel(); var request = new CancellationTokenSource(); search = request;
        SearchButton.IsEnabled = false; SaveButton.IsEnabled = false;
        SearchStatus.Text = "Wyszukiwanie w Open-Meteo…";
        try
        {
            var results = await weather.SearchAsync(query, request.Token);
            if (request.IsCancellationRequested || CityInput.Text.Trim() != query) return;
            searchedText = query; Places.ItemsSource = results;
            if (results.Count > 0) Places.SelectedIndex = 0;
            SearchStatus.Text = results.Count == 0 ? "Brak wyników. Spróbuj innej nazwy." : "Wybierz właściwą miejscowość i zapisz ustawienia.";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException)
        { SearchStatus.Text = "Nie udało się wyszukać. Sprawdź połączenie i spróbuj ponownie."; }
        finally
        {
            if (ReferenceEquals(search, request)) { search = null; SearchButton.IsEnabled = true; SaveButton.IsEnabled = true; }
            request.Dispose();
        }
    }
    private void CityKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { Search(sender, e); e.Handled = true; } }
    private void CityChanged(object sender, TextChangedEventArgs e)
    {
        if (!ready) return;
        selected = null; Places.ItemsSource = null; search?.Cancel();
        SearchStatus.Text = "Kliknij Szukaj, aby wybrać miejscowość.";
    }
    private void PlaceSelected(object sender, SelectionChangedEventArgs e) => selected = Places.SelectedItem as Place;
    private void ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (ScaleLabel != null) ScaleLabel.Text = $"{e.NewValue:0}%"; }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (OpacityLabel != null) OpacityLabel.Text = $"{e.NewValue:0}% widoczności"; }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (CityInput.Text.Trim() != settings.City && (selected == null || searchedText != CityInput.Text.Trim()))
        { SaveStatus.Text = "Wyszukaj miejscowość i wybierz wynik z listy."; return; }
        try { Autostart.Set(StartupInput.IsChecked == true); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        { SaveStatus.Text = "Nie udało się zmienić autostartu: " + ex.Message; return; }
        if (selected != null)
        { settings.City = selected.Name; settings.Latitude = selected.Latitude; settings.Longitude = selected.Longitude; }
        settings.Scale = ScaleInput.Value / 100; settings.Opacity = OpacityInput.Value / 100;
        settings.BackgroundColor = backgroundColor;
        settings.FontColor = fontColor;
        settings.FontFamily = FontInput.SelectedItem is string family && family != DefaultFont ? family : "";
        settings.RefreshMinutes = (int)(IntervalInput.SelectedItem ?? 15);
        settings.Topmost = TopmostInput.IsChecked == true; settings.Locked = LockInput.IsChecked == true;
        SettingsApplied?.Invoke(this, EventArgs.Empty); Close();
    }
    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private void UpdateColorSwatch()
    {
        ColorSwatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor));
        ColorSwatch.ToolTip = backgroundColor;
        UpdateFontPreview();
    }
    private void ChooseColor(object sender, RoutedEventArgs e)
    {
        var color = PickColor(backgroundColor);
        if (color == null) return;
        backgroundColor = color;
        UpdateColorSwatch();
    }
    private string? PickColor(string initial)
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.ColorTranslator.FromHtml(initial)
        };
        var owner = new System.Windows.Forms.NativeWindow();
        owner.AssignHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        try
        {
            if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK) return null;
            return $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
        finally { owner.ReleaseHandle(); }
    }
    private void ResetColor(object sender, RoutedEventArgs e)
    { backgroundColor = "#171E2B"; UpdateColorSwatch(); }
    private void FontChanged(object sender, SelectionChangedEventArgs e) => UpdateFontPreview();
    private void UpdateFontPreview()
    {
        if (FontPreview == null) return;
        var text = Appearance.TextColor(backgroundColor, fontColor);
        FontColorSwatch.Background = new SolidColorBrush(text);
        FontColorSwatch.ToolTip = string.IsNullOrEmpty(fontColor) ? "Automatyczny" : fontColor;
        FontPreviewBackground.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor));
        FontPreview.Foreground = new SolidColorBrush(text);
        FontPreview.FontFamily = new FontFamily(FontInput.SelectedItem is string family && family != DefaultFont ? family : "Segoe UI");
    }
    private void ChooseFontColor(object sender, RoutedEventArgs e)
    {
        var initial = Appearance.TextColor(backgroundColor, fontColor);
        var color = PickColor($"#{initial.R:X2}{initial.G:X2}{initial.B:X2}");
        if (color == null) return;
        fontColor = color;
        UpdateFontPreview();
    }
    private void ResetFontColor(object sender, RoutedEventArgs e)
    { fontColor = ""; UpdateFontPreview(); }
}
