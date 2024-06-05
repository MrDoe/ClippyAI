using Avalonia.Controls;
namespace ClippyAI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        // Finden Sie die ComboBox in Ihrem Avalonia XAML
        var comboBox = this.FindControl<ComboBox>("cboTask");

        // Fügen Sie einen Event-Handler für das SelectionChanged Event hinzu
        if (comboBox != null)
            comboBox.SelectionChanged += OnComboBoxSelectionChanged;
    }

    // Diese Funktion wird aufgerufen, wenn der Benutzer einen Wert in der ComboBox auswählt
    private void OnComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // setze txtCustomTask auf Enabled, wenn der Benutzer "Custom Task" auswählt 
        var comboBox = (ComboBox)sender!;
        var selectedItem = (string)comboBox.SelectedItem!;
        var txtCustomTask = this.FindControl<TextBox>("txtCustomTask");
        if (txtCustomTask != null)
        {
            if (selectedItem == ClippyAI.Resources.Resources.Task_15)
            {
                txtCustomTask.IsEnabled = true;
            }
            else
            {
                txtCustomTask.IsEnabled = false;
            }
        }
    }
}