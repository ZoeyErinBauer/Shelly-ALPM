![shelly_banner.png](shelly_banner.png)

![Shelly Wiki](https://img.shields.io/badge/Shelly-Wiki-blue)

<p align="center">
  <img src="demo_part1.gif" width="32%">
  <img src="demo_part2.gif" width="32%">
  <img src="demo_part3.gif" width="32%">
</p>

### About

Shelly is a modern reimagination of the Arch Linux package manager, designed to be a more intuitive and user-friendly
alternative to `pacman` and `octopi`. Unlike other Arch package managers, Shelly offers a modern, visual interface with
a focus on
user experience and ease of use; It **IS NOT** built as a `pacman` wrapper or front-end. It is a complete reimagination
of how a user
interacts with their Arch Linux system, providing a more streamlined and intuitive experience.

## Quick Install
Recommended install for Shelly with a single command:

```bash
curl -fsSL https://raw.githubusercontent.com/ZoeyErinBauer/Shelly-ALPM/master/web-install.sh | sudo bash
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

**Note:** -bin packages will be made available soon and will allow you to install without needing to run makepkg every
update.

## Uninstall
To uninstall the quick installer version is you wish to switch to the AUR managed version:

```bash
curl -fsSL https://raw.githubusercontent.com/ZoeyErinBauer/Shelly-ALPM/master/uninstall.sh | sudo bash
```


## Features

- **Modern-CLI**: Provides a command-line interface for advanced users and automation, with a focus on ease of use.
- **Native Arch Integration**: Directly interacts with `libalpm` for accurate and fast package management.
- **Modern UI Framework**: Built using [Avalonia UI](https://avaloniaui.net/), ensuring a modern and responsive
  user interface.
- **Package Management**: Supports searching for, installing, updating, and removing packages.
- **Repository Management**: Synchronizes with official repositories to keep package lists up to date.
- **AUR Support**: Integration with the Arch User Repository for a wider range of software.
- **Flatpak Support**: Manage Flatpak applications alongside native packages. (Currently only in cli)

## Roadmap

Upcoming features and development targets:

- **Repository Modification**: Allow modification of supported repositories (First future release).
- **Package Grouping**: Group related packages for easier management.
- **Flatpak Support**: Manage Flatpak applications alongside native packages.
- **Desktop Integration**: Enhance integration with the desktop environment for seamless experience. Targeting KDE
  Plasma and Gnome initially.
- **Shelly Sync**: Multi-system sync lists that keep packages together across computers

## Prerequisites

- **Arch Linux** (or an Arch-based distribution)
- **.NET 10.0 Runtime** (for running only if installed from non *-bin aur package)
- **.NET 10.0 SDK** (for building)
- **libalpm** (provided by `pacman`)

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
dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -o publish/shelly-ui
dotnet publish Shelly-CLI/Shelly-CLI.csproj -C Release -o publish/shelly-cli
```

alternatively you can run

```bash
sudo ./local-install.sh
```

This will build and perform the functions of install.sh

The binary will be located in the `/publish/shelly-ui/` directory.

## Usage

Run the application from your terminal or application launcher:

```bash
shelly-ui
```

In it's install location or by opening it from your applications menu.

## Shelly-CLI

Shelly also includes a command-line interface (`shelly-cli`) for users who prefer terminal-based package management. The
CLI provides the same core functionality as the UI but in a scriptable, terminal-friendly format.

### CLI Commands

#### Package Management

| Command              | Description                   |
|----------------------|-------------------------------|
| `sync`               | Synchronize package databases |
| `list-installed`     | List all installed packages   |
| `list-available`     | List all available packages   |
| `list-updates`       | List packages that need updates |
| `install <packages>` | Install one or more packages  |
| `remove <packages>`  | Remove one or more packages   |
| `update <packages>`  | Update one or more packages   |
| `upgrade`            | Perform a full system upgrade |

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

| Command                    | Description                    |
|----------------------------|--------------------------------|
| `flatpak search <query>`   | Search flatpak                 |
| `flatpak list`             | List installed flatpak apps    |
| `flatpak list-updates`     | List flatpak apps with updates |
| `flatpak install <apps>`   | Install flatpak app            |
| `flatpak update <apps>`    | Update flatpak app             |
| `flatpak uninstall <apps>` | Remove flatpak app             |
| `flatpak run <app>`        | Run flatpak app                |
| `flatpak running`          | List running flatpak apps      |
| `flatpak kill <app>`       | Kill running flatpak app       |

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

- **Shelly-UI**: The main Avalonia-based desktop application.
- **Shelly-CLI**: Command-line interface for terminal-based package management.
- **PackageManager**: The core logic library providing bindings and abstractions for `libalpm`.
- **PackageManager.Tests**: Comprehensive tests for the package management logic.
- **Shelly-UI.Tests**: Unit tests for the Avalonia UI components.

### Building for Development

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

## License

This project is licensed under the GPL-3.0 License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

Shelly is in active development. It comes with no guarantees and may contain bugs, however if you experience issues
please report them by opening an issue on this page and we will do our best to resolve the issues.

## Inspirations
- Some UI controls were inspired by [Aura.UI](https://github.com/PieroCastillo/Aura.UI)