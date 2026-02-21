#!/bin/bash

# Install Script for Shelly-ALPM
# This script installs pre-built Shelly binaries from a release package.

set -e  # Exit on any error

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (use sudo)"
  exit 1
fi

INSTALL_DIR="/usr/bin"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=========================================="
echo "Shelly Install Script"
echo "=========================================="
echo ""

echo "Script directory: $SCRIPT_DIR"
echo "Install directory: $INSTALL_DIR"
echo ""

# Install Shelly-UI binary
echo "Installing Shelly-UI to $INSTALL_DIR"
install -Dm755 "$SCRIPT_DIR/Shelly-UI" "$INSTALL_DIR/shelly-ui"

# Install Shelly-Notifications binary
echo "Installing Shelly-Notifications to $INSTALL_DIR"
install -Dm755 "$SCRIPT_DIR/Shelly-Notifications" "$INSTALL_DIR/shelly-notifications"

# Install Shelly-CLI binary
echo "Installing Shelly-CLI to $INSTALL_DIR"
install -Dm755 "$SCRIPT_DIR/shelly" "$INSTALL_DIR/shelly"

# Install bundled native libraries
echo "Installing native libraries..."
install -Dm755 "$SCRIPT_DIR/libSkiaSharp.so" /usr/lib/libSkiaSharp.so
install -Dm755 "$SCRIPT_DIR/libHarfBuzzSharp.so" /usr/lib/libHarfBuzzSharp.so

# Install icon to standard location
echo "Installing icon to standard location..."
mkdir -p /usr/share/icons/hicolor/256x256/apps
install -Dm644 "$SCRIPT_DIR/shellylogo.png" /usr/share/icons/hicolor/256x256/apps/shelly.png

# Create desktop entry
echo "Creating desktop entry"
cat <<EOF > /usr/share/applications/shelly.desktop
[Desktop Entry]
Name=Shelly
Exec=/usr/bin/shelly-ui
Icon=shelly
Type=Application
Categories=System;Utility;
Terminal=false
EOF

echo ""
echo "=========================================="
echo "Installation complete!"
echo "=========================================="
echo ""
echo "You can now:"
echo "  - Run the GUI: shelly-ui"
echo "  - Run the CLI: shelly"
echo "  - Notification Service: shelly-notifications"
echo "  - Find Shelly in your application menu"
echo ""
