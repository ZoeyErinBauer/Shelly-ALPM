#!/bin/bash
# This script allows debugging the application as root using pkexec.
# It is intended to be used as a custom executable in a Rider Run/Debug Configuration.

if [ "$#" -lt 1 ]; then
    echo "Usage: $0 <executable> [arguments...]"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DBUS_SERVICE_FILE="$SCRIPT_DIR/Shelly.Service/config/org.shelly.PackageManager.service"
DBUS_SYSTEM_DIR="/usr/share/dbus-1/system-services"

# Install the D-Bus service file if it exists
if [ -f "$DBUS_SERVICE_FILE" ]; then
    sudo mkdir -p "$DBUS_SYSTEM_DIR"
    sudo cp "$DBUS_SERVICE_FILE" "$DBUS_SYSTEM_DIR/"
fi

pkexec env DISPLAY=$DISPLAY XAUTHORITY=$XAUTHORITY "$@"
