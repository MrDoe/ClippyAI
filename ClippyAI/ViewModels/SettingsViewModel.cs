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
        private string _localTunnel = ConfigurationManager.AppSettings["LocalTunnel"] ?? string.Empty;

        [ObservableProperty]
        private string _remoteTunnel = ConfigurationManager.AppSettings["RemoteTunnel"] ?? string.Empty;

        [ObservableProperty]
        private string _publicKey = ConfigurationManager.AppSettings["PublicKey"] ?? string.Empty;

        [ObservableProperty]
        private bool _sshTunnel = bool.Parse(ConfigurationManager.AppSettings["SSHTunnel"] ?? "false");

        public void SaveSettings()
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings["SSHServerUrl"].Value = SSHServerUrl;
            config.AppSettings.Settings["SSHPort"].Value = SSHPort;
            config.AppSettings.Settings["LocalTunnel"].Value = LocalTunnel;
            config.AppSettings.Settings["RemoteTunnel"].Value = RemoteTunnel;
            config.AppSettings.Settings["PublicKey"].Value = PublicKey;
            config.AppSettings.Settings["SSHTunnel"].Value = SSHTunnel.ToString();
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        public void LoadSettings()
        {
            SSHServerUrl = ConfigurationManager.AppSettings["SSHServerUrl"] ?? string.Empty;
            SSHPort = ConfigurationManager.AppSettings["SSHPort"] ?? string.Empty;
            LocalTunnel = ConfigurationManager.AppSettings["LocalTunnel"] ?? string.Empty;
            RemoteTunnel = ConfigurationManager.AppSettings["RemoteTunnel"] ?? string.Empty;
            PublicKey = ConfigurationManager.AppSettings["PublicKey"] ?? string.Empty;
            SSHTunnel = bool.Parse(ConfigurationManager.AppSettings["SSHTunnel"] ?? "false");
        }
    }
}
