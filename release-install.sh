#!/bin/bash

# Shelly Binary Installer (for Release Packages)
set -e

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (use sudo)"
  exit 1
fi

INSTALL_DIR="/opt/shelly"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "=========================================="
echo "Updating Shelly..."
echo "=========================================="

# 1. Prepare installation directory
# DO NOT remove the directory, just ensure it exists
mkdir -p "$INSTALL_DIR"

# 2. Copy binaries and libraries
echo "Installing binaries and libraries to $INSTALL_DIR..."

# The binaries are in the same directory as this script in the release package
if [ -f "$SCRIPT_DIR/Shelly-UI" ]; then
    cp "$SCRIPT_DIR/Shelly-UI" "$INSTALL_DIR/shelly-ui"
fi

# Copy native libraries (if any are present)
# Using 'cp' with wildcard might fail if no files match, handling that
cp "$SCRIPT_DIR/"*.so "$INSTALL_DIR/" 2>/dev/null || true

if [ -f "$SCRIPT_DIR/shelly" ]; then
    cp "$SCRIPT_DIR/shelly" "$INSTALL_DIR/shelly"
fi

# 3. Copy icons and assets
echo "Installing assets..."
if [ -f "$SCRIPT_DIR/shellylogo.png" ]; then
    cp "$SCRIPT_DIR/shellylogo.png" "$INSTALL_DIR/"
fi

# 4. Set permissions
if [ -f "$INSTALL_DIR/shelly-ui" ]; then
    chmod +x "$INSTALL_DIR/shelly-ui"
fi
if [ -f "$INSTALL_DIR/shelly" ]; then
    chmod +x "$INSTALL_DIR/shelly"
fi

# 5. Create symlinks
echo "Creating symlinks in /usr/bin..."
ln -sf "$INSTALL_DIR/shelly-ui" /usr/bin/shelly-ui
ln -sf "$INSTALL_DIR/shelly" /usr/bin/shelly

# 6. Install icon to standard location
echo "Installing system icon..."
mkdir -p /usr/share/icons/hicolor/256x256/apps
if [ -f "$INSTALL_DIR/shellylogo.png" ]; then
    cp "$INSTALL_DIR/shellylogo.png" /usr/share/icons/hicolor/256x256/apps/shelly.png 2>/dev/null || true
fi

# 7. Create desktop entry
echo "Creating desktop entry..."
cat <<EOF > /usr/share/applications/shelly.desktop
[Desktop Entry]
Name=Shelly
Comment=Visual Arch Linux Package Manager
Exec=/usr/bin/shelly-ui
Icon=shelly
Type=Application
Categories=System;Utility;
Terminal=false
StartupNotify=true
EOF

# 8. Create user desktop icon if applicable
REAL_USER=${SUDO_USER:-$USER}
USER_HOME=$(getent passwd "$REAL_USER" | cut -d: -f6)
USER_DESKTOP="$USER_HOME/Desktop"

if [ -d "$USER_DESKTOP" ]; then
    echo "Creating desktop icon for user: $REAL_USER"
    cp /usr/share/applications/shelly.desktop "$USER_DESKTOP/shelly.desktop"
    chown "$REAL_USER":"$REAL_USER" "$USER_DESKTOP/shelly.desktop"
    chmod +x "$USER_DESKTOP/shelly.desktop"
    # Mark as trusted for GNOME/KDE
    sudo -u "$REAL_USER" gio set "$USER_DESKTOP/shelly.desktop" metadata::trusted true 2>/dev/null || true
fi

echo ""
echo "=========================================="
echo "Update complete!"
echo "Please restart Shelly."
echo "=========================================="
