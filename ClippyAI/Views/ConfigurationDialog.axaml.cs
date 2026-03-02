using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ClippyAI.ViewModels;
using System;

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

                    // Close the dialog to prevent PlatformImpl null errors
                    //Close(true);
                };

            closeButton?.Click += (s, args) =>
            {
                Close(false);
            };
        }
    }
}