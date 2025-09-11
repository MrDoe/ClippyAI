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
            string sshSetting = ConfigurationManager.AppSettings["UseSSH"] ?? "false";
            string sshUsername = ConfigurationManager.AppSettings["SSHUsername"] ?? "";
            string sshServerUrl = ConfigurationManager.AppSettings["SSHServerUrl"] ?? "";
            int sshPort = int.Parse(ConfigurationManager.AppSettings["SSHPort"] ?? "0");
            string sshLocalTunnel = ConfigurationManager.AppSettings["SSHLocalTunnel"] ?? "";
            string sshRemoteTunnel = ConfigurationManager.AppSettings["SSHRemoteTunnel"] ?? "";
            string sshPrivateKeyFile = ConfigurationManager.AppSettings["SSHPrivateKeyFile"] ?? "";

            if (!sshSetting.Equals("true", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(sshUsername) ||
                 string.IsNullOrWhiteSpace(sshServerUrl) || sshPort == 0 || string.IsNullOrWhiteSpace(sshLocalTunnel))
                return;

            // connect via public key
            var keyFile = new PrivateKeyFile(Environment.ExpandEnvironmentVariables(sshPrivateKeyFile));
            var keyFiles = new[] { keyFile };
            var methods = new AuthenticationMethod[]
            { new PrivateKeyAuthenticationMethod(sshUsername, keyFiles) };
            var connectionInfo = new ConnectionInfo(sshServerUrl, sshPort, sshUsername, methods);
            _sshClient = new SshClient(connectionInfo);

            if (sshLocalTunnel != "")
            {
                // split tunnel variable
                var tunnelParts = sshLocalTunnel.Split(':');
                _forwardedPort = new ForwardedPortLocal(tunnelParts[0], uint.Parse(tunnelParts[1]), tunnelParts[2], uint.Parse(tunnelParts[3]));
                _forwardedPort.Start();
                _sshClient.AddForwardedPort(_forwardedPort);
            }

            if(sshRemoteTunnel != "")
            {
                // split tunnel variable
                var tunnelParts = sshRemoteTunnel.Split(':');
                var remotePort = new ForwardedPortRemote(tunnelParts[0], uint.Parse(tunnelParts[1]), tunnelParts[2], uint.Parse(tunnelParts[3]));
                _sshClient.AddForwardedPort(remotePort);
                remotePort.Start();
            }

            _sshClient.Connect();

            if (!_sshClient.IsConnected)
            {
                throw new Exception("SSH connection failed.");
            }
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
