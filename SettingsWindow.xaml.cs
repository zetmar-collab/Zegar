using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Automation;

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
    private string language = "pl";
    private string DefaultFont => T("Domyślna (HTC)");
    private string T(string text) => UiText.Get(text, language);
    private readonly Dictionary<TextBlock, string> labels = new();
    private readonly Dictionary<ContentControl, string> captions = new();
    private readonly Dictionary<DependencyObject, string> accessibleNames = new();
    private readonly string? recoveryNotice;
    public Func<WidgetSettings, bool, string?>? CommitSettings { get; set; }
    public SettingsWindow(WidgetSettings settings, WeatherService weather, string? recoveryNotice = null, bool? startupEnabled = null)
    {
        InitializeComponent(); this.settings = settings.Clone(); this.weather = weather; this.recoveryNotice = recoveryNotice;
        CaptureLabels(this);
        language = settings.Language;
        LanguageInput.SelectedIndex = language == "en" ? 1 : 0;
        ClockFormatInput.SelectedIndex = settings.Use24HourClock ? 0 : 1;
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
        TextOpacityInput.Value = settings.TextOpacity * 100;
        VersionLabel.Text = "Zegar HTC " + typeof(App).Assembly.GetName().Version!.ToString(3);
        IntervalInput.ItemsSource = new[] { 5, 10, 15, 30, 60, 120 }.Append(settings.RefreshMinutes).Distinct().Order().ToArray();
        IntervalInput.SelectedItem = settings.RefreshMinutes;
        StartupInput.IsChecked = startupEnabled ?? Autostart.Enabled;
        TopmostInput.IsChecked = settings.Topmost; LockInput.IsChecked = settings.Locked;
        ready = true;
        ApplyLanguage();
        Closed += (_, _) => { search?.Cancel(); };
    }
    private async void Search(object sender, RoutedEventArgs e)
    {
        string query = CityInput.Text.Trim();
        if (query.Length < 2) { SearchStatus.Text = T("Wpisz przynajmniej 2 znaki."); return; }
        search?.Cancel(); var request = new CancellationTokenSource(); search = request;
        SearchButton.IsEnabled = false; SaveButton.IsEnabled = false;
        SearchStatus.Text = T("Wyszukiwanie w Open-Meteo…");
        try
        {
            var results = await weather.SearchAsync(query, request.Token, language);
            if (request.IsCancellationRequested || CityInput.Text.Trim() != query) return;
            searchedText = query; Places.ItemsSource = results;
            if (results.Count > 0) Places.SelectedIndex = 0;
            SearchStatus.Text = T(results.Count == 0 ? "Brak wyników. Spróbuj innej nazwy." : "Wybierz właściwą miejscowość i zapisz ustawienia.");
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException)
        { SearchStatus.Text = T("Nie udało się wyszukać. Sprawdź połączenie i spróbuj ponownie."); }
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
        SearchStatus.Text = T("Kliknij Szukaj, aby wybrać miejscowość.");
    }
    private void PlaceSelected(object sender, SelectionChangedEventArgs e) => selected = Places.SelectedItem as Place;
    private void ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (ScaleLabel != null) ScaleLabel.Text = $"{e.NewValue:0}%"; }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (OpacityLabel != null) OpacityLabel.Text = $"{e.NewValue:0}% {T("widoczności")}"; if (ready) UpdateFontPreview(); }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (CityInput.Text.Trim() != settings.City && (selected == null || searchedText != CityInput.Text.Trim()))
        { SaveStatus.Text = T("Wyszukaj miejscowość i wybierz wynik z listy."); return; }
        if (selected != null)
        { settings.City = selected.Name; settings.Latitude = selected.Latitude; settings.Longitude = selected.Longitude; }
        settings.Scale = ScaleInput.Value / 100; settings.Opacity = OpacityInput.Value / 100;
        settings.TextOpacity = TextOpacityInput.Value / 100;
        settings.BackgroundColor = backgroundColor;
        settings.FontColor = fontColor;
        settings.FontFamily = FontInput.SelectedItem is string family && family != DefaultFont ? family : "";
        settings.Language = language;
        settings.Use24HourClock = ClockFormatInput.SelectedIndex == 0;
        settings.RefreshMinutes = (int)(IntervalInput.SelectedItem ?? 15);
        settings.Topmost = TopmostInput.IsChecked == true; settings.Locked = LockInput.IsChecked == true;
        if (CommitSettings == null) { SaveStatus.Text = T("Nie udało się zapisać ustawień"); return; }
        string? error = CommitSettings(settings.Clone(), StartupInput.IsChecked == true);
        if (error != null) { SaveStatus.Text = error; return; }
        Close();
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
        FontColorSwatch.ToolTip = string.IsNullOrEmpty(fontColor) ? T("Automatyczny") : fontColor;
        FontPreviewBackground.Background = Appearance.Background((Color)ColorConverter.ConvertFromString(backgroundColor));
        FontPreviewBackground.Background.Opacity = OpacityInput.Value >= 40 ? OpacityInput.Value / 100 : 1;
        FontPreview.Foreground = new SolidColorBrush(text);
        FontPreview.Opacity = TextOpacityInput.Value >= 40 ? TextOpacityInput.Value / 100 : 1;
        FontPreview.FontFamily = new FontFamily(FontInput.SelectedItem is string family && family != DefaultFont ? family : "Segoe UI");
        bool use24 = ClockFormatInput.SelectedIndex != 1;
        FontPreview.Text = ClockFormat.Time(new DateTime(2026, 10, 26, 15, 34, 0), use24) + " · " +
            (language == "en" ? "Monday" : "Poniedziałek") + " · 21°C";
        double contrast = Appearance.MinimumContrast((Color)ColorConverter.ConvertFromString(backgroundColor), text, FontPreview.Opacity);
        ContrastLabel.Text = contrast < 4.5 ? T("Niski kontrast tekstu — wybierz jaśniejszy lub ciemniejszy kolor.") : "";
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
    private void CaptureLabels(DependencyObject element)
    {
        if (element is TextBlock block && block.Name is not ("SearchStatus" or "FontPreview" or "ScaleLabel" or "OpacityLabel" or "TextOpacityLabel" or "SaveStatus" or "RecoveryLabel" or "VersionLabel" or "ContrastLabel") && !string.IsNullOrEmpty(block.Text))
            labels[block] = block.Text;
        if (element is ContentControl control && control.Content is string content) captions[control] = content;
        string name = AutomationProperties.GetName(element);
        if (!string.IsNullOrEmpty(name)) accessibleNames[element] = name;
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>()) CaptureLabels(child);
    }
    private void ApplyLanguage()
    {
        Title = T("Ustawienia zegara");
        foreach (var label in labels) label.Key.Text = T(label.Value);
        foreach (var caption in captions) caption.Key.Content = T(caption.Value);
        foreach (var name in accessibleNames) AutomationProperties.SetName(name.Key, T(name.Value));
        if (FontInput.ItemsSource is List<string> fonts)
        {
            bool wasDefault = FontInput.SelectedIndex == 0;
            var selectedFont = FontInput.SelectedItem;
            var updatedFonts = fonts.ToList(); updatedFonts[0] = DefaultFont;
            FontInput.ItemsSource = updatedFonts;
            FontInput.SelectedItem = wasDefault ? DefaultFont : selectedFont;
        }
        OpacityLabel.Text = $"{OpacityInput.Value:0}% {T("widoczności")}";
        SearchStatus.Text = T(Places.Items.Count > 0 ? "Wybierz właściwą miejscowość i zapisz ustawienia." : "Wpisz nazwę i wybierz miejscowość z listy.");
        SaveStatus.Text = "";
        RecoveryLabel.Text = recoveryNotice == null ? "" : T(recoveryNotice);
        TextOpacityLabel.Text = $"{TextOpacityInput.Value:0}%";
        UpdateFontPreview();
    }
    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready) return;
        language = LanguageInput.SelectedIndex == 1 ? "en" : "pl";
        ApplyLanguage();
    }
    private void ClockFormatChanged(object sender, SelectionChangedEventArgs e)
    { if (ready) UpdateFontPreview(); }
    private void TextOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (TextOpacityLabel != null) TextOpacityLabel.Text = $"{e.NewValue:0}%"; if (ready) UpdateFontPreview(); }
}
