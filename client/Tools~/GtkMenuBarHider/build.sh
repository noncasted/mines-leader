#!/usr/bin/env bash
# Builds the native editor plugin used by Internal.CompactMenuBar (Linux editor only).
# The binary is committed, so rebuild only after changing gtk_menubar_hider.c.
set -euo pipefail

cd "$(dirname "$0")"
out="../../Assets/Common/Internal/Editor/Tools/CompactMenuBar/libgtk-menubar-hider.so"

gcc -shared -fPIC -O2 -Wall -Wextra -Wno-unused-parameter \
    -o "$out" gtk_menubar_hider.c \
    $(pkg-config --cflags --libs gtk+-3.0)

echo "Built: $(realpath "$out")"
