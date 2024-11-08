#!/bin/bash
if [[ $(id -u) == 0 ]]; then
  echo "Installing service..."
  mkdir /etc/pgai -p
  cp docker-compose.yml /etc/pgai/docker-compose.yml
  cp pgai.service /etc/systemd/system/pgai.service
  systemctl daemon-reload
  systemctl enable pgai
  systemctl start pgai
  echo "Installation complete!"
else
  echo "Root permissions are not available. Please execute this script with 'sudo'!"
fi
