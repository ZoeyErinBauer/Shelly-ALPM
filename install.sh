#!/bin/bash

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root"
  exit 1
fi

INSTALL_DIR="/usr/bin/Shelly"
SERVICE_DIR="/usr/bin/Shelly-Service"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Creating installation directories..."
mkdir -p "$INSTALL_DIR"
mkdir -p "$SERVICE_DIR"

echo "Copying UI files to $INSTALL_DIR"
cp -r "$SCRIPT_DIR/Shelly-UI/"* "$INSTALL_DIR/" 2>/dev/null || cp -r "$SCRIPT_DIR"/* "$INSTALL_DIR/"

echo "Copying service files to $SERVICE_DIR"
if [ -d "$SCRIPT_DIR/Shelly-Service" ]; then
    cp -r "$SCRIPT_DIR/Shelly-Service/"* "$SERVICE_DIR/"
fi

# Ensure the binaries are executable
if [ -f "$INSTALL_DIR/Shelly-UI" ]; then
    chmod +x "$INSTALL_DIR/Shelly-UI"
fi

if [ -f "$SERVICE_DIR/shelly-service" ]; then
    chmod +x "$SERVICE_DIR/shelly-service"
    # Create symlink in /usr/bin for systemd
    ln -sf "$SERVICE_DIR/shelly-service" /usr/bin/shelly-service
fi

echo "Installing systemd service..."
if [ -f "$SCRIPT_DIR/Shelly.Service/config/shelly.service" ]; then
    cp "$SCRIPT_DIR/Shelly.Service/config/shelly.service" /etc/systemd/system/
elif [ -f "$SERVICE_DIR/config/shelly.service" ]; then
    cp "$SERVICE_DIR/config/shelly.service" /etc/systemd/system/
fi

echo "Installing D-Bus configuration..."
if [ -f "$SCRIPT_DIR/Shelly.Service/config/org.shelly.PackageManager.conf" ]; then
    cp "$SCRIPT_DIR/Shelly.Service/config/org.shelly.PackageManager.conf" /etc/dbus-1/system.d/
elif [ -f "$SERVICE_DIR/config/org.shelly.PackageManager.conf" ]; then
    cp "$SERVICE_DIR/config/org.shelly.PackageManager.conf" /etc/dbus-1/system.d/
fi

echo "Installing D-Bus service activation file..."
mkdir -p /usr/share/dbus-1/system-services/
if [ -f "$SCRIPT_DIR/Shelly.Service/config/org.shelly.PackageManager.service" ]; then
    cp "$SCRIPT_DIR/Shelly.Service/config/org.shelly.PackageManager.service" /usr/share/dbus-1/system-services/
elif [ -f "$SERVICE_DIR/config/org.shelly.PackageManager.service" ]; then
    cp "$SERVICE_DIR/config/org.shelly.PackageManager.service" /usr/share/dbus-1/system-services/
fi

echo "Installing Polkit policy..."
if [ -f "$SCRIPT_DIR/Shelly.Service/config/org.shelly.policy" ]; then
    cp "$SCRIPT_DIR/Shelly.Service/config/org.shelly.policy" /usr/share/polkit-1/actions/
elif [ -f "$SERVICE_DIR/config/org.shelly.policy" ]; then
    cp "$SERVICE_DIR/config/org.shelly.policy" /usr/share/polkit-1/actions/
fi

echo "Reloading systemd daemon..."
systemctl daemon-reload

echo "Enabling and starting shelly service..."
systemctl enable shelly.service
systemctl start shelly.service

echo "Creating desktop entry..."
cat <<EOF > /usr/share/applications/shelly.desktop
[Desktop Entry]
Name=Shelly
Comment=Arch Linux Package Manager
Exec=$INSTALL_DIR/Shelly-UI
Icon=$INSTALL_DIR/shellylogo.png
Type=Application
Categories=System;PackageManager;
Terminal=false
EOF

echo ""
echo "Installation complete!"
echo ""
echo "The Shelly service is now running. You can check its status with:"
echo "  systemctl status shelly.service"
echo ""
echo "To start the GUI, run: $INSTALL_DIR/Shelly-UI"
echo "Or find 'Shelly' in your application menu."
