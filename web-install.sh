#!/bin/bash

# Shelly-ALPM Web Installer
# Usage: curl -fsSL https://raw.githubusercontent.com/USER/Shelly-ALPM/main/web-install.sh | sudo bash

set -e

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (use sudo)"
  exit 1
fi

REPO="ZoeyErinBauer/Shelly-ALPM"
ASSET_NAME="Shelly-ALPM-linux-x64.tar.gz"
TMP_DIR=$(mktemp -d)

cleanup() {
    rm -rf "$TMP_DIR"
}
trap cleanup EXIT

echo "Fetching latest release from GitHub..."
LATEST_URL=$(curl -fsSL "https://api.github.com/repos/ZoeyErinBauer/Shelly-ALPM/releases/latest" | grep "browser_download_url.*$ASSET_NAME" | cut -d '"' -f 4)

if [ -z "$LATEST_URL" ]; then
    echo "Error: Could not find $ASSET_NAME in the latest release"
    exit 1
fi

echo "Downloading $ASSET_NAME..."
curl -fsSL -o "$TMP_DIR/$ASSET_NAME" "$LATEST_URL"

echo "Extracting archive..."
tar -xzf "$TMP_DIR/$ASSET_NAME" -C "$TMP_DIR"

# Find the extracted directory
EXTRACT_DIR=$(find "$TMP_DIR" -maxdepth 1 -type d ! -path "$TMP_DIR" | head -1)
if [ -z "$EXTRACT_DIR" ]; then
    EXTRACT_DIR="$TMP_DIR"
fi

echo "Installing binaries to /usr/bin..."

# Install Shelly-UI binary
if [ -f "$EXTRACT_DIR/Shelly-UI" ]; then
    install -Dm755 "$EXTRACT_DIR/Shelly-UI" /usr/bin/shelly-ui
fi

# Install native libraries alongside binaries (same as PKGBUILD)
if [ -f "$EXTRACT_DIR/libSkiaSharp.so" ]; then
    install -Dm755 "$EXTRACT_DIR/libSkiaSharp.so" /usr/bin/libSkiaSharp.so
fi

if [ -f "$EXTRACT_DIR/libHarfBuzzSharp.so" ]; then
    install -Dm755 "$EXTRACT_DIR/libHarfBuzzSharp.so" /usr/bin/libHarfBuzzSharp.so
fi

# Install Shelly-CLI binary
if [ -f "$EXTRACT_DIR/shelly" ]; then
    install -Dm755 "$EXTRACT_DIR/shelly" /usr/bin/shelly
fi

REAL_USER=${SUDO_USER:-$USER}
USER_HOME=$(getent passwd "$REAL_USER" | cut -d: -f6)

# Install icon to standard location
echo "Installing icon..."
if [ -f "$EXTRACT_DIR/shellylogo.png" ]; then
    install -Dm644 "$EXTRACT_DIR/shellylogo.png" /usr/share/icons/hicolor/256x256/apps/shelly.png
fi

# Update icon cache so KDE and other DEs pick up the new icon
echo "Updating icon cache..."
if command -v gtk-update-icon-cache &> /dev/null; then
    gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
fi
if command -v xdg-icon-resource &> /dev/null; then
    xdg-icon-resource forceupdate 2>/dev/null || true
fi
if command -v kbuildsycoca5 &> /dev/null; then
    sudo -u "$REAL_USER" kbuildsycoca5 2>/dev/null || true
elif command -v kbuildsycoca6 &> /dev/null; then
    sudo -u "$REAL_USER" kbuildsycoca6 2>/dev/null || true
fi

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
echo "You can run Shelly with: shelly-ui"
echo "You can run Shelly-CLI with: shelly"
