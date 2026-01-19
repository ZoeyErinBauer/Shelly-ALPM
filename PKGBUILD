# Maintainer: Zoey <zoey@example.com>
pkgname=shelly-ui
pkgver=1.0.3.alpha4
pkgrel=1
pkgdesc="Shelly is a visual arch linux package manager"
arch=('x86_64')
url="https://github.com/ZoeyErinBauer/Shelly-ALPM"
license=('GPL-3.0-only')
depends=('pacman')
makedepends=('dotnet-sdk-10.0')
source=("${pkgname}::git+https://github.com/ZoeyErinBauer/Shelly-ALPM.git")
sha256sums=('SKIP')

prepare() {
  cd "$srcdir/${pkgname}"
  # Ensure the submodules or nested projects are handled if needed, 
  # but here it's just a single repo.
}

build() {
  cd "$srcdir/${pkgname}"
  dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -o out
  dotnet publish Shelly-CLI/Shelly-CLI.csproj -c Release -o out-cli
}

package() {
  cd "$srcdir/${pkgname}"
  
  # Install Shelly-UI binary
  install -Dm755 out/Shelly-UI "$pkgdir/usr/bin/shelly-ui"
  
  # Install native libraries (SkiaSharp and HarfBuzzSharp) alongside Shelly-UI
  install -Dm755 out/libSkiaSharp.so "$pkgdir/usr/bin/libSkiaSharp.so"
  install -Dm755 out/libHarfBuzzSharp.so "$pkgdir/usr/bin/libHarfBuzzSharp.so"
  
  # Install Shelly-CLI binary
  install -Dm755 out-cli/Shelly-CLI "$pkgdir/usr/bin/shelly-cli"
  
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
}
