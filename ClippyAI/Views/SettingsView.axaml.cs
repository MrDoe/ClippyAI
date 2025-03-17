using Avalonia.Controls;
using System.Configuration;

namespace ClippyAI.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }
    }

    public class SettingsViewModel
    {
        public string SSHServerUrl
        {
            get => ConfigurationManager.AppSettings["SSHServerUrl"] ?? string.Empty;
            set
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["SSHServerUrl"].Value = value;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
        }

        public string SSHPort
        {
            get => ConfigurationManager.AppSettings["SSHPort"] ?? string.Empty;
            set
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["SSHPort"].Value = value;
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
        }

        public bool SSHTunnel
        {
            get => bool.Parse(ConfigurationManager.AppSettings["SSHTunnel"] ?? "false");
            set
            {
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["SSHTunnel"].Value = value.ToString();
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
        }
    }
}
