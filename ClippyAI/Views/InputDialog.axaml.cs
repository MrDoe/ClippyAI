using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
namespace ClippyAI.Views;

public partial class InputDialog : Window
{
    private bool _isInputRequired = false;
    private Func<string, bool>? _beforeClosing;
    private Window? parentWindow;
    public bool TextBoxVisible
    {
        get => txtBox.IsVisible;
        set => txtBox.IsVisible = value;
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
                    mainWindow.Position.X - 488,
                    mainWindow.Position.Y + 20);
            }
        }

        Opened += OnOpened;
        TextBoxVisible = true;
    }

    public static async Task<string?> Prompt(Window parentWindow, string title, string caption, string subtext = "",
                                             bool isRequired = true, string initialValue = "",
                                             Func<string, bool>? beforeClosing = null)
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
        inputDialog.lbl.Content = caption;
        inputDialog.txtBox.Text = initialValue;
        inputDialog.lblSubText.Content = subtext;
        inputDialog._beforeClosing = beforeClosing;
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
        if (_isInputRequired && string.IsNullOrWhiteSpace(txtBox.Text))
        {
            return;
        }

        if (_beforeClosing != null && !_beforeClosing(txtBox.Text ?? ""))
        {
            return;
        }
        if (TextBoxVisible == false)
            Close(ClippyAI.Resources.Resources.Yes);
        else
            Close(txtBox.Text);
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