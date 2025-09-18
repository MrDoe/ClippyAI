using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClippyAI.ViewModels;

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
            var cancelButton = this.FindControl<Button>("CancelButton");
            
            // Override the commands to handle dialog closing
            if (saveButton != null)
            {
                saveButton.Click += (s, args) =>
                {
                    viewModel.Save();
                    Close(true);
                };
            }
            
            if (cancelButton != null)
            {
                cancelButton.Click += (s, args) =>
                {
                    Close(false);
                };
            }
        }
    }
}