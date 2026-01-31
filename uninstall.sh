#!/bin/bash

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root"
  exit 1
fi

echo "Uninstalling Shelly..."

# Remove symlinks
echo "Removing symlinks from /usr/bin..."
rm -f /usr/bin/shelly-ui
rm -f /usr/bin/shelly

# Remove desktop entry
echo "Removing desktop entry..."
rm -f /usr/share/applications/shelly.desktop

# Remove icon
echo "Removing icon..."
rm -f /usr/share/icons/hicolor/256x256/apps/shelly.png

# Attempt to remove user desktop icon
REAL_USER=${SUDO_USER:-$USER}
USER_HOME=$(getent passwd "$REAL_USER" | cut -d: -f6)
USER_DESKTOP="$USER_HOME/Desktop"

if [ -f "$USER_DESKTOP/shelly.desktop" ]; then
  echo "Removing desktop icon for user: $REAL_USER"
  rm -f "$USER_DESKTOP/shelly.desktop"
fi

# Remove installation directory
INSTALL_DIR="/opt/shelly"
if [ -d "$INSTALL_DIR" ]; then
  echo "Removing installation directory: $INSTALL_DIR"
  rm -rf "$INSTALL_DIR"
fi

echo "Uninstallation complete!"
