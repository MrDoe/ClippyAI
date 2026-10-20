using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
                    viewModel.Save();

                    // Refresh configurations in MainViewModel
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        if (desktop.MainWindow is MainWindow mainWindow)
                        {
                            if (mainWindow.DataContext is MainViewModel mainViewModel)
                            {
                                mainViewModel.RefreshConfigurations();
                            }
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