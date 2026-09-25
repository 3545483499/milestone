#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
APP="$PWD/dist/里程碑.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$PWD/.build/module-cache"
ICONSET="$PWD/.build/AppIcon.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" Assets/logo-y.png --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" Assets/logo-y.png --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
xcrun swiftc -O -target arm64-apple-macosx14.0 -module-cache-path "$PWD/.build/module-cache" \
  -import-objc-header Sources/SQLite.h Sources/Database.swift Sources/App.swift \
  -framework SwiftUI -framework AppKit -lsqlite3 -o "$APP/Contents/MacOS/里程碑"
if [ -f "$APP/Contents/MacOS/Milestone" ]; then
  rm "$APP/Contents/MacOS/Milestone"
fi
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>里程碑</string>
<key>CFBundleIdentifier</key><string>local.milestone.canvas</string>
<key>CFBundleName</key><string>里程碑</string>
<key>CFBundleDisplayName</key><string>里程碑</string>
<key>CFBundleIconFile</key><string>AppIcon</string>
<key>CFBundleDevelopmentRegion</key><string>zh_CN</string>
<key>CFBundleLocalizations</key><array><string>zh_CN</string></array>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.0.3</string>
<key>CFBundleVersion</key><string>4</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSPrincipalClass</key><string>NSApplication</string>
</dict></plist>
PLIST
codesign --force --sign - "$APP"
echo "$APP"
