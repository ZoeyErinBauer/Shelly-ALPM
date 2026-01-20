#!/bin/bash

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root"
  exit 1
fi

INSTALL_DIR="/usr/bin/Shelly"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Creating installation directory: $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"

echo "Copying files to $INSTALL_DIR"
cp -r "$SCRIPT_DIR"/* "$INSTALL_DIR/"

# Ensure the binary is executable
if [ -f "$INSTALL_DIR/Shelly-UI" ]; then
    chmod +x "$INSTALL_DIR/Shelly-UI"
fi

# Ensure the CLI binary is executable and accessible in PATH
if [ -f "$INSTALL_DIR/Shelly-CLI" ]; then
    chmod +x "$INSTALL_DIR/Shelly-CLI"
    echo "Creating symlink for shelly-cli in /usr/local/bin"
    ln -sf "$INSTALL_DIR/Shelly-CLI" /usr/local/bin/shelly-cli
fi

echo "Creating desktop entry"
cat <<EOF > /usr/share/applications/shelly.desktop
[Desktop Entry]
Name=Shelly
Exec=$INSTALL_DIR/Shelly-UI
Icon=$INSTALL_DIR/shellylogo.png
Type=Application
Categories=System;Utility;
Terminal=false
EOF

REAL_USER=${SUDO_USER:-$USER}
USER_HOME=$(getent passwd "$REAL_USER" | cut -d: -f6)


USER_DESKTOP="$USER_HOME/Desktop"

if [ -d "$USER_DESKTOP" ]; then
    echo "Creating desktop icon for user: $REAL_USER"
    cp /usr/share/applications/shelly.desktop "$USER_DESKTOP/shelly.desktop"
    
    # Ensure the user owns the file and it's executable
    chown "$REAL_USER":"$REAL_USER" "$USER_DESKTOP/shelly.desktop"
    chmod +x "$USER_DESKTOP/shelly.desktop"
    
    # Mark as trusted (specific to some desktop environments like GNOME/KDE)
    gio set "$USER_DESKTOP/shelly.desktop" metadata::trusted true 2>/dev/null || true
else
    echo "Desktop directory not found for $REAL_USER, skipping desktop icon."
fi

echo "Installation complete!"
