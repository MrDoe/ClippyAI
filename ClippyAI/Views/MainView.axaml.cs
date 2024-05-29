using Avalonia.Controls;
namespace ClippyAI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und stimme dankend zu.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und lehne höflich ab.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und bitte um mehr Informationen.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und bitte um eine Terminabsprache.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und sage, dass du dich um das Problem kümmerst.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und sage, dass du das Problem nicht lösen kannst.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und bitte um eine Rückmeldung.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und bitte um eine Bestätigung.");
        cboTask.Items.Add("Schreibe eine Antwort auf die E-Mail und bitte um eine erneute Terminvereinbarung.");
        cboTask.Items.Add("Erkläre möglichst genau, was das ist.");
        cboTask.Items.Add("Erkläre, wo hier der Fehler liegt.");
        cboTask.Items.Add("Erkläre, wie man das verbessern kann.");
        cboTask.SelectedIndex = 0;
    }
}