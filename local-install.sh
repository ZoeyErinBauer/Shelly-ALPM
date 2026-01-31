#!/bin/bash

# Shelly Installation Script
set -e

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (use sudo)"
  exit 1
fi

INSTALL_DIR="/opt/shelly"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_CONFIG="Release"

echo "=========================================="
echo "Installing Shelly..."
echo "=========================================="

# Check for .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed. Please install .NET 10.0 SDK first."
    exit 1
fi

# 1. Build projects
echo "Building Shelly-UI..."
dotnet publish "$SCRIPT_DIR/Shelly-UI/Shelly-UI.csproj" -c $BUILD_CONFIG -r linux-x64 --self-contained true -o "$SCRIPT_DIR/publish/Shelly-UI"

echo "Building Shelly-CLI..."
dotnet publish "$SCRIPT_DIR/Shelly-CLI/Shelly-CLI.csproj" -c $BUILD_CONFIG -r linux-x64 --self-contained true -o "$SCRIPT_DIR/publish/Shelly-CLI"

# 2. Prepare installation directory
echo "Cleaning old installation..."
rm -rf "$INSTALL_DIR"
mkdir -p "$INSTALL_DIR"

# 3. Copy binaries and libraries
echo "Installing binaries and libraries to $INSTALL_DIR..."
# The UI binary is named Shelly-UI in its publish folder
cp "$SCRIPT_DIR/publish/Shelly-UI/Shelly-UI" "$INSTALL_DIR/shelly-ui"
# Copy native libraries required by Avalonia/SkiaSharp
cp "$SCRIPT_DIR/publish/Shelly-UI/"*.so "$INSTALL_DIR/" 2>/dev/null || true
# The CLI binary is named shelly in its publish folder (due to AssemblyName)
cp "$SCRIPT_DIR/publish/Shelly-CLI/shelly" "$INSTALL_DIR/shelly"

# Copy icons and assets
echo "Installing assets..."
cp "$SCRIPT_DIR/Shelly-UI/Assets/shellylogo.png" "$INSTALL_DIR/" 2>/dev/null || true

# 4. Set permissions
chmod +x "$INSTALL_DIR/shelly-ui"
chmod +x "$INSTALL_DIR/shelly"

# 5. Create symlinks
echo "Creating symlinks in /usr/bin..."
ln -sf "$INSTALL_DIR/shelly-ui" /usr/bin/shelly-ui
ln -sf "$INSTALL_DIR/shelly" /usr/bin/shelly

# 6. Install icon to standard location
echo "Installing system icon..."
mkdir -p /usr/share/icons/hicolor/256x256/apps
cp "$INSTALL_DIR/shellylogo.png" /usr/share/icons/hicolor/256x256/apps/shelly.png 2>/dev/null || true

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

# Cleanup build artifacts
echo "Cleaning up..."
rm -rf "$SCRIPT_DIR/publish"

echo ""
echo "=========================================="
echo "Installation complete!"
echo "Run 'shelly-ui' to start the GUI or 'shelly' for CLI."
echo "=========================================="

