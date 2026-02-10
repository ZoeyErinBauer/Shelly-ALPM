# Maintainer: Zoey Bauer <zoey.erin.bauer@gmail.com>
# Maintainer: Caroline Snyder
pkgname=shelly
pkgver=1.4.8
pkgrel=1
pkgdesc="Shelly: A Modern Arch Package Manager"
arch=('x86_64')
url="https://github.com/ZoeyErinBauer/Shelly-ALPM"
license=('GPL-3.0-only')
depends=(
    'brotli'
    'bzip2'
    'expat'
    'fontconfig'
    'freetype2'
    'glibc'
    'graphite'
    'harfbuzz'
    'hicolor-icon-theme'
    'icu'
    'libdisplay-info'
    'libdrm'
    'libedit'
    'libice'
    'libpng'
    'libtirpc'
    'libx11'
    'libxau'
    'libxcb'
    'libxcrypt'
    'libxcursor'
    'libxdmcp'
    'libxext'
    'libxfixes'
    'libxi'
    'libxrandr'
    'libxrender'
    'libxshmfence'
    'ncurses'
    'pacman'
    'pcre2'
    'sh'
    'sudo'
    'tar'
    'util-linux'
    'vulkan-driver'
    'xcb-util-keysyms'
    'xdg-utils'
    'zlib'
)
optdepends=(
    'flatpak'
)
makedepends=('dotnet-sdk-10.0')

# Source tarball from GitHub release
source=("${pkgname}-${pkgver}.tar.gz::https://github.com/ZoeyErinBauer/Shelly-ALPM/archive/v${pkgver}.tar.gz")

sha256sums=('b8a284a2faa90155c934162b87211eb834aa7877023844ea94668c3575c2b7d4')

build() {
  cd "$srcdir/Shelly-ALPM-${pkgver}"

  #suppressing warnings that have no bearing on the actual project and server nothing but to add confusion to the package build.
  dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -o out --nologo -v q /p:WarningLevel=0 /p:NoWarn=IL3000%3BIL3001%3BIL3002%3BIL3003%3BIL3050%3BIL3051%3BIL3053 /p:SuppressTrimAnalysisWarnings=true /p:EnableTrimAnalyzer=false /p:EnableSingleFileAnalyzer=false /p:EnableAotAnalyzer=false
  dotnet publish Shelly-CLI/Shelly-CLI.csproj -c Release -o out-cli --nologo -v q /p:WarningLevel=0 /p:NoWarn=IL3000%3BIL3001%3BIL3002%3BIL3003%3BIL3050%3BIL3051%3BIL3053 /p:SuppressTrimAnalysisWarnings=true /p:EnableTrimAnalyzer=false /p:EnableSingleFileAnalyzer=false /p:EnableAotAnalyzer=false
}

package() {
  cd "$srcdir/Shelly-ALPM-${pkgver}"

  # Install Shelly-UI binary
  install -Dm755 out/Shelly-UI "$pkgdir/usr/lib/shelly/Shelly-UI"

  # Install bundled native libraries (SkiaSharp and HarfBuzzSharp)
  install -Dm755 out/libSkiaSharp.so "$pkgdir/usr/lib/shelly/libSkiaSharp.so"
  install -Dm755 out/libHarfBuzzSharp.so "$pkgdir/usr/lib/shelly/libHarfBuzzSharp.so"

  # Install Shelly-CLI binary
  install -Dm755 out-cli/shelly "$pkgdir/usr/lib/shelly/shelly"

  # Install shelly launch wrapper
  cat <<'EOF' | install -Dm755 /dev/stdin "$pkgdir/usr/bin/shelly"
#!/bin/sh
exec /usr/lib/shelly/shelly "$@"
EOF

  # Install shelly-ui launch wrapper
  cat <<'EOF' | install -Dm755 /dev/stdin "$pkgdir/usr/bin/shelly-ui"
#!/bin/sh
exec /usr/lib/shelly/Shelly-UI "$@"
EOF

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
