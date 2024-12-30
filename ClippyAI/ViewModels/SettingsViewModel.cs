using System.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClippyAI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sshServerUrl = ConfigurationManager.AppSettings["SSHServerUrl"] ?? string.Empty;

        [ObservableProperty]
        private string _sshPort = ConfigurationManager.AppSettings["SSHPort"] ?? string.Empty;

        [ObservableProperty]
        private string _sshLocalTunnel = ConfigurationManager.AppSettings["SSHLocalTunnel"] ?? string.Empty;

        [ObservableProperty]
        private string _sshRemoteTunnel = ConfigurationManager.AppSettings["SSHRemoteTunnel"] ?? string.Empty;

        [ObservableProperty]
        private string _sshPublicKey = ConfigurationManager.AppSettings["SSHPublicKey"] ?? string.Empty;

        public void SaveSettings()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["SSHServerUrl"].Value = SSHServerUrl;
            config.AppSettings.Settings["SSHPort"].Value = SSHPort;
            config.AppSettings.Settings["SSHLocalTunnel"].Value = SSHLocalTunnel;
            config.AppSettings.Settings["SSHRemoteTunnel"].Value = SSHRemoteTunnel;
            config.AppSettings.Settings["SSHPublicKey"].Value = SSHPublicKey;
            config.AppSettings.Settings["SSHTunnel"].Value = SSHTunnel.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public void LoadSettings()
        {
            SSHServerUrl = ConfigurationManager.AppSettings["SSHServerUrl"] ?? string.Empty;
            SSHPort = ConfigurationManager.AppSettings["SSHPort"] ?? string.Empty;
            SSHLocalTunnel = ConfigurationManager.AppSettings["SSHLocalTunnel"] ?? string.Empty;
            SSHRemoteTunnel = ConfigurationManager.AppSettings["SSHRemoteTunnel"] ?? string.Empty;
            SSHPublicKey = ConfigurationManager.AppSettings["SSHPublicKey"] ?? string.Empty;
            SSHTunnel = bool.Parse(ConfigurationManager.AppSettings["SSHTunnel"] ?? "false");
        }
    }
}
