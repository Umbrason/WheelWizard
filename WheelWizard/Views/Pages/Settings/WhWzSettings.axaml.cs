using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HarfBuzzSharp;
using WheelWizard.Helpers;
using WheelWizard.Models.Settings;
using WheelWizard.Resources.Languages;
using WheelWizard.Services;
using WheelWizard.Services.Settings;
using WheelWizard.Shared.MessageTranslations;
using WheelWizard.Views.Popups.Generic;
using Button = WheelWizard.Views.Components.Button;

namespace WheelWizard.Views.Pages.Settings;

public partial class WhWzSettings : UserControl
{
    private readonly bool _pageLoaded;
    private bool _editingScale;

    public WhWzSettings()
    {
        InitializeComponent();
        AutoFillPaths();
        TogglePathSettings(false);
        LoadSettings();
        _pageLoaded = true;

        MKGameFieldLabel.TipText = WheelWizard.Resources.Languages.Settings.HelperText_EndWithX + "Path can end with: .wbfs/.iso/.rvz";
        WhWzLanguageDropdown.SelectionChanged += WhWzLanguageDropdown_OnSelectionChanged;
    }

    private void LoadSettings()
    {
        // -----------------
        // Wheel Wizard Language Dropdown
        // -----------------
        WhWzLanguageDropdown.Items.Clear(); // Clear existing items
        foreach (var lang in SettingValues.WhWzLanguages.Values)
        {
            WhWzLanguageDropdown.Items.Add(lang());
        }

        var currentWhWzLanguage = (string)SettingsManager.WW_LANGUAGE.Get();
        var whWzLanguageDisplayName = SettingValues.WhWzLanguages[currentWhWzLanguage];
        WhWzLanguageDropdown.SelectedItem = whWzLanguageDisplayName();

        TranslationsPercentageText.Text = Humanizer.ReplaceDynamic(
            Phrases.Text_LanguageTranslatedBy,
            WheelWizard.Resources.Languages.Settings.Value_Language_zTranslators
        );
        TranslationsPercentageText.IsVisible = WheelWizard.Resources.Languages.Settings.Value_Language_zTranslators != "-";

        // -----------------
        // Window Scale settings
        // -----------------
        // IMPORTANT: Make sure that the number and percentage is always the last word in the string,
        // If you don't want this, you should change the code below that parses the string back to an actual value

        foreach (var scale in SettingValues.WindowScales)
        {
            WindowScaleDropdown.Items.Add(ScaleToString(scale));
        }

        var selectedItemText = ScaleToString((double)SettingsManager.WINDOW_SCALE.Get());
        if (!WindowScaleDropdown.Items.Contains(selectedItemText))
            WindowScaleDropdown.Items.Add(selectedItemText);
        WindowScaleDropdown.SelectedItem = selectedItemText;

        EnableAnimations.IsChecked = (bool)SettingsManager.ENABLE_ANIMATIONS.Get();
    }

    private static string ScaleToString(double scale)
    {
        var percentageString = (int)Math.Round(scale * 100) + "%";
        if (SettingValues.WindowScales.Contains(scale))
            return percentageString;

        return Common.State_Custom + ": " + percentageString;
    }

    private void AutoFillPaths()
    {
        if (DolphinExeInput.Text != "")
            return;

        var folderPath = PathManager.TryFindUserFolderPath();
        if (!string.IsNullOrEmpty(folderPath))
            DolphinUserPathInput.Text = folderPath;
    }

    private void AssignWrappedDolphinExeInput(string inputText)
    {
        DolphinExeInput.Text = EnvHelper.SingleQuotePath(inputText);
    }

    private async void DolphinExeBrowse_OnClick(object sender, RoutedEventArgs e)
    {
        var executableFileType = new FilePickerFileType("Executable files")
        {
            Patterns = Environment.OSVersion.Platform switch
            {
                PlatformID.Win32NT => new[] { "*.exe" },
                PlatformID.Unix => new[] { "*", "*.sh" },
                PlatformID.MacOSX => new[] { "*", "*.app" },
                _ => new[] { "*" }, // Fallback
            },
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (IsFlatpakDolphinInstalled() && DolphinExeInput.Text == "")
            {
                DolphinExeInput.Text = "flatpak run org.DolphinEmu.dolphin-emu";
                return;
            }

            if (!EnvHelper.IsFlatpakSandboxed() && !IsFlatpakDolphinInstalled())
            {
                var wantsAutomaticInstall = await new YesNoWindow()
                    .SetMainText(Phrases.Question_DolphinFlatpack_Title)
                    .SetExtraText(Phrases.Question_DolphinFlatpack_Extra)
                    .SetButtonText(Common.Action_Install, Common.Action_DoManually)
                    .AwaitAnswer();
                if (wantsAutomaticInstall)
                {
                    var progressWindow = new ProgressWindow()
                        .SetGoal(Phrases.Progress_InstallingDolphin)
                        .SetExtraText(Phrases.Progress_ThisMayTakeAWhile);
                    TogglePathSettings(true);
                    progressWindow.Show();
                    var progress = new Progress<int>(progressWindow.UpdateProgress);
                    var success = await LinuxDolphinInstaller.InstallFlatpakDolphin(progress);
                    progressWindow.Close();
                    if (!success)
                    {
                        await MessageTranslationHelper.AwaitMessageAsync(MessageTranslation.Error_FailedInstallDolphin);
                        return;
                    }

                    DolphinExeInput.Text = "flatpak run org.DolphinEmu.dolphin-emu";
                    return;
                }
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var dolphinAppPath = PathManager.TryToFindApplicationPath();
            if (!string.IsNullOrEmpty(dolphinAppPath))
            {
                var result = await new YesNoWindow()
                    .SetMainText(Phrases.Question_DolphinFound_Title)
                    .SetExtraText($"{Phrases.Question_DolphinFound_Extra}\n{dolphinAppPath}")
                    .AwaitAnswer();

                if (result)
                {
                    AssignWrappedDolphinExeInput(dolphinAppPath);
                    return;
                }
            }
            else
            {
                await MessageTranslationHelper.AwaitMessageAsync(MessageTranslation.Warning_DolphinNotFound);
            }

            // Fallback to manual selection
            var folders = await FilePickerHelper.SelectFolderAsync("Select Dolphin.app");
            if (folders.Count >= 1)
            {
                var executablePath = Path.Combine(folders[0].Path.LocalPath, "Contents", "MacOS", "Dolphin");
                AssignWrappedDolphinExeInput(executablePath);
            }

            return; // do not do normal selection for MacOS
        }

        var filePath = await FilePickerHelper.OpenSingleFileAsync("Select Dolphin Emulator", [executableFileType]);
        if (!string.IsNullOrEmpty(filePath))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, the file path is directly used as the executable, not in some command
                DolphinExeInput.Text = filePath;
            }
            else
            {
                AssignWrappedDolphinExeInput(filePath);
            }
        }
    }

    private bool IsFlatpakDolphinInstalled()
    {
        return LinuxDolphinInstaller.IsDolphinInstalledInFlatpak();
    }

    private async void GameLocationBrowse_OnClick(object sender, RoutedEventArgs e)
    {
        var fileType = new FilePickerFileType("Game files") { Patterns = ["*.iso", "*.wbfs", "*.rvz"] };

        var filePath = await FilePickerHelper.OpenSingleFileAsync("Select Mario Kart Wii Game File", [fileType]);
        if (!string.IsNullOrEmpty(filePath))
        {
            MarioKartInput.Text = filePath;
        }
    }

    private async void DolphinUserPathBrowse_OnClick(object sender, RoutedEventArgs e)
    {
        // Attempt to find Dolphin's default path if no valid folder is set
        var folderPath = PathManager.TryFindUserFolderPath();
        if (!string.IsNullOrEmpty(folderPath))
        {
            // Ask the user if they want to use the automatically found folder
            var result = await new YesNoWindow()
                .SetMainText(Phrases.Question_DolphinFound_Title)
                .SetExtraText($"{Phrases.Question_DolphinFound_Extra}\n{folderPath}")
                .AwaitAnswer();

            if (result)
            {
                DolphinUserPathInput.Text = folderPath;
                return;
            }
        }
        else
        {
            await MessageTranslationHelper.AwaitMessageAsync(MessageTranslation.Warning_DolphinNotFound);
        }

        var currentFolder = (string)SettingsManager.USER_FOLDER_PATH.Get();
        var topLevel = TopLevel.GetTopLevel(this);
        // If a current folder exists and is valid, suggest it as the starting location
        if (!string.IsNullOrEmpty(currentFolder) && Directory.Exists(currentFolder))
        {
            var folder = await topLevel!.StorageProvider.TryGetFolderFromPathAsync(currentFolder);
            var folders = await FilePickerHelper.SelectFolderAsync("Select Dolphin User Path", folder);

            if (folders.Count >= 1)
                DolphinUserPathInput.Text = folders[0].Path.LocalPath;
            return;
        }
        else
        {
            // Let the user manually select a folder
            var manualFolders = await FilePickerHelper.SelectFolderAsync("Select Dolphin User Path");

            if (manualFolders.Count >= 1)
                DolphinUserPathInput.Text = manualFolders[0].Path.LocalPath;
        }
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var oldPath1 = (string)SettingsManager.DOLPHIN_LOCATION.Get();
        var oldPath2 = (string)SettingsManager.GAME_LOCATION.Get();
        var oldPath3 = (string)SettingsManager.USER_FOLDER_PATH.Get();

        var path1 = SettingsManager.DOLPHIN_LOCATION.Set(DolphinExeInput.Text);
        var path2 = SettingsManager.GAME_LOCATION.Set(MarioKartInput.Text);
        var path3 = SettingsManager.USER_FOLDER_PATH.Set(DolphinUserPathInput.Text.TrimEnd(Path.DirectorySeparatorChar));
        // These 3 lines is only saving the settings
        TogglePathSettings(false);
        if (!(SettingsHelper.PathsSetupCorrectly() && path1 && path2 && path3))
            await MessageTranslationHelper.AwaitMessageAsync(MessageTranslation.Warning_InvalidPathSettings);
        else
        {
            await MessageTranslationHelper.AwaitMessageAsync(MessageTranslation.Success_PathSettingsSaved);

            // This is not really the best approach, but it works for now
            if (oldPath1 + oldPath2 + oldPath3 != DolphinExeInput.Text + MarioKartInput.Text + DolphinUserPathInput.Text)
                DolphinSettingManager.Instance.ReloadSettings();
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => TogglePathSettings(false);

    private void EditButton_OnClick(object sender, RoutedEventArgs e) => TogglePathSettings(true);

    private void WhWzFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(PathManager.WheelWizardAppdataPath))
            Directory.CreateDirectory(PathManager.WheelWizardAppdataPath);

        FilePickerHelper.OpenFolderInFileManager(PathManager.WheelWizardAppdataPath);
    }

    private void GameFileFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(PathManager.RiivolutionWhWzFolderPath))
            return; //Button should be disabled
        FilePickerHelper.OpenFolderInFileManager(PathManager.RiivolutionWhWzFolderPath);
    }

    private void TogglePathSettings(bool enable)
    {
        if (enable)
        {
            LocationBorder.BorderBrush = new SolidColorBrush(ViewUtils.Colors.Neutral900);
        }
        else if (!SettingsHelper.PathsSetupCorrectly())
        {
            LocationBorder.BorderBrush = new SolidColorBrush(ViewUtils.Colors.Warning400);
            LocationEditButton.Variant = Button.ButtonsVariantType.Warning;
            LocationWarningIcon.IsVisible = true;
            LocationBorderBlur.Background = new SolidColorBrush(ViewUtils.Colors.Warning600);
        }
        else
        {
            LocationBorder.BorderBrush = new SolidColorBrush(ViewUtils.Colors.Primary400);
            LocationEditButton.Variant = Button.ButtonsVariantType.Primary;
            LocationWarningIcon.IsVisible = false;
            LocationBorderBlur.Background = new SolidColorBrush(ViewUtils.Colors.Primary600);
        }

        LocationBorderBlur.IsVisible = !enable;
        LocationInputFields.IsEnabled = enable;
        LocationEditButton.IsVisible = !enable;
        LocationSaveButton.IsVisible = enable;
        LocationCancelButton.IsVisible = enable;

        DolphinExeInput.Text = PathManager.DolphinFilePath;
        MarioKartInput.Text = PathManager.GameFilePath;
        DolphinUserPathInput.Text = PathManager.UserFolderPath;
        OpenGameFolderButton.IsEnabled = Directory.Exists(PathManager.RiivolutionWhWzFolderPath);
    }

    private async void WindowScaleDropdown_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_pageLoaded || _editingScale)
            return;

        _editingScale = true;
        var selectedScale = WindowScaleDropdown.SelectedItem?.ToString() ?? "1";
        var scale = double.Parse(selectedScale.Split(" ").Last().Replace("%", "")) / 100;

        SettingsManager.WINDOW_SCALE.Set(scale);
        var seconds = 10;

        string ExtraScaleText() =>
            Humanizer.ReplaceDynamic(Phrases.Question_ApplyScale_Extra, Humanizer.HumanizeSeconds(seconds))
            ?? $"This will apply the new scale in {Humanizer.HumanizeSeconds(seconds)} seconds. You can cancel this by clicking Revert.";

        var yesNoWindow = new YesNoWindow()
            .SetButtonText(Common.Action_Apply, Common.Action_Revert)
            .SetMainText(Phrases.Question_ApplyScale_Title)
            .SetExtraText(ExtraScaleText());
        // we want to now set up a timer every second to update the text, and at the last second close the window
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };

        timer.Tick += (_, args) =>
        {
            seconds--;
            yesNoWindow.SetExtraText(ExtraScaleText());
            if (seconds != 0)
                return;
            yesNoWindow.Close();
            timer.Stop();
        };
        timer.Start();

        var yesNoAnswer = await yesNoWindow.AwaitAnswer();
        if (yesNoAnswer)
            SettingsManager.SAVED_WINDOW_SCALE.Set(SettingsManager.WINDOW_SCALE.Get());
        else
        {
            SettingsManager.WINDOW_SCALE.Set(SettingsManager.SAVED_WINDOW_SCALE.Get());
            WindowScaleDropdown.SelectedItem = ScaleToString((double)SettingsManager.WINDOW_SCALE.Get());
        }

        _editingScale = false;
    }

    private async void WhWzLanguageDropdown_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (WhWzLanguageDropdown.SelectedItem == null)
            return;

        var selectedLanguage = WhWzLanguageDropdown.SelectedItem.ToString();
        var key = SettingValues.WhWzLanguages.FirstOrDefault(x => x.Value() == selectedLanguage).Key;

        var currentLanguage = (string)SettingsManager.WW_LANGUAGE.Get();
        if (key == null || key == currentLanguage)
            return;

        // TODO: translate this popup, but support multiple languages. So it should display both NL and FR when you try to switch from NL to FR
        var yesNoWindow = await new YesNoWindow()
            .SetMainText("Do you want to apply the new language settings?")
            .SetExtraText("This will close the current window and open a new one with the new language settings.")
            .SetButtonText(Common.Action_Apply, Common.Action_Cancel)
            .AwaitAnswer();

        if (!yesNoWindow)
        {
            var currentWhWzLanguage = (string)SettingsManager.WW_LANGUAGE.Get();
            var whWzLanguageDisplayName = SettingValues.WhWzLanguages[currentWhWzLanguage];
            WhWzLanguageDropdown.SelectedItem = whWzLanguageDisplayName;
            return; // We only want to change the setting if we really apply this change
        }

        SettingsManager.WW_LANGUAGE.Set(key);
        ViewUtils.RefreshWindow();
    }

    private void EnableAnimations_OnClick(object sender, RoutedEventArgs e) =>
        SettingsManager.ENABLE_ANIMATIONS.Set(EnableAnimations.IsChecked == true);
}
