#!/bin/bash
# setup-linux-vm.sh
# Setup script for KafkaTestC on Linux VM

# Create installation directory
echo "Creating installation directory..."
mkdir -p /opt/KafkaTestC

# Copy application files
echo "Installing KafkaTestC..."
cp -f KafkaTestC /opt/KafkaTestC/
chmod +x /opt/KafkaTestC/KafkaTestC

# Create startup script
echo "Creating startup script..."
cat > /opt/KafkaTestC/start-kafka-client.sh << 'EOF'
#!/bin/bash
cd /opt/KafkaTestC
./KafkaTestC
EOF

chmod +x /opt/KafkaTestC/start-kafka-client.sh

# Create symbolic link for easier access
echo "Creating symbolic link..."
ln -sf /opt/KafkaTestC/start-kafka-client.sh /usr/local/bin/kafka-test-client

# Create systemd service file (optional)
echo "Creating systemd service file..."
cat > /etc/systemd/system/kafka-test-client.service << 'EOF'
[Unit]
Description=Kafka Test Client for Confluent Cloud
After=network.target

[Service]
ExecStart=/opt/KafkaTestC/KafkaTestC
WorkingDirectory=/opt/KafkaTestC
Restart=on-failure
User=root

[Install]
WantedBy=multi-user.target
EOF

echo "Installation completed!"
echo "You can run the application using: /opt/KafkaTestC/start-kafka-client.sh"
echo "Or using the shortcut: kafka-test-client"
echo ""
echo "To run as a service (optional):"
echo "  systemctl daemon-reload"
echo "  systemctl enable kafka-test-client"
echo "  systemctl start kafka-test-client"
