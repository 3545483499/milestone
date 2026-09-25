#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p .build/module-cache
xcrun swiftc -module-cache-path "$PWD/.build/module-cache" -import-objc-header Sources/SQLite.h \
  Sources/Database.swift Tests/StorageTests.swift -lsqlite3 -o .build/storage-tests
.build/storage-tests
