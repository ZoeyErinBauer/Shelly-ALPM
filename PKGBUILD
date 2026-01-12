# Maintainer: Zoey <zoey@example.com>
pkgname=shelly-ui
pkgver=1.0.0
pkgrel=1
pkgdesc="Shelly-UI - exclusively for Arch Linux"
arch=('x86_64')
url="https://github.com/zoey/Shelly-UI"
license=('GPL-3.0-only')
depends=('dotnet-runtime-10.0')
makedepends=('dotnet-sdk-10.0')
source=("shelly-ui::git+https://github.com/zoey/Shelly-UI.git")
sha256sums=('SKIP')

build() {
  cd "$srcdir/shelly-ui"
  dotnet publish Shelly-UI/Shelly-UI.csproj -c Release -o out /p:PublishSingleFile=true /p:SelfContained=false
}

package() {
  cd "$srcdir/shelly-ui"
  install -Dm755 out/Shelly-UI "$pkgdir/usr/bin/shelly-ui"
}
