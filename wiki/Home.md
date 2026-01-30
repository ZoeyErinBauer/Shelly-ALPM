# Shelly Wiki

Welcome to the Shelly Wiki!

## About

Shelly is a modern reimagination of the Arch Linux package manager, designed to be a more intuitive and user-friendly alternative to `pacman` and `octopi`. Unlike other Arch package managers, Shelly offers a modern, visual interface with a focus on user experience and ease of use. It **IS NOT** built as a `pacman` wrapper or front-end — it is a complete reimagination of how a user interacts with their Arch Linux system.

## Features

- **Modern UI**: Built using [Avalonia UI](https://avaloniaui.net/) for a modern and responsive user interface.
- **Modern CLI**: Command-line interface for advanced users and automation.
- **Native Arch Integration**: Directly interacts with `libalpm` for accurate and fast package management.
- **Package Management**: Search, install, update, and remove packages with ease.
- **AUR Support**: Install and manage packages from the Arch User Repository.
- **Repository Management**: Synchronizes with official repositories to keep package lists up to date.

## Quick Install

Install Shelly with a single command:

```bash
curl -fsSL https://raw.githubusercontent.com/ZoeyErinBauer/Shelly-ALPM/master/web-install.sh | sudo bash
```

## Prerequisites

- **Arch Linux** (or an Arch-based distribution)
- **.NET 10.0 Runtime** (for running only if installed from non -bin AUR package)
- **.NET 10.0 SDK** (for building)
- **libalpm** (provided by `pacman`)

## Project Structure

- **Shelly-UI**: The main Avalonia-based desktop application.
- **Shelly-CLI**: Command-line interface for terminal-based package management.
- **PackageManager**: The core logic library providing bindings and abstractions for `libalpm`.

## Getting Started

See the [User Guide](UserGuide.md) for detailed instructions on using Shelly.

## License

This project is licensed under the GPL-2.0 License.
