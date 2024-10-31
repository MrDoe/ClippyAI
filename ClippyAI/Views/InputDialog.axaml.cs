using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
namespace ClippyAI.Views;

public partial class InputDialog : Window
{
    private bool _isInputRequired = false;
    Func<string, bool>? _beforeClosing;
    public bool TextBoxVisible
    {
        get => txtBox.IsVisible;
        set => txtBox.IsVisible = value;
    }

    public InputDialog()
    {
        InitializeComponent();
        Opened += OnOpened;
        TextBoxVisible = true;
    }
    
    public static async Task<string?> Prompt(Window parentWindow, string title, string caption,
                                             bool isRequired = true, string initialValue = "",
                                             Func<string, bool>? beforeClosing = null)
    {
        var window = new InputDialog
        {
            _isInputRequired = isRequired,
            Title = title
        };
        window.lbl.Content = caption;
        window.txtBox.Text = initialValue;
        window._beforeClosing = beforeClosing;
        var result = await window.ShowDialog<string?>(parentWindow);
        return result;
    }

    public static async Task<string> Confirm(Window parentWindow, string title, string caption)
    {
        var window = new InputDialog
        {
            Title = title
        };
        window.lbl.Content = caption;
        window.TextBoxVisible = false;
        window.Height = 120;

        var result = await window.ShowDialog<string>(parentWindow);
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
            txtBox.Watermark = "Error: Please provide a value!";
            return;
        }

        if (_beforeClosing != null && !_beforeClosing(txtBox.Text ?? ""))
        {
            return;
        }
        if(TextBoxVisible == false)
            Close("OK");
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
        if(TextBoxVisible == false)
            Close("Cancel");
        else
            Close(null);
    }
}