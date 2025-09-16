using Renci.SshNet;
using System;
using System.Configuration;
using System.IO;
using ClippyAI.Services;

namespace ClippyAI.Services
{
    public class SSHService
    {
        private SshClient? _sshClient;
        private ForwardedPortLocal? _forwardedPort;
        private ForwardedPortRemote? _remoteForwardedPort;

        public void Connect()
        {
            try
            {
                string sshSetting = ConfigurationService.GetConfigurationValue("UseSSH", "false");
                string sshUsername = ConfigurationService.GetConfigurationValue("SSHUsername");
                string sshServerUrl = ConfigurationService.GetConfigurationValue("SSHServerUrl");
                int sshPort = int.Parse(ConfigurationService.GetConfigurationValue("SSHPort", "22"));
                string sshLocalTunnel = ConfigurationService.GetConfigurationValue("SSHLocalTunnel");
                string sshRemoteTunnel = ConfigurationService.GetConfigurationValue("SSHRemoteTunnel");
                string sshPrivateKeyFile = ConfigurationService.GetConfigurationValue("SSHPrivateKeyFile");

                // Validate configuration
                if (!sshSetting.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return; // SSH disabled
                }

                if (string.IsNullOrWhiteSpace(sshUsername) || string.IsNullOrWhiteSpace(sshServerUrl) || sshPort <= 0)
                {
                    throw new ArgumentException("SSH configuration incomplete: username, server URL, and port are required");
                }

                // Validate private key file
                string expandedKeyPath = Environment.ExpandEnvironmentVariables(sshPrivateKeyFile);
                if (string.IsNullOrWhiteSpace(expandedKeyPath) || !File.Exists(expandedKeyPath))
                {
                    throw new FileNotFoundException($"SSH private key file not found: {expandedKeyPath}");
                }

                // Create SSH connection
                var keyFile = new PrivateKeyFile(expandedKeyPath);
                var keyFiles = new[] { keyFile };
                // allow ssh-rsa keys
                

                var methods = new AuthenticationMethod[]
                {
                    new PrivateKeyAuthenticationMethod(sshUsername, keyFiles)
                };
                var connectionInfo = new ConnectionInfo(sshServerUrl, sshPort, sshUsername, methods);
                _sshClient = new SshClient(connectionInfo);

                // Connect first
                _sshClient.Connect();

                if (!_sshClient.IsConnected)
                {
                    Console.WriteLine("SSH connection failed - unable to establish connection");
                    return;
                }

                // Setup port forwarding after successful connection
                if (!string.IsNullOrWhiteSpace(sshLocalTunnel))
                {
                    SetupLocalPortForwarding(sshLocalTunnel);
                }

                if (!string.IsNullOrWhiteSpace(sshRemoteTunnel))
                {
                    SetupRemotePortForwarding(sshRemoteTunnel);
                }
            }
            catch (Exception ex)
            {
                // Clean up on failure
                Disconnect();
                Console.WriteLine($"SSH connection failed: {ex.Message}", ex);
            }
        }

        private void SetupLocalPortForwarding(string tunnelConfig)
        {
            var tunnelParts = tunnelConfig.Split(':');
            if (tunnelParts.Length != 4)
            {
                Console.WriteLine($"Invalid local tunnel format. Expected 'localHost:localPort:remoteHost:remotePort', got: {tunnelConfig}");
                return;
            }

            string localHost = tunnelParts[0];
            uint localPort = uint.Parse(tunnelParts[1]);
            string remoteHost = tunnelParts[2];
            uint remotePort = uint.Parse(tunnelParts[3]);

            _forwardedPort = new ForwardedPortLocal(localHost, localPort, remoteHost, remotePort);
            _sshClient!.AddForwardedPort(_forwardedPort);
            _forwardedPort.Start();
        }

        private void SetupRemotePortForwarding(string tunnelConfig)
        {
            var tunnelParts = tunnelConfig.Split(':');
            if (tunnelParts.Length != 4)
            {
                Console.WriteLine($"Invalid remote tunnel format. Expected 'remoteHost:remotePort:localHost:localPort', got: {tunnelConfig}");
                return;
            }

            string remoteHost = tunnelParts[0];
            uint remotePort = uint.Parse(tunnelParts[1]);
            string localHost = tunnelParts[2];
            uint localPort = uint.Parse(tunnelParts[3]);

            _remoteForwardedPort = new ForwardedPortRemote(remoteHost, remotePort, localHost, localPort);
            _sshClient!.AddForwardedPort(_remoteForwardedPort);
            _remoteForwardedPort.Start();
        }

        public void Disconnect()
        {
            try
            {
                if (_forwardedPort != null && _forwardedPort.IsStarted)
                {
                    _forwardedPort.Stop();
                    _forwardedPort = null;
                }

                if (_remoteForwardedPort != null && _remoteForwardedPort.IsStarted)
                {
                    _remoteForwardedPort.Stop();
                    _remoteForwardedPort = null;
                }

                if (_sshClient != null && _sshClient.IsConnected)
                {
                    _sshClient.Disconnect();
                }

                _sshClient?.Dispose();
                _sshClient = null;
            }
            catch (Exception)
            {
                // Ignore errors during cleanup
            }
        }

        public bool IsConnected()
        {
            return _sshClient != null && _sshClient.IsConnected;
        }

        /// <summary>
        /// Tests the SSH connection without setting up port forwarding
        /// </summary>
        /// <returns>Test result message</returns>
        public string TestConnection()
        {
            try
            {
                string sshSetting = ConfigurationService.GetConfigurationValue("UseSSH", "false");
                string sshUsername = ConfigurationService.GetConfigurationValue("SSHUsername");
                string sshServerUrl = ConfigurationService.GetConfigurationValue("SSHServerUrl");
                int sshPort = int.Parse(ConfigurationService.GetConfigurationValue("SSHPort", "22"));
                string sshPrivateKeyFile = ConfigurationService.GetConfigurationValue("SSHPrivateKeyFile");

                // Validate configuration
                if (!sshSetting.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return "SSH is disabled in configuration";
                }

                if (string.IsNullOrWhiteSpace(sshUsername) || string.IsNullOrWhiteSpace(sshServerUrl) || sshPort <= 0)
                {
                    return "SSH configuration incomplete: username, server URL, and port are required";
                }

                // Validate private key file
                string expandedKeyPath = Environment.ExpandEnvironmentVariables(sshPrivateKeyFile);
                if (string.IsNullOrWhiteSpace(expandedKeyPath) || !File.Exists(expandedKeyPath))
                {
                    return $"SSH private key file not found: {expandedKeyPath}";
                }

                // Create test SSH connection
                var keyFile = new PrivateKeyFile(expandedKeyPath);
                var keyFiles = new[] { keyFile };
                var methods = new AuthenticationMethod[]
                {
                    new PrivateKeyAuthenticationMethod(sshUsername, keyFiles)
                };
                var connectionInfo = new ConnectionInfo(sshServerUrl, sshPort, sshUsername, methods);
                
                using var testClient = new SshClient(connectionInfo);
                testClient.Connect();

                if (testClient.IsConnected)
                {
                    testClient.Disconnect();
                    return "SSH connection test successful!";
                }
                else
                {
                    return "SSH connection test failed - unable to establish connection";
                }
            }
            catch (Exception ex)
            {
                return $"SSH connection test failed: {ex.Message}";
            }
        }
    }
}
