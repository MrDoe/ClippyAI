using Avalonia.Controls;
using System.Configuration;
using ClippyAI.Services;

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
            get => ConfigurationService.GetConfigurationValue("SSHServerUrl");
            set => ConfigurationService.SetConfigurationValue("SSHServerUrl", value);
        }

        public string SSHPort
        {
            get => ConfigurationService.GetConfigurationValue("SSHPort");
            set => ConfigurationService.SetConfigurationValue("SSHPort", value);
        }

        public bool SSHTunnel
        {
            get => bool.Parse(ConfigurationService.GetConfigurationValue("SSHTunnel", "false"));
            set => ConfigurationService.SetConfigurationValue("SSHTunnel", value.ToString());
        }
    }
}
