# Shelly User Guide

Shelly is a visual package manager for Arch Linux that makes installing, updating, and removing packages simple and intuitive.

## Installation

### Quick Install (Recommended)

Install Shelly with a single command:

```bash
curl -fsSL https://raw.githubusercontent.com/ZoeyErinBauer/Shelly-ALPM/master/web-install.sh | sudo bash
```

### Manual Installation

1. Download the latest release `.tar.gz` from the [Releases page](https://github.com/ZoeyErinBauer/Shelly-ALPM/releases)
2. Extract the archive and navigate to the directory
3. Run the installer:

```bash
chmod +x install.sh
sudo bash install.sh
```

Shelly will now be available in your application menu.

---

## Using Shelly UI

### Home Page

The home page displays important Arch Linux news. Always check this before updating your system, as some updates may require manual intervention.

> **Tip:** We display the 10 most recent news items. For more details, visit https://archlinux.org/news/

### Installing Packages

1. Click on the **Packages** icon in the navigation menu
2. Select **Install Packages** from the dropdown

![HomePagePackagesHighlighted.png](HomePagePackagesHighlighted.png)
![InstallPackagesHighlighted.png](InstallPackagesHighlighted.png)

3. Search for the packages you want to install
4. Check the boxes next to the packages you want
5. Click the **download arrow** in the bottom right to install

![InstallGuide.png](InstallGuide.png)

> **Tip:** Right-click on any package to view more information, including the package's website.

### Updating Packages

1. Click the **Update** icon to open the update page

![UpdateHighlighted.png](UpdateHighlighted.png)

**Update specific packages:**
- Check the boxes next to the packages you want to update
- Click the **update arrow** in the bottom right

![UpdateOne.png](UpdateOne.png)

**Update all packages:**
- Click **Toggle all** to select all packages
- Click the **update arrow** in the bottom right

![UpdateMany.png](UpdateMany.png)

### Removing Packages

1. Click the **Remove** icon to open the remove page

![Remove.png](Remove.png)

2. Search for packages you want to remove
3. Check the boxes next to the packages (you can select multiple)
4. Click the **trashcan icon** in the bottom right to remove

![RemoveOne.png](RemoveOne.png)

---

## AUR (Arch User Repository) Support

Shelly supports installing, updating, and removing packages from the AUR.

### Installing AUR Packages

1. Click on the **Packages** icon in the navigation menu
2. Select **AUR Packages** from the dropdown
3. Search for the AUR package you want
4. Select the package and click the download icon to install

> **Note:** AUR packages are built from source and may take longer to install than official repository packages.

### Updating AUR Packages

1. Navigate to **Update** → **AUR Updates**
2. Select the packages you want to update or use **Toggle all**
3. Click the update arrow to begin updating

### Removing AUR Packages

1. Navigate to **Remove** → **AUR Packages**
2. Search for and select the AUR packages to remove
3. Click the trashcan icon to remove

---

## Settings

Access settings by clicking the **gear icon**. Available options include:

- Theme customization
- Default view preferences
- Other display settings

---

## Command Line Interface (CLI)

Shelly also includes `shelly-cli` for terminal-based package management.

### Basic Commands

| Command | Description |
|---------|-------------|
| `shelly-cli sync` | Synchronize package databases |
| `shelly-cli list-installed` | List all installed packages |
| `shelly-cli list-available` | List all available packages |
| `shelly-cli list-updates` | List packages with available updates |
| `shelly-cli install <pkg>` | Install a package |
| `shelly-cli remove <pkg>` | Remove a package |
| `shelly-cli update <pkg>` | Update a specific package |
| `shelly-cli upgrade` | Upgrade all packages |

### Examples

```bash
# Sync databases and upgrade all packages
shelly-cli sync
shelly-cli upgrade

# Install multiple packages
shelly-cli install firefox vlc

# Remove a package (skip confirmation)
shelly-cli remove -y package-name
```

---

## Troubleshooting

### Common Issues

**Shelly won't start:**
- Ensure you have the .NET 10.0 Runtime installed
- Check that `libalpm` is available (provided by `pacman`)

**Package database out of sync:**
- Click the sync button or run `shelly-cli sync`

**Permission errors:**
- Shelly requires root privileges for package operations
- Enter your password when prompted

---

## Getting Help

- **GitHub Issues:** [Report bugs or request features](https://github.com/ZoeyErinBauer/Shelly-ALPM/issues)
- **Wiki:** Check the [Home page](Home.md) for more information
