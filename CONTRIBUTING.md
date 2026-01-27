# Contributing to Shelly

Thank you for your interest in contributing to Shelly! This guide explains the project structure and how the components
interact.

## Project Structure

Shelly is organized into several interconnected projects:

### Core Components

| Project              | Description                                                                                                    |
|----------------------|----------------------------------------------------------------------------------------------------------------|
| **Shelly-UI**        | The main Avalonia-based desktop application providing a graphical interface for package management             |
| **Shelly-CLI**       | Command-line interface for terminal-based package management, also used by Shelly-UI for privileged operations |
| **PackageManager**   | Core library containing libalpm bindings, AUR integration, and Flatpak support                                 |
| **Shelly.Utilities** | Shared utility classes and extensions used across projects                                                     |

### Test Projects

| Project                  | Description                                               |
|--------------------------|-----------------------------------------------------------|
| **PackageManager.Tests** | Tests for ALPM bindings, AUR functionality, and utilities |
| **Shelly-UI.Tests**      | Tests for UI services, ViewModels, and Views              |

## How Components Interact

```
┌─────────────────────────────────────────────────────────────┐
│                        User                                 │
└─────────────────┬───────────────────────┬───────────────────┘
                  │                       │
                  ▼                       ▼
         ┌───────────────┐       ┌───────────────┐
         │   Shelly-UI   │──────▶│  Shelly-CLI   │
         │  (Avalonia)   │ sudo  │  (Terminal)   │
         └───────┬───────┘       └───────┬───────┘
                 │                       │
                 │    ┌──────────────────┘
                 │    │
                 ▼    ▼
         ┌───────────────┐
         │ PackageManager │
         │    (Core)      │
         └───────┬───────┘
                 │
    ┌────────────┼────────────┐
    ▼            ▼            ▼
┌────────┐  ┌────────┐  ┌──────────┐
│ libalpm│  │  AUR   │  │ Flatpak  │
│ (arch) │  │  API   │  │  CLI     │
└────────┘  └────────┘  └──────────┘
```

### Key Interactions

1. **Shelly-UI ↔ Shelly-CLI**: The UI launches the CLI via `sudo` with `--ui-mode` flag for privileged operations (
   install, remove, upgrade). The CLI outputs structured messages that the UI parses for progress updates.
    - **Note**: Goal is to eventually remove direct access to package managers from the UI and have all operations
      performed via the CLI.
2. **Both UIs → PackageManager**: Both Shelly-UI and Shelly-CLI use the PackageManager library for:
    - ALPM operations (via `AlpmManager`)
    - AUR package management (via `AurManager`)
    - Flatpak operations (via `FlatpakManager`)

3. **PackageManager → System**:
    - Directly interfaces with `libalpm` for native package operations
    - Calls AUR API for package searches and metadata
    - Invokes Flatpak CLI for Flatpak management

## Directory Structure

```
Shelly-ALPM/
├── PackageManager/           # Core library
│   ├── Alpm/                 # libalpm bindings and management
│   ├── Aur/                  # AUR integration
│   │   └── Models/           # AUR data models
│   ├── Flatpak/              # Flatpak management
│   ├── Models/               # Shared data models
│   ├── User/                 # User-related functionality
│   └── Utilities/            # Helper utilities
├── Shelly-CLI/               # Command-line interface
│   └── Commands/             # CLI command implementations
├── Shelly-UI/                # Desktop application
│   ├── Assets/               # Images, icons, resources
│   ├── BaseClasses/          # Base ViewModels and classes
│   ├── Converters/           # XAML value converters
│   ├── CustomControls/       # Custom Avalonia controls
│   ├── Enums/                # Enumeration types
│   ├── Models/               # UI-specific models
│   ├── Services/             # Application services
│   │   └── AppCache/         # Caching services
│   ├── ViewModels/           # MVVM ViewModels
│   │   └── AUR/              # AUR-specific ViewModels
│   └── Views/                # XAML views
│       └── AUR/              # AUR-specific views
├── Shelly.Utilities/         # Shared utilities
│   ├── Extensions/           # Extension methods
│   └── System/               # System utilities
├── Shelly.Protocol/          # Communication protocol
├── Shelly.Service/           # Privileged service
└── wiki/                     # Documentation images
```

## Building the Project

```bash
# Build all projects
dotnet build

# Build specific project
dotnet build Shelly-UI/Shelly-UI.csproj
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test PackageManager.Tests/PackageManager.Tests.csproj
dotnet test Shelly-UI.Tests/Shelly-UI.Tests.csproj
```

## Development Guidelines

1. **Code Style**: Follow the existing code style in each project
2. **Testing**: Add tests for new functionality in the appropriate test project
3. **Documentation**: Update relevant documentation when adding features
4. **Commits**: Use clear, descriptive commit messages

## Getting Help

If you have questions or need help, please open an issue on the GitHub repository or reach out on Discord to zoeybear.
