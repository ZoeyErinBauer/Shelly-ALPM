# Fish shell completions for shelly
# Auto-generated from Shelly-CLI command structure

# Disable file completions by default
complete -c shelly -f

# --- Top-level subcommands ---
complete -c shelly -n __fish_use_subcommand -a version -d "Display the application version"
complete -c shelly -n __fish_use_subcommand -a sync -d "Synchronize package databases"
complete -c shelly -n __fish_use_subcommand -a list-installed -d "List all installed packages"
complete -c shelly -n __fish_use_subcommand -a list-available -d "List all available packages"
complete -c shelly -n __fish_use_subcommand -a list-updates -d "List packages that need updates"
complete -c shelly -n __fish_use_subcommand -a list-repos -d "List configured repositories in order"
complete -c shelly -n __fish_use_subcommand -a info -d "Display information about a package"
complete -c shelly -n __fish_use_subcommand -a install -d "Install one or more packages"
complete -c shelly -n __fish_use_subcommand -a install-local -d "Install a local package file (.xz, .gz, .zst)"
complete -c shelly -n __fish_use_subcommand -a remove -d "Remove one or more packages"
complete -c shelly -n __fish_use_subcommand -a update -d "Update one or more packages"
complete -c shelly -n __fish_use_subcommand -a upgrade -d "Perform a full system upgrade"
complete -c shelly -n __fish_use_subcommand -a downgrade -d "Downgrade a package"
complete -c shelly -n __fish_use_subcommand -a news -d "Shows Arch news you haven't seen before"
complete -c shelly -n __fish_use_subcommand -a purify -d "Find and remove corrupted packages"
complete -c shelly -n __fish_use_subcommand -a keyring -d "Manage pacman keyring"
complete -c shelly -n __fish_use_subcommand -a aur -d "Manage AUR packages"
complete -c shelly -n __fish_use_subcommand -a flatpak -d "Manage flatpak"
complete -c shelly -n __fish_use_subcommand -a appimage -d "Manage AppImage packages"
complete -c shelly -n __fish_use_subcommand -a config -d "Manage shelly configuration"
complete -c shelly -n __fish_use_subcommand -a utility -d "shelly utils"

# --- Global options (DefaultSettings) - available on most commands ---
# -j/--json and -y/--sync are inherited by commands using DefaultSettings

# --- sync (SyncSettings extends ForceSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from sync" -s f -l force -d "Force the operation"
complete -c shelly -n "__fish_seen_subcommand_from sync" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from sync" -s y -l sync -d "Synchronize package databases before the operation"

# --- list-installed (ListSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from list-installed" -s s -l sort -d "Sort by: name, size, popularity" -r -a "name size popularity"
complete -c shelly -n "__fish_seen_subcommand_from list-installed" -s o -l order -d "Sort order: ascending, descending" -r -a "ascending descending"
complete -c shelly -n "__fish_seen_subcommand_from list-installed" -s f -l filter -d "Filter packages by name" -r
complete -c shelly -n "__fish_seen_subcommand_from list-installed" -s p -l page -d "The page to render" -r
complete -c shelly -n "__fish_seen_subcommand_from list-installed" -s t -l take -d "Number of packages per page" -r
complete -c shelly -n "__fish_seen_subcommand_from list-installed" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from list-installed" -s y -l sync -d "Synchronize package databases before the operation"

# --- list-available (ListSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from list-available" -s s -l sort -d "Sort by: name, size, popularity" -r -a "name size popularity"
complete -c shelly -n "__fish_seen_subcommand_from list-available" -s o -l order -d "Sort order: ascending, descending" -r -a "ascending descending"
complete -c shelly -n "__fish_seen_subcommand_from list-available" -s f -l filter -d "Filter packages by name" -r
complete -c shelly -n "__fish_seen_subcommand_from list-available" -s p -l page -d "The page to render" -r
complete -c shelly -n "__fish_seen_subcommand_from list-available" -s t -l take -d "Number of packages per page" -r
complete -c shelly -n "__fish_seen_subcommand_from list-available" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from list-available" -s y -l sync -d "Synchronize package databases before the operation"

# --- list-updates (DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from list-updates" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from list-updates" -s y -l sync -d "Synchronize package databases before the operation"

# --- info (PackageInformationSettings extends PackageSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from info" -s i -l installed -d "Searches installed packages"
complete -c shelly -n "__fish_seen_subcommand_from info" -s r -l repository -d "Searches repository of available packages"
complete -c shelly -n "__fish_seen_subcommand_from info" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from info" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from info" -s y -l sync -d "Synchronize package databases before the operation"

# --- install (InstallPackageSettings extends PackageSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from install; and not __fish_seen_subcommand_from aur flatpak" -s o -l build-deps -d "Install build dependencies only"
complete -c shelly -n "__fish_seen_subcommand_from install; and not __fish_seen_subcommand_from aur flatpak" -s m -l make-deps -d "Install make dependencies only"
complete -c shelly -n "__fish_seen_subcommand_from install; and not __fish_seen_subcommand_from aur flatpak" -s d -l no-deps -d "Install without checking/installing dependencies"
complete -c shelly -n "__fish_seen_subcommand_from install; and not __fish_seen_subcommand_from aur flatpak" -s u -l upgrade -d "Triggers full system upgrade with install"
complete -c shelly -n "__fish_seen_subcommand_from install; and not __fish_seen_subcommand_from aur flatpak" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from install; and not __fish_seen_subcommand_from aur flatpak" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from install; and not __fish_seen_subcommand_from aur flatpak" -s y -l sync -d "Synchronize package databases before the operation"

# --- install-local (InstallLocalPackageSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from install-local" -s l -l location -d "Location of the .pkg.tar.gz(xz) to be installed" -r -F
complete -c shelly -n "__fish_seen_subcommand_from install-local" -s n -l no-confirm -d "Proceed without confirmation"

# --- install-appimage (Legacy, moved to appimage branch) ---
complete -c shelly -n "__fish_seen_subcommand_from install-appimage" -s l -l location -d "Location of the .AppImage to be installed" -r -F
complete -c shelly -n "__fish_seen_subcommand_from install-appimage" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from install-appimage" -s u -l update-url -d "Set the release URL for update checking" -r
complete -c shelly -n "__fish_seen_subcommand_from install-appimage" -s t -l type -d "Set the update type" -r -a "None StaticUrl GitHub GitLab Codeberg Forgejo"

# --- remove (RemovePackageSettings extends PackageSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from remove; and not __fish_seen_subcommand_from aur" -s c -l cascade -d "Remove dependent packages with no other uses"
complete -c shelly -n "__fish_seen_subcommand_from remove; and not __fish_seen_subcommand_from aur" -s i -l ripple -d "Remove packages that depend on the removed package"
complete -c shelly -n "__fish_seen_subcommand_from remove; and not __fish_seen_subcommand_from aur" -s r -l remove-config -d "Remove config files tied to the package (EXPERIMENTAL)"
complete -c shelly -n "__fish_seen_subcommand_from remove; and not __fish_seen_subcommand_from aur" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from remove; and not __fish_seen_subcommand_from aur" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from remove; and not __fish_seen_subcommand_from aur" -s y -l sync -d "Synchronize package databases before the operation"

# --- update (PackageSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from update; and not __fish_seen_subcommand_from aur flatpak" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from update; and not __fish_seen_subcommand_from aur flatpak" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from update; and not __fish_seen_subcommand_from aur flatpak" -s y -l sync -d "Synchronize package databases before the operation"

# --- upgrade (UpgradeSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from upgrade; and not __fish_seen_subcommand_from aur flatpak" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from upgrade; and not __fish_seen_subcommand_from aur flatpak" -s a -l all -d "Upgrade all sources (Standard, AUR, Flatpak)"
complete -c shelly -n "__fish_seen_subcommand_from upgrade; and not __fish_seen_subcommand_from aur flatpak" -s u -l aur -d "Upgrade AUR packages"
complete -c shelly -n "__fish_seen_subcommand_from upgrade; and not __fish_seen_subcommand_from aur flatpak" -s l -l flatpak -d "Upgrade Flatpak packages"
complete -c shelly -n "__fish_seen_subcommand_from upgrade; and not __fish_seen_subcommand_from aur flatpak" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from upgrade; and not __fish_seen_subcommand_from aur flatpak" -s y -l sync -d "Synchronize package databases before the operation"

# --- downgrade (DowngradePackageCommandSettings extends PackageSettings extends DefaultSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from downgrade" -s o -l oldest -d "Install the oldest matched version"
complete -c shelly -n "__fish_seen_subcommand_from downgrade" -s l -l latest -d "Install the newest matched version"
complete -c shelly -n "__fish_seen_subcommand_from downgrade" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from downgrade" -s j -l json -d "Output results in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from downgrade" -s y -l sync -d "Synchronize package databases before the operation"

# --- news (ArchNewsSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from news" -s a -l all -d "Show all Arch news"
# --- purify (CorruptedPackagesSettings) ---
complete -c shelly -n "__fish_seen_subcommand_from purify" -s n -l no-confirm -d "Skip confirmation prompts"
complete -c shelly -n "__fish_seen_subcommand_from purify" -s d -l dry-run -d "Show what would be removed without removing"

# =====================
# keyring subcommands
# =====================
complete -c shelly -n "__fish_seen_subcommand_from keyring; and not __fish_seen_subcommand_from init populate recv lsign list refresh" -a init -d "Initialize the pacman keyring"
complete -c shelly -n "__fish_seen_subcommand_from keyring; and not __fish_seen_subcommand_from init populate recv lsign list refresh" -a populate -d "Reload keys from keyrings"
complete -c shelly -n "__fish_seen_subcommand_from keyring; and not __fish_seen_subcommand_from init populate recv lsign list refresh" -a recv -d "Receive keys from a keyserver"
complete -c shelly -n "__fish_seen_subcommand_from keyring; and not __fish_seen_subcommand_from init populate recv lsign list refresh" -a lsign -d "Locally sign the specified key(s)"
complete -c shelly -n "__fish_seen_subcommand_from keyring; and not __fish_seen_subcommand_from init populate recv lsign list refresh" -a list -d "List all keys in the keyring"
complete -c shelly -n "__fish_seen_subcommand_from keyring; and not __fish_seen_subcommand_from init populate recv lsign list refresh" -a refresh -d "Refresh keys from the keyserver"

# keyring recv options
complete -c shelly -n "__fish_seen_subcommand_from keyring; and __fish_seen_subcommand_from recv" -l keyserver -d "Keyserver URL" -r

# =====================
# aur subcommands
# =====================
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a search -d "Search for AUR packages"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a list-installed -d "List installed AUR packages"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a list-updates -d "List AUR packages that need updates"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a install -d "Install AUR packages"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a install-version -d "Install a specific version of an AUR package by commit hash"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a update -d "Update specific AUR packages"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a upgrade -d "Upgrade all AUR packages"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a remove -d "Remove AUR packages"
complete -c shelly -n "__fish_seen_subcommand_from aur; and not __fish_seen_subcommand_from search list-installed list-updates install install-version update upgrade remove get-package-build" -a get-package-build -d "Get package build"

# aur list-installed options (ListSettings)
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-installed" -s s -l sort -d "Sort by: name, size, popularity" -r -a "name size popularity"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-installed" -s o -l order -d "Sort order" -r -a "ascending descending"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-installed" -s f -l filter -d "Filter packages by name" -r
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-installed" -s p -l page -d "Page to render" -r
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-installed" -s t -l take -d "Packages per page" -r
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-installed" -s j -l json -d "Output in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-installed" -s y -l sync -d "Sync databases first"

# aur list-updates options (ListSettings)
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-updates" -s s -l sort -d "Sort by: name, size, popularity" -r -a "name size popularity"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-updates" -s o -l order -d "Sort order" -r -a "ascending descending"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-updates" -s f -l filter -d "Filter packages by name" -r
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-updates" -s p -l page -d "Page to render" -r
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-updates" -s t -l take -d "Packages per page" -r
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-updates" -s j -l json -d "Output in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from list-updates" -s y -l sync -d "Sync databases first"

# aur install options (AurInstallSettings extends AurPackageSettings)
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from install" -s o -l build-deps -d "Install build dependencies only"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from install" -s m -l make-deps -d "Install make dependencies only"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from install" -s c -l chroot -d "Build in clean chroot environment"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from install" -l no-confirm -d "Proceed without confirmation"

# aur update options (AurPackageSettings)
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from update" -l no-confirm -d "Proceed without confirmation"

# aur upgrade options (AurUpgradeSettings)
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from upgrade" -l no-confirm -d "Proceed without confirmation"

# aur remove options (AurRemovePackageSettings extends AurPackageSettings)
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from remove" -s c -l cascade -d "Remove dependent packages with no other uses"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from remove" -s i -l ripple -d "Remove packages that depend on the removed package"
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from remove" -l no-confirm -d "Proceed without confirmation"

# aur get-package-build options (AurPackageSettings)
complete -c shelly -n "__fish_seen_subcommand_from aur; and __fish_seen_subcommand_from get-package-build" -l no-confirm -d "Proceed without confirmation"

# =====================
# flatpak subcommands
# =====================
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a install -d "Install flatpak app"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a update -d "Update flatpak app"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a list -d "List installed flatpak apps"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a list-updates -d "List flatpak updates"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a running -d "List running flatpak apps"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a uninstall -d "Remove flatpak app"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a run -d "Run flatpak app"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a kill -d "Kill running flatpak app"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a search -d "Search flatpak"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a sync-remote-appstream -d "Sync remote appstream"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a get-remote-appstream -d "Returns remote appstream json"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a upgrade -d "Upgrade all flatpak apps"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a list-remotes -d "Returns all remotes currently added"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a add-remotes -d "Adds a flatpak remote"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a remove-remotes -d "Removes a flatpak remote"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a install-ref-file -d "Installs flatpak app from ref file"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and not __fish_seen_subcommand_from install update list list-updates running uninstall run kill search sync-remote-appstream get-remote-appstream upgrade list-remotes add-remotes remove-remotes install-ref-file app-remote-info" -a app-remote-info -d "Get app remote info"

# flatpak install/update/run/kill options (FlatpakPackageSettings)
for cmd in install update run kill
    complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from $cmd" -l user -d "Install to user scope"
    complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from $cmd" -s r -l remote -d "Remote to install from" -r
    complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from $cmd" -s b -l branch -d "Branch to install" -r
    complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from $cmd" -l runtime -d "Install as a runtime"
    complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from $cmd" -l remove-unused -d "Remove unused dependencies"
end

# flatpak uninstall options (FlatpakRemoveSettings)
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from uninstall" -s r -l remove-unused -d "Remove unused dependencies"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from uninstall" -s c -l config -d "Remove flatpak config for removed app"

# flatpak search options (FlathubSearchSettings)
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from search" -s l -l limit -d "Max results per page" -r
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from search" -s p -l page -d "Page number" -r
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from search" -s j -l json -d "Output in JSON format"

# flatpak list/list-remotes options (DefaultSettings)
for cmd in list list-updates list-remotes
    complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from $cmd" -s j -l json -d "Output in JSON format"
    complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from $cmd" -s y -l sync -d "Sync databases first"
end

# flatpak add-remotes options (FlatpakRemoteSettings)
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from add-remotes" -s u -l remote-url -d "Remote URL" -r
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from add-remotes" -s s -l system -d "System-wide install"
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from add-remotes" -s g -l gpg-verify -d "GPG verification"

# flatpak remove-remotes options (FlatpakRemoveRemoteSettings)
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from remove-remotes" -s s -l system -d "System-wide"

# flatpak install-ref-file options (FlatpakRemoteRefFileInstallSettings)
complete -c shelly -n "__fish_seen_subcommand_from flatpak; and __fish_seen_subcommand_from install-ref-file" -s s -l system -d "System-wide install"

# =====================
# appimage subcommands
# =====================
complete -c shelly -n "__fish_seen_subcommand_from appimage; and not __fish_seen_subcommand_from list install remove list-updates upgrade configure-updates" -a list -d "List installed AppImages"
complete -c shelly -n "__fish_seen_subcommand_from appimage; and not __fish_seen_subcommand_from list install remove list-updates upgrade configure-updates" -a install -d "Install an AppImage"
complete -c shelly -n "__fish_seen_subcommand_from appimage; and not __fish_seen_subcommand_from list install remove list-updates upgrade configure-updates" -a remove -d "Remove an AppImage"
complete -c shelly -n "__fish_seen_subcommand_from appimage; and not __fish_seen_subcommand_from list install remove list-updates upgrade configure-updates" -a list-updates -d "Check for AppImage updates"
complete -c shelly -n "__fish_seen_subcommand_from appimage; and not __fish_seen_subcommand_from list install remove list-updates upgrade configure-updates" -a upgrade -d "Upgrade AppImages"
complete -c shelly -n "__fish_seen_subcommand_from appimage; and not __fish_seen_subcommand_from list install remove list-updates upgrade configure-updates" -a configure-updates -d "Configure update settings for an AppImage"

# appimage list options
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from list" -f

# appimage install options
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from install" -s l -l location -d "Location of the .AppImage to be installed" -r -F
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from install" -s n -l no-confirm -d "Proceed without confirmation"
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from install" -s u -l update-url -d "Set the release URL for update checking" -r
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from install" -s t -l type -d "Set the update type" -r -a "None StaticUrl GitHub GitLab Codeberg Forgejo"

# appimage remove options
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from remove" -s n -l name -d "Name of the AppImage to be removed" -r
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from remove" -s c -l no-confirm -d "Proceed without confirmation"

# appimage upgrade options
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from upgrade" -s n -l no-confirm -d "Proceed without confirmation"

# appimage configure-updates options
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from configure-updates" -s u -l update-url -d "Set the update URL" -r
complete -c shelly -n "__fish_seen_subcommand_from appimage; and __fish_seen_subcommand_from configure-updates" -s t -l type -d "Set the update type" -r -a "None StaticUrl GitHub GitLab Codeberg Forgejo"

# =====================
# config subcommands
# =====================
complete -c shelly -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from get set list reset parallel" -a get -d "Get a configuration value"
complete -c shelly -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from get set list reset parallel" -a set -d "Set a configuration value"
complete -c shelly -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from get set list reset parallel" -a list -d "List all configuration values"
complete -c shelly -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from get set list reset parallel" -a reset -d "Reset configuration to defaults"
complete -c shelly -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from get set list reset parallel" -a parallel -d "Sets parallel download count"

# =====================
# utility subcommands
# =====================
complete -c shelly -n "__fish_seen_subcommand_from utility; and not __fish_seen_subcommand_from export updates cache-clean" -a export -d "Export sync file"
complete -c shelly -n "__fish_seen_subcommand_from utility; and not __fish_seen_subcommand_from export updates cache-clean" -a updates -d "Check for updates as non-root user"
complete -c shelly -n "__fish_seen_subcommand_from utility; and not __fish_seen_subcommand_from export updates cache-clean" -a cache-clean -d "Clean the package cache"

# utility export options (ExportSettings)
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from export" -s o -l output -d "Output location" -r -F
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from export" -s n -l name -d "Name of the exported sync" -r

# utility updates options (CheckPackageUpdatesNonRootSettings extends ForceSettings extends DefaultSettings)
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from updates" -s a -l aur -d "Check AUR for updates"
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from updates" -s l -l flatpak -d "Check Flatpak for updates"
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from updates" -s f -l force -d "Force the operation"
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from updates" -s j -l json -d "Output in JSON format"
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from updates" -s y -l sync -d "Sync databases first"

# utility cache-clean options (CacheCleanSettings)
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from cache-clean" -s r -l remove -d "Removes all candidate entries"
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from cache-clean" -s k -l keep -d "Number of versions to keep" -r
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from cache-clean" -s u -l uninstalled -d "Target uninstalled packages"
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from cache-clean" -s d -l dry-run -d "Show what would be removed"
complete -c shelly -n "__fish_seen_subcommand_from utility; and __fish_seen_subcommand_from cache-clean" -s c -l cache-dir -d "Path to the cache directory" -r -F