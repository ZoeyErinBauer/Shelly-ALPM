#!/bin/bash

# Local Install Script for Shelly-ALPM
# This script builds and installs Shelly locally, similar to install.sh
# but starting from source code instead of pre-built binaries.

set -e  # Exit on any error

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (use sudo)"
  exit 1
fi

INSTALL_DIR="/opt/shelly"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_CONFIG="Release"

echo "=========================================="
echo "Shelly Local Install Script"
echo "=========================================="
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: .NET SDK is not installed. Please install .NET 10.0 SDK first."
    exit 1
fi

echo "Script directory: $SCRIPT_DIR"
echo "Install directory: $INSTALL_DIR"
echo ""

# Build Shelly-UI
echo "Building Shelly-UI..."
cd "$SCRIPT_DIR/Shelly-Notifications"
dotnet publish -c $BUILD_CONFIG -r linux-x64 --self-contained true -o "$SCRIPT_DIR/publish/Shelly-Notifications" -p:InstructionSet=x86-64-v3
echo "Shelly-UI build complete."
echo ""

# Build Shelly-UI
echo "Building Shelly-UI..."
cd "$SCRIPT_DIR/Shelly-UI"
dotnet publish -c $BUILD_CONFIG -r linux-x64 --self-contained true -o "$SCRIPT_DIR/publish/Shelly-UI" -p:InstructionSet=x86-64-v3
echo "Shelly-UI build complete."
echo ""

# Build Shelly-CLI
echo "Building Shelly-CLI..."
cd "$SCRIPT_DIR/Shelly-CLI"
dotnet publish -c $BUILD_CONFIG -r linux-x64 --self-contained true -o "$SCRIPT_DIR/publish/Shelly-CLI" -p:InstructionSet=x86-64-v3
echo "Shelly-CLI build complete."
echo ""

# Create installation directory
echo "Creating installation directory: $INSTALL_DIR"
mkdir -p "$INSTALL_DIR"

# Copy Shelly-Notifications files
echo "Copying Shelly-UI files to $INSTALL_DIR"
cp -r "$SCRIPT_DIR/publish/Shelly-Notifications/"* "$INSTALL_DIR/"

# Copy Shelly-UI files
echo "Copying Shelly-UI files to $INSTALL_DIR"
cp -r "$SCRIPT_DIR/publish/Shelly-UI/"* "$INSTALL_DIR/"

# Copy Shelly-CLI binary (output is named 'shelly' due to AssemblyName)
echo "Copying Shelly-CLI binary to $INSTALL_DIR"
cp "$SCRIPT_DIR/publish/Shelly-CLI/shelly" "$INSTALL_DIR/shelly"

# Copy the logo
echo "Copying logo..."
cp "$SCRIPT_DIR/Shelly-UI/Assets/shellylogo.png" "$INSTALL_DIR/"

# Install icon to standard location
echo "Installing icon to standard location..."
mkdir -p /usr/share/icons/hicolor/256x256/apps
cp "$INSTALL_DIR/shellylogo.png" /usr/share/icons/hicolor/256x256/apps/shelly.png

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

# Clean up publish directory (optional - comment out to keep build artifacts)
echo "Cleaning up build artifacts..."
rm -rf "$SCRIPT_DIR/publish"

echo ""
echo "=========================================="
echo "Installation complete!"
echo "=========================================="
echo ""
echo "You can now:"
echo "  - Run the GUI: shelly-ui (or $INSTALL_DIR/Shelly-UI)"
echo "  - Run the CLI: shelly (or $INSTALL_DIR/shelly)"
echo "  - Notification Service added: or $INSTALL_DIR/Shelly-Notifications"
echo "  - Find Shelly in your application menu"
echo ""
