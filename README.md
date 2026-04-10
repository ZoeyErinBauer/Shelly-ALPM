![shelly_banner.png](shelly_banner.png)

![Shelly Wiki](https://img.shields.io/badge/Shelly-Wiki-blue)

<details>
  <summary>Screenshots</summary>
  <p align="center">
  Search Standard Packages, AUR, and Flatpak in one place

  <img width="1372" height="1019" alt="image" src="https://github.com/user-attachments/assets/6aa86662-d9f6-4d3c-9164-9df5d05257b3" />
  <img width="1768" height="1177" alt="image" src="https://github.com/user-attachments/assets/8e9d851b-a3a0-4aaf-b91a-b3b3c3ec7f6d" />
  <img width="1768" height="1177" alt="image" src="https://github.com/user-attachments/assets/cc2a8d31-e5c9-42d4-ba87-db25e10a1110" />
  </p>
</details>


### About

Shelly is a modern reimagination of the Arch Linux package manager, designed to be a more intuitive and user-friendly
alternative to `pacman` and `octopi`. Unlike other Arch package managers, Shelly offers a modern, visual interface with
a focus on
user experience and ease of use; It **IS NOT** built as a `pacman` wrapper or front-end. It is a complete reimagination
of how a user
interacts with their Arch Linux system, providing a more streamlined and intuitive experience.

## Quick Install

The recommended installation method for Shelly is for CachyOS or using CachyOS packages

```bash
sudo pacman -S shelly
```

This will download and install the latest release, including the UI and CLI tools.

To install with an AUR helper like yay or paru.

```bash
yay -S shelly
```

or

```bash
paru -S shelly
```

## Uninstall

#### For standard package removal

```bash
sudo pacman -Rns shelly
```

#### If installed from AUR

```bash
yay -Rns shelly
```

or

```bash
paru -Rns shelly
```

## Features

- **Modern-CLI**: Provides a command-line interface for advanced users and automation, with a focus on ease of use.
- **Native Arch Integration**: Directly interacts with `libalpm` for accurate and fast package management.
- **Native Wayland Support**: Front end built using GTK4.
- **Package Management**: Supports searching and filtering for, installing, updating, and removing packages.
- **Repository Management**: Synchronizes with official repositories to keep package lists up to date.
- **AUR Support**: Integration with the Arch User Repository for a wider range of software.
- **Flatpak Support**: Manage Flatpak applications alongside native packages.

## Roadmap

Upcoming features and development targets:

- **Trigger updates from Notifications**: Allow users to trigger package updates from the notification system.
- **Repository Modification**: Allow modification of supported repositories (In progress).
- **App Image Support**: Further app image support similar to [AppLever](https://github.com/mijorus/gearlever). (In
  progress)
- **Flatpak Overhaul**: Improve Flatpak integration and management. Allow for management of flatpak app stream
  locations. (In progress)
- **Package Import**: Allow for import of a previously existing package list to bring the system back to a saved package
  state. (Not yet started)
- **Icons for Standard Packages**: Allow icons for standard package applications to be viewed while searching for 
  packages. (Architecting)

## Prerequisites

- **Arch Linux** (or an Arch-based distribution)
- **.NET 10.0 SDK** (for building)
- **libalpm** (provided by `pacman`)

#### Optional Prerequisites

- **Flatpak**: Can be installed via shelly inside settings by turning flatpak on.

## Installation

### Using PKGBUILD

Since Shelly is designed for Arch Linux, you can build and install it using the provided `PKGBUILD`:

```bash
git clone https://github.com/ZoeyErinBauer/Shelly-ALPM.git
cd Shelly-ALPM
makepkg -si
```

### Manual Build

You can also build the project manually using the .NET CLI:

```bash
dotnet publish Shelly.Gtk/Shelly.Gtk.csproj -c Release -o publish/shelly-ui
dotnet publish Shelly-CLI/Shelly-CLI.csproj -c Release -o publish/shelly-cli
dotnet publish Shelly-CLI/Shelly-CLI.csproj -c Release -o publish/shelly-notifications
```

alternatively, you can run

```bash
sudo ./local-install.sh
```

This will build and perform the functions of install.sh

The binary will be located in the `/opt/shelly` directory.

## Usage

Run the application from your terminal:

For ui:

```bash
shelly-ui
```

For cli:

```bash
shelly
```

Notifications will be started with the ui, or it can be configured to launch at startup using your systems startup
configuration to run:

```bash
shelly-notifications
```

## Shelly-CLI

Shelly also includes a command-line interface (`shelly-cli`) for users who prefer terminal-based package management. The
CLI provides the same core functionality as the UI but in a scriptable, terminal-friendly format.

### CLI Commands

#### Package Management

| Command              | Description                     |
|----------------------|---------------------------------|
| `sync`               | Synchronize package databases   |
| `list-installed`     | List all installed packages     |
| `list-available`     | List all available packages     |
| `list-updates`       | List packages that need updates |
| `install <packages>` | Install one or more packages    |
| `install-local`      | Install a local package file    |
| `remove <packages>`  | Remove one or more packages     |
| `update <packages>`  | Update one or more packages     |
| `upgrade`            | Perform a full system upgrade   |

#### Keyring Management (`keyring`)

| Command                      | Description                                             |
|------------------------------|---------------------------------------------------------|
| `keyring init`               | Initialize the pacman keyring                           |
| `keyring populate [keyring]` | Reload keys from keyrings in /usr/share/pacman/keyrings |
| `keyring recv <keys>`        | Receive keys from a keyserver                           |
| `keyring lsign <keys>`       | Locally sign the specified key(s)                       |
| `keyring list`               | List all keys in the keyring                            |
| `keyring refresh`            | Refresh keys from the keyserver                         |

#### AUR Management (`aur`)

| Command                  | Description                         |
|--------------------------|-------------------------------------|
| `aur search <query>`     | Search for AUR packages             |
| `aur list`               | List installed AUR packages         |
| `aur list-updates`       | List AUR packages that need updates |
| `aur install <packages>` | Install AUR packages                |
| `aur update <packages>`  | Update specific AUR packages        |
| `aur upgrade`            | Upgrade all AUR packages            |
| `aur remove <packages>`  | Remove AUR packages                 |

#### Flatpak Management (`flatpak`)

| Command                         | Description                    |
|---------------------------------|--------------------------------|
| `flatpak search <query>`        | Search flatpak                 |
| `flatpak list`                  | List installed flatpak apps    |
| `flatpak list-updates`          | List flatpak apps with updates |
| `flatpak install <apps>`        | Install flatpak app            |
| `flatpak update <apps>`         | Update flatpak app             |
| `flatpak uninstall <apps>`      | Remove flatpak app             |
| `flatpak run <app>`             | Run flatpak app                |
| `flatpak running`               | List running flatpak apps      |
| `flatpak search <app>`          | search flathub                 |
| `flatpak sync-remote-appstream` | Sync remote appstream          |
| `flatpak get-remote-appstream`  | Returns remote appstream json  |
| `flatpak upgrade`               | Upgrade all flatpak apps       |

#### Shelly Utility (`utility`)

| Command           | Description                   |
|-------------------|-------------------------------|
| `utility export`  | Export sync file              |
| `utility updates` | check for updates as non-root |

### CLI Configuration

Shelly-CLI uses a JSON configuration file to customize its behavior. On the first run, it automatically creates a
default configuration file at:

`~/.config/shelly/config.json`

#### Configuration Options

| Option             | Description                                                                                                                                                                                                                                                    |
|--------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `FileSizeDisplay`  | Controls how file sizes are displayed. <br> Possible values: "Bytes", "Megabytes", "Gigabytes". <br> Default: "Bytes"                                                                                                                                          |
| `DefaultExecution` | Determines which command is executed when `shelly` is run without any arguments (the default command). <br> Possible values: "UpgradeStandard", "UpgradeFlatpak", "UpgradeAur", "UpgradeAll", "Sync", "SyncForce", "ListInstalled". <br> Default: "UpgradeAll" |

#### Example `config.json`

```json
{
  "FileSizeDisplay": "Bytes",
  "DefaultExecution": "UpgradeAll"
}
```

### CLI Options

**Global options:**

- `--help` - Display help information
- `--version` - Display version information

**sync command:**

- `-f, --force` - Force synchronization even if databases are up to date

**install, remove, update commands:**

- `--no-confirm` - Skip confirmation prompt

**upgrade command:**

- `--no-confirm` - Skip confirmation prompt

### CLI Examples

```bash
# Synchronize package databases
shelly sync

# Force sync even if up to date
shelly sync --force

# List all installed packages
shelly list-installed

# List packages needing updates
shelly list-updates

# Install packages
shelly install firefox vim

# Install without confirmation
shelly install firefox --no-confirm

# Remove packages
shelly remove firefox

# Update specific packages
# This should not be done unless you know what you're doing
shelly update firefox vim

# Perform full system upgrade
# Preferred way to update your system
shelly upgrade

# System upgrade without confirmation
shelly upgrade --no-confirm
```

## Development

Shelly is structured into several components:

- **Shelly.Gtk**: The main GUI desktop application.
- **Shelly-CLI**: Command-line interface for terminal-based package management.
- **Shelly-Notifications**: Tray service to manage notifactions the Shelly-UI.
- **PackageManager**: The core logic library providing bindings and abstractions for `libalpm`.
- **PackageManager.Tests**: Comprehensive tests for the package management logic.

### Building for Development

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

## License

This project is licensed under the GPL-3.0 License – see the [LICENSE](LICENSE) file for details.


