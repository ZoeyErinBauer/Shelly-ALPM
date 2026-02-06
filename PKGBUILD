# Maintainer: Zoey Bauer <zoey.erin.bauer@gmail.com>
# Maintainer: Caroline Snyder
pkgname=shelly
pkgver=1.4.6
pkgrel=1
pkgdesc="Shelly: A Modern Arch Package Manager"
arch=('x86_64')
url="https://github.com/ZoeyErinBauer/Shelly-ALPM"
license=('GPL-3.0-only')
depends=('pacman')
makedepends=('dotnet-sdk-10.0')

# Source tarball from GitHub release
source=("${pkgname}-${pkgver}.tar.gz::https://github.com/ZoeyErinBauer/Shelly-ALPM/archive/v${pkgver}.tar.gz")

# SHA256 checksum fetched from GitHub release SHA256SUMS.txt
# To update, run: curl -sL "https://github.com/ZoeyErinBauer/Shelly-ALPM/releases/download/v${pkgver}/SHA256SUMS.txt" | grep "v${pkgver}.tar.gz" | cut -d' ' -f1
_source_sha256=$(curl -sL "https://github.com/ZoeyErinBauer/Shelly-ALPM/releases/download/v${pkgver}/SHA256SUMS.txt" 2>/dev/null | grep "v${pkgver}.tar.gz" | cut -d' ' -f1)
sha256sums=("${_source_sha256:-SKIP}")

# Track which makedepends were installed before building
_makedepends_installed_before=()

prepare() {
  # Record which makedepends are already installed before we start
  for dep in "${makedepends[@]}"; do
    if pacman -Qi "$dep" &>/dev/null; then
      _makedepends_installed_before+=("$dep")
    fi
  done
  # Save to a file so it persists across function calls
  printf '%s\n' "${_makedepends_installed_before[@]}" > "$srcdir/.makedepends_installed_before"
  cd "$srcdir/Shelly-ALPM-${pkgver}"
}

build() {
  cd "$srcdir/Shelly-ALPM-${pkgver}"
  #suppressing warnings that have no bearing on the actual project and server nothing but to add confusion to the package build.
  dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -o out --nologo -v q /p:WarningLevel=0 /p:NoWarn=IL3000%3BIL3001%3BIL3002%3BIL3003%3BIL3050%3BIL3051%3BIL3053 /p:SuppressTrimAnalysisWarnings=true /p:EnableTrimAnalyzer=false /p:EnableSingleFileAnalyzer=false /p:EnableAotAnalyzer=false
  dotnet publish Shelly-CLI/Shelly-CLI.csproj -c Release -o out-cli --nologo -v q /p:WarningLevel=0 /p:NoWarn=IL3000%3BIL3001%3BIL3002%3BIL3003%3BIL3050%3BIL3051%3BIL3053 /p:SuppressTrimAnalysisWarnings=true /p:EnableTrimAnalyzer=false /p:EnableSingleFileAnalyzer=false /p:EnableAotAnalyzer=false
}

package() {
  cd "$srcdir/Shelly-ALPM-${pkgver}"
  
  # Install Shelly-UI binary
  install -Dm755 out/Shelly-UI "$pkgdir/usr/bin/shelly-ui"
  
  # Install bundled native libraries (SkiaSharp and HarfBuzzSharp)
  # These are bundled with the .NET application and will be removed with the package
  install -Dm755 out/libSkiaSharp.so "$pkgdir/usr/lib/libSkiaSharp.so"
  install -Dm755 out/libHarfBuzzSharp.so "$pkgdir/usr/lib/libHarfBuzzSharp.so"
  
  # Install Shelly-CLI binary
  install -Dm755 out-cli/shelly "$pkgdir/usr/bin/shelly"
  
  # Install desktop entry
  echo "[Desktop Entry]
        Name=Shelly
        Exec=/usr/bin/shelly-ui
        Icon=shelly
        Type=Application
        Categories=System;Utility;
        Terminal=false" | install -Dm644 /dev/stdin "$pkgdir/usr/share/applications/shelly.desktop"

  # Install icon
  install -Dm644 Shelly-UI/Assets/shellylogo.png "$pkgdir/usr/share/icons/hicolor/256x256/apps/shelly.png"

  # Install license
  install -Dm644 LICENSE "$pkgdir/usr/share/licenses/$pkgname/LICENSE"

  # Remove makedepends that weren't installed before building
  if [[ -f "$srcdir/.makedepends_installed_before" ]]; then
    local _installed_before
    mapfile -t _installed_before < "$srcdir/.makedepends_installed_before"
    for dep in "${makedepends[@]}"; do
      # Check if this dep was NOT in the list of previously installed packages
      local _was_installed=0
      for installed in "${_installed_before[@]}"; do
        if [[ "$dep" == "$installed" ]]; then
          _was_installed=1
          break
        fi
      done
      # If it wasn't installed before and is currently installed, remove it
      if [[ $_was_installed -eq 0 ]] && pacman -Qi "$dep" &>/dev/null; then
        echo "Removing makedepend '$dep' (was not installed before build)..."
        sudo pacman -Rns --noconfirm "$dep" 2>/dev/null || true
      fi
    done
    rm -f "$srcdir/.makedepends_installed_before"
  fi
}
