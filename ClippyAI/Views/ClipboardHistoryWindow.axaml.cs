using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ClippyAI.Models;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace ClippyAI.Views;

public partial class ClipboardHistoryWindow : Window
{
    private MainViewModel? Vm => DataContext as MainViewModel;

    public ClipboardHistoryWindow()
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

        ListBox list = this.FindControl<ListBox>("HistoryList")!;
        StackPanel emptyState = this.FindControl<StackPanel>("EmptyStatePanel")!;

        UpdateEmptyState();
        if (Vm != null)
        {
            Vm.HistoryItems.CollectionChanged += (_, _) => UpdateEmptyState();
        }

        void UpdateEmptyState()
        {
            emptyState.IsVisible = Vm == null || Vm.HistoryItems.Count == 0;
        }

        this.FindControl<Button>("RestoreButton")!.Click += async (_, _) =>
        {
            if (Vm != null)
            {
                await Vm.RestoreHistoryItemCommand.ExecuteAsync(null);
            }
        };

        this.FindControl<Button>("ExecuteButton")!.Click += async (_, _) =>
        {
            if (Vm != null)
            {
                await Vm.ExecuteHistoryItemCommand.ExecuteAsync(null);
            }
        };

        this.FindControl<Button>("DeleteButton")!.Click += (_, _) =>
        {
            // Delete every multi-selected entry (the single-selection command
            // covers the focused item; the service call is trivially looped).
            if (Vm == null)
            {
                return;
            }

            foreach (ClipboardHistoryItem item in list.SelectedItems!.OfType<ClipboardHistoryItem>().ToList())
            {
                Services.ClipboardHistoryService.Delete(item.Id);
            }

            Vm.LoadHistory();
        };

        this.FindControl<Button>("ClearButton")!.Click += (_, _) => Vm?.ClearHistoryCommand.Execute(null);

        this.FindControl<Button>("ManualButton")!.Click += async (_, _) =>
        {
            if (Vm == null)
            {
                return;
            }

            var selected = list.SelectedItems!.OfType<ClipboardHistoryItem>().ToList();
            if (selected.Count == 0 && Vm.SelectedHistoryItem != null)
            {
                selected.Add(Vm.SelectedHistoryItem);
            }

            await Vm.GenerateManualAsync(selected);
        };

        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();

        list.DoubleTapped += async (_, _) =>
        {
            if (Vm != null)
            {
                await Vm.RestoreHistoryItemCommand.ExecuteAsync(null);
            }
        };

        list.KeyDown += async (_, args) =>
        {
            if (args.Key == Key.Delete)
            {
                if (Vm == null)
                {
                    return;
                }

                foreach (ClipboardHistoryItem item in list.SelectedItems!.OfType<ClipboardHistoryItem>().ToList())
                {
                    Services.ClipboardHistoryService.Delete(item.Id);
                }

                Vm.LoadHistory();
                args.Handled = true;
            }
            else if (args.Key == Key.Enter && Vm != null)
            {
                await Vm.RestoreHistoryItemCommand.ExecuteAsync(null);
                args.Handled = true;
            }
        };
    }
}
