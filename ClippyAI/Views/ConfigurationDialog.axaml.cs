using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using ClippyAI.Models;
using ClippyAI.ViewModels;
using System;
using System.Threading.Tasks;

namespace ClippyAI.Views;

public partial class ConfigurationDialog : Window
{
    public ConfigurationDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Turns a key press in a hotkey field into the canonical "Ctrl+Alt+C"
    /// form. Bare keys are rejected (a global hotkey without a modifier would
    /// swallow normal typing); Backspace/Delete clears the field (= disabled).
    /// </summary>
    private void HotkeyTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        e.Handled = true;

        switch (e.Key)
        {
            // Modifier-only presses: nothing to record yet.
            case Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin:
                return;

            case Key.Back or Key.Delete:
                box.Text = "";
                return;

            case Key.Escape:
                return; // leave the current text untouched
        }

        KeyModifiers mods = e.KeyModifiers;
        string? keyName = GlobalHotkey.NameFromAvaloniaKey(e.Key);

        if (keyName == null)
        {
            return;
        }

        bool ctrl = mods.HasFlag(KeyModifiers.Control);
        bool alt = mods.HasFlag(KeyModifiers.Alt);
        bool shift = mods.HasFlag(KeyModifiers.Shift);
        bool win = mods.HasFlag(KeyModifiers.Meta);

        if (!ctrl && !alt && !shift && !win)
        {
            return; // bare key - keep the previous value
        }

        box.Text = GlobalHotkey.Format(ctrl, alt, shift, win, keyName);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ConfigurationDialogViewModel viewModel)
        {
            // Subscribe to command execution to close dialog
            Button? saveButton = this.FindControl<Button>("SaveButton");
            Button? closeButton = this.FindControl<Button>("CloseButton");
            StackPanel? saveStatus = this.FindControl<StackPanel>("SaveStatusPanel");

            // Show the first task straight away. An empty detail pane reads as
            // a broken screen even though the list is full.
            if (viewModel.SelectedTaskConfiguration is null && viewModel.TaskConfigurations.Count > 0)
            {
                viewModel.SelectedTaskConfiguration = viewModel.TaskConfigurations[0];
            }

            // Override the commands to handle dialog closing
            saveButton?.Click += (s, args) =>
                {
                    if (!viewModel.Save())
                    {
                        // Invalid settings were rejected with an error message;
                        // do not refresh or re-register anything.
                        return;
                    }

                    // Refresh configurations in MainViewModel
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        if (desktop.MainWindow is MainWindow mainWindow)
                        {
                            if (mainWindow.DataContext is MainViewModel mainViewModel)
                            {
                                mainViewModel.RefreshConfigurations();
                            }

                            // Hotkey changes take effect immediately, no restart needed.
                            mainWindow.ApplyGlobalHotkeyConfiguration();
                        }
                    }

                    // Reinitialize services (SSH, Embeddings)
                    App.Current?.ReinitializeServices();

                    // The dialog stays open so the user can keep working, but the
                    // save has to be visible - otherwise it looks like nothing happened.
                    ShowSaveStatus(saveStatus);

                    // Close the dialog to prevent PlatformImpl null errors
                    //Close(true);
                };

            closeButton?.Click += (s, args) =>
            {
                Close(false);
            };
        }
    }

    /// <summary>
    /// Briefly confirms a successful save in the dialog footer.
    /// </summary>
    private async void ShowSaveStatus(StackPanel? saveStatus)
    {
        if (saveStatus is null)
        {
            return;
        }

        saveStatus.IsVisible = true;

        await Task.Delay(2500);

        saveStatus.IsVisible = false;
    }
}