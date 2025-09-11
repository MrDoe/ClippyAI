using System.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using ClippyAI.Services;

namespace ClippyAI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sSHServerUrl = ConfigurationService.GetConfigurationValue("SSHServerUrl");

        [ObservableProperty]
        private string _sSHPort = ConfigurationService.GetConfigurationValue("SSHPort");

        [ObservableProperty]
        private string _sSHLocalTunnel = ConfigurationService.GetConfigurationValue("SSHLocalTunnel");

        [ObservableProperty]
        private string _sSHRemoteTunnel = ConfigurationService.GetConfigurationValue("SSHRemoteTunnel");

        [ObservableProperty]
        private string _sSHPublicKey = ConfigurationService.GetConfigurationValue("SSHPublicKey");

        [ObservableProperty]
        private bool _sSHTunnel = bool.Parse(ConfigurationService.GetConfigurationValue("SSHTunnel", "false"));

        public void SaveSettings()
        {
            ConfigurationService.SetConfigurationValue("SSHServerUrl", SSHServerUrl);
            ConfigurationService.SetConfigurationValue("SSHPort", SSHPort);
            ConfigurationService.SetConfigurationValue("SSHLocalTunnel", SSHLocalTunnel);
            ConfigurationService.SetConfigurationValue("SSHRemoteTunnel", SSHRemoteTunnel);
            ConfigurationService.SetConfigurationValue("SSHPublicKey", SSHPublicKey);
            ConfigurationService.SetConfigurationValue("SSHTunnel", SSHTunnel.ToString());
        }

        public void LoadSettings()
        {
            SSHServerUrl = ConfigurationService.GetConfigurationValue("SSHServerUrl");
            SSHPort = ConfigurationService.GetConfigurationValue("SSHPort");
            SSHLocalTunnel = ConfigurationService.GetConfigurationValue("SSHLocalTunnel");
            SSHRemoteTunnel = ConfigurationService.GetConfigurationValue("SSHRemoteTunnel");
            SSHPublicKey = ConfigurationService.GetConfigurationValue("SSHPublicKey");
            SSHTunnel = bool.Parse(ConfigurationService.GetConfigurationValue("SSHTunnel", "false"));
        }
    }
}
