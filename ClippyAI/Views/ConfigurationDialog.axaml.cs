using System;
using Avalonia.Controls;
using ClippyAI.ViewModels;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;

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
            var saveButton = this.FindControl<Button>("SaveButton");
            var closeButton = this.FindControl<Button>("CloseButton");
            
            // Override the commands to handle dialog closing
            if (saveButton != null)
            {
                saveButton.Click += (s, args) =>
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
                    Close(true);
                };
            }
            
            if (closeButton != null)
            {
                closeButton.Click += (s, args) =>
                {
                    Close(false);
                };
            }
        }
    }
}