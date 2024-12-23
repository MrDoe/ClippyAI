using Renci.SshNet;
using System;
using System.Configuration;

namespace ClippyAI.Services
{
    public class SSHService
    {
        private SshClient? _sshClient;
        private ForwardedPortLocal? _forwardedPort;

        public void Connect()
        {
            string sshServerUrl = ConfigurationManager.AppSettings["SSHServerUrl"] ?? throw new ArgumentNullException("SSHServerUrl");
            int sshPort = int.Parse(ConfigurationManager.AppSettings["SSHPort"] ?? throw new ArgumentNullException("SSHPort"));
            bool sshTunnel = bool.Parse(ConfigurationManager.AppSettings["SSHTunnel"] ?? throw new ArgumentNullException("SSHTunnel"));

            if (!sshTunnel)
                return;

            _sshClient = new SshClient(sshServerUrl, sshPort, "username", "password");
            _sshClient.Connect();

            _forwardedPort = new ForwardedPortLocal("127.0.0.1", 3306, "remote.server.com", 3306);
            _sshClient.AddForwardedPort(_forwardedPort);
            _forwardedPort.Start();
        }

        public void Disconnect()
        {
            if (_forwardedPort != null && _forwardedPort.IsStarted)
            {
                _forwardedPort.Stop();
            }

            if (_sshClient != null && _sshClient.IsConnected)
            {
                _sshClient.Disconnect();
            }
        }

        public bool IsConnected()
        {
            return _sshClient != null && _sshClient.IsConnected;
        }
    }
}
