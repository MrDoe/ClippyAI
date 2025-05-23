using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
namespace ClippyAI.Views;

public partial class InputDialog : Window
{
    private bool _isInputRequired = false;
    private Window? parentWindow;
    public bool TextBoxVisible
    {
        get => txtBox.IsVisible;
        set => txtBox.IsVisible = value;
    }
    public bool ComboBoxVisible
    {
        get => cboInput!.IsVisible;
        set => cboInput!.IsVisible = value;
    }

    public InputDialog()
    {
        InitializeComponent();

        // position the window above the MainWindow
        if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                Position = new PixelPoint(
                    mainWindow.Position.X + 488,
                    mainWindow.Position.Y + 500);
            }
        }

        Opened += OnOpened;
        TextBoxVisible = true;
    }

    /// <summary>
    /// Prompt the user for input
    /// </summary>
    /// <param name="parentWindow">The parent window </param>
    /// <param name="title">The title of the dialog</param>
    /// <param name="caption">The caption of the input control</param>
    /// <param name="subtext"></param>
    /// <param name="isRequired"></param>
    /// <param name="initialValue"></param>
    /// <param name="comboBoxItems"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static async Task<string?> Prompt(Window parentWindow, string title, string caption, string subtext = "",
                                             bool isRequired = true, string initialValue = "",
                                             ObservableCollection<string>? comboBoxItems = null)
    {
        if (parentWindow == null)
        {
            throw new ArgumentNullException(nameof(parentWindow));
        }
        var inputDialog = new InputDialog()
        {
            parentWindow = parentWindow,
            _isInputRequired = isRequired,
            Title = title
        };

        if (comboBoxItems != null)
        {
            // Hide the text box and show the combo box
            inputDialog.txtBox.IsVisible = false;
            inputDialog.cboInput!.IsVisible = true;
            inputDialog.cboInput!.ItemsSource = comboBoxItems;
            inputDialog.cboInput.SelectedValue = initialValue;
        }
        else
            inputDialog.txtBox.Text = initialValue;

        inputDialog.lbl.Content = caption;
        inputDialog.lblSubText.Content = subtext;
        var result = await inputDialog.ShowDialog<string?>(parentWindow);
        return result;
    }

    public static async Task<string> Confirm(Window parentWindow, string title, string caption)
    {
        var inputDialog = new InputDialog()
        {
            parentWindow = parentWindow,
            _isInputRequired = false,
            Title = title
        };
        inputDialog.lbl.Content = caption;
        inputDialog.TextBoxVisible = false;
        inputDialog.Height = 120;
        inputDialog.lblSubText.Content = "";
        inputDialog.btnOK.Content = ClippyAI.Resources.Resources.Yes;
        inputDialog.btnCancel.Content = ClippyAI.Resources.Resources.No;

        var result = await inputDialog.ShowDialog<string>(parentWindow);
        return result;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        txtBox.Focus();
    }

    private void ButtonOK_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? value;

        if (TextBoxVisible)
        {
            value = string.IsNullOrEmpty(txtBox.Text) ? null : txtBox.Text;
        }
        else if (ComboBoxVisible)
        {
            value = cboInput.SelectedValue!.ToString();
            value = string.IsNullOrEmpty(value) ? null : value;
        }
        else
            value = ClippyAI.Resources.Resources.Yes;

        if (_isInputRequired && string.IsNullOrEmpty(value))
        {
            return;
        }
        else
            Close(value);
    }

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ButtonOK_Click(sender, e);
        }
    }

    private void ButtonCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TextBoxVisible == false)
            Close(ClippyAI.Resources.Resources.No);
        else
            Close(null);
    }
}