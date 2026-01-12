# Shelly: A Visual Arch Package Manager

Shelly is a modern, visual package manager for Arch Linux, built with .NET 10 and Avalonia. It provides a user-friendly interface for managing your Arch Linux system's packages by leveraging the power of `libalpm`.

## Features

- **Native Arch Integration**: Directly interacts with `libalpm` for accurate and fast package management.
- **Cross-Platform UI Framework**: Built using [Avalonia UI](https://avaloniaui.net/), ensuring a modern and responsive user interface.
- **Package Management**: Supports searching for, installing, updating, and removing packages.
- **Repository Management**: Synchronizes with official repositories to keep package lists up to date.

## Roadmap

Upcoming features and development targets:

- **Repository Modification**: Allow modification of supported repositories (First future release).
- **AUR Support**: Integration with the Arch User Repository for a wider range of software.
- **Package Grouping**: Group related packages for easier management.
- **Flatpak Support**: Manage Flatpak applications alongside native packages.
- **Snapd Support**: Support for Snap packages.

## Prerequisites

- **Arch Linux** (or an Arch-based distribution)
- **.NET 10.0 Runtime** (for running)
- **.NET 10.0 SDK** (for building)
- **libalpm** (provided by `pacman`)

## Installation

### Using PKGBUILD

Since Shelly is designed for Arch Linux, you can build and install it using the provided `PKGBUILD`:

```bash
git clone https://github.com/zoey/Shelly-UI.git
cd Shelly-UI
makepkg -si
```

### Manual Build

You can also build the project manually using the .NET CLI:

```bash
dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -o out /p:PublishSingleFile=true /p:SelfContained=false
```

The binary will be located in the `out/` directory.

## Usage

Run the application from your terminal or application launcher:

```bash
shelly-ui
```

*Note: Shelly may require root privileges for certain package management operations.*

## Development

Shelly is structured into several components:

- **Shelly-UI**: The main Avalonia-based desktop application.
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

This project is licensed under the GPL-3.0 License - see the [PKGBUILD](PKGBUILD) or project files for details.

## Disclaimer

Shelly is currently in early development. Use it with caution on your Arch Linux system.
