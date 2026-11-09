using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ClippyAI.ViewModels;
using System;

namespace ClippyAI.Views;

public partial class ManualContextDialog : Window
{
    public ManualContextDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is ManualDialogViewModel vm)
        {
            Button? ok = this.FindControl<Button>("OkButton");
            ok?.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(vm.AppName))
                {
                    vm.ErrorMessage = "Please enter the application name (or pick a profile).";
                    return;
                }

                vm.ErrorMessage = "";
                vm.PersistProfileIfRequested();
                Close(true);
            };
        }
    }
}
