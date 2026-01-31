# Maintainer: Zoey <zoey@example.com>
pkgname=shelly-ui
pkgver=1.0.3.alpha4
pkgrel=1
pkgdesc="Shelly an alternative to pacman implemented on top of libalpm directly"
arch=('x86_64')
url="https://github.com/ZoeyErinBauer/Shelly-ALPM"
license=('GPL-2.0-only')
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
  dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -o out --nologo -v q /p:WarningLevel=0
  dotnet publish Shelly-CLI/Shelly-CLI.csproj -c Release -o out-cli --nologo -v q /p:WarningLevel=0
}

package() {
  cd "$srcdir/${pkgname}"
  
  # Prepare installation directory
  mkdir -p "$pkgdir/opt/shelly"
  
  # Install Shelly-UI binary and libraries to /opt/shelly
  install -Dm755 out/Shelly-UI "$pkgdir/opt/shelly/shelly-ui"
  install -m755 out/*.so -t "$pkgdir/opt/shelly/"
  
  # Install Shelly-CLI binary to /opt/shelly
  install -Dm755 out-cli/shelly "$pkgdir/opt/shelly/shelly"
  
  # Create symlinks in /usr/bin
  mkdir -p "$pkgdir/usr/bin"
  ln -s "/opt/shelly/shelly-ui" "$pkgdir/usr/bin/shelly-ui"
  ln -s "/opt/shelly/shelly" "$pkgdir/usr/bin/shelly"
  
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
}
