# Maintainer: Zoey Bauer <zoey.erin.bauer@gmail.com>
# Maintainer: Caroline Snyder <hirpeng@gmail.com>
pkgname=shelly
pkgver=1.5.2
pkgrel=1
pkgdesc="Shelly: A Modern Arch Package Manager"
arch=('x86_64')
url="https://github.com/ZoeyErinBauer/Shelly-ALPM"
license=('GPL-3.0-only')
provides=('shelly')
depends=(
    'pacman'
    'sudo'
    'tar'
    'sh'
    'xdg-utils'
    'hicolor-icon-theme'
    'icu'
    'fontconfig'
    'freetype2'
    'harfbuzz'
    'libpng'
    'libx11'
    'libxcb'
    'libxcursor'
)
optdepends=(
    'flatpak'
    'glib2'
)
makedepends=('dotnet-sdk-10.0')

# Source tarball from GitHub release
source=("${pkgname}-${pkgver}.tar.gz::https://github.com/ZoeyErinBauer/Shelly-ALPM/archive/v${pkgver}.tar.gz")

sha256sums=('a00b159faa548a37663a552ab24159788cd2201ce7c717218e21567744b6a3a1')

build() {
  cd "$srcdir/Shelly-ALPM-${pkgver}"

  dotnet publish Shelly-CLI/Shelly-CLI.csproj -c Release -o out-cli --nologo -p:InstructionSet=${INSTRUCTIONS:=x86-64-v3}
  dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -r linux-x64 -o out --nologo -p:InstructionSet=${INSTRUCTIONS:=x86-64-v3}
  dotnet publish Shelly-Notifications/Shelly-Notifications.csproj -c Release -r linux-x64 -o out-notify --nologo -p:InstructionSet=${INSTRUCTIONS:=x86-64-v3}
}

package() {
  cd "$srcdir/Shelly-ALPM-${pkgver}"

  # Install Shelly-UI binary
  install -Dm755 out/Shelly-UI "$pkgdir/usr/bin/shelly-ui"

  # Install bundled native libraries (SkiaSharp and HarfBuzzSharp)
  install -Dm755 out/libSkiaSharp.so "$pkgdir/usr/lib/libSkiaSharp.so"
  install -Dm755 out/libHarfBuzzSharp.so "$pkgdir/usr/lib/libHarfBuzzSharp.so"

  # Install Shelly-Notifications binary
  install -Dm755 out-notify/Shelly-Notifications "$pkgdir/usr/bin/shelly-notifications"

  # Install Shelly-CLI binary
  install -Dm755 out-cli/shelly "$pkgdir/usr/bin/shelly"

  # Install desktop entry
  cat <<'EOF' | install -Dm644 /dev/stdin "$pkgdir/usr/share/applications/shelly.desktop"
[Desktop Entry]
Name=Shelly
Exec=/usr/bin/shelly-ui
Icon=shelly
Type=Application
Categories=System;Utility;
Terminal=false
EOF

  # Install icon
  install -Dm644 Shelly-UI/Assets/shellylogo.png "$pkgdir/usr/share/icons/hicolor/256x256/apps/shelly.png"
}
