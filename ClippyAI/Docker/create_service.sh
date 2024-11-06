#!/bin/bash

# Variables
SERVICE_NAME="my_docker_compose_service"
COMPOSE_FILE="/path/to/docker-compose.yml"
WORKING_DIR="/path/to/working/directory"

# Create the systemd service file
sudo bash -c "cat > /etc/systemd/system/${SERVICE_NAME}.service <<EOF
[Unit]
Description=Docker Compose Service
After=network.target

[Service]
Type=oneshot
WorkingDirectory=${WORKING_DIR}
ExecStart=/usr/local/bin/docker-compose -f ${COMPOSE_FILE} up -d
ExecStop=/usr/local/bin/docker-compose -f ${COMPOSE_FILE} down
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF"

# Reload systemd daemon
sudo systemctl daemon-reload

# Enable and start the service
sudo systemctl enable ${SERVICE_NAME}
sudo systemctl start ${SERVICE_NAME}