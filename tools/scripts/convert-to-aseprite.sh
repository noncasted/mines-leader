#!/usr/bin/env bash
set -euo pipefail

# Converts PNG/PSD files under client/Assets/Art/Old to RGBA .aseprite under client/Assets/Art.
# Requires Aseprite (tested with Steam build) and ImageMagick (magick/convert).
# PSD files are flattened to PNG first because neither Aseprite CLI nor Lua can load PSD.
# All outputs are forced to RGBA color mode to avoid indexed-mode transparency/color loss.

SOURCE_DIR="client/Assets/Art/Old"
TARGET_DIR="client/Assets/Art"

# Globals accessed by cleanup trap.
_TMPDIR=""
_LUA_SCRIPT=""

usage() {
    cat <<USAGE
Usage: $(basename "$0") [options]

Options:
  -f, --force      Overwrite existing .aseprite files
  -n, --dry-run    Print what would be done without converting
  -h, --help       Show this help

Environment:
  ASEPRITE         Path to aseprite binary (auto-detected if unset)
  MAGICK           Path to ImageMagick binary (auto-detected if unset)
USAGE
}

cleanup() {
    if [[ -n "${_TMPDIR:-}" && -d "$_TMPDIR" ]]; then
        rm -rf "$_TMPDIR"
    fi
    if [[ -n "${_LUA_SCRIPT:-}" && -f "$_LUA_SCRIPT" ]]; then
        rm -f "$_LUA_SCRIPT"
    fi
}

find_aseprite() {
    if [[ -n "${ASEPRITE:-}" ]]; then
        if [[ -x "$ASEPRITE" ]]; then
            echo "$ASEPRITE"
            return 0
        fi
        echo "ERROR: ASEPRITE env points to non-executable: $ASEPRITE" >&2
        return 1
    fi

    local candidates=(
        "$(command -v aseprite 2>/dev/null || true)"
        "$HOME/.local/share/Steam/steamapps/common/Aseprite/aseprite"
        "$HOME/.steam/steam/steamapps/common/Aseprite/aseprite"
        "/usr/bin/aseprite"
        "/usr/local/bin/aseprite"
        "/opt/aseprite/aseprite"
    )
    for c in "${candidates[@]}"; do
        if [[ -n "$c" && -x "$c" ]]; then
            echo "$c"
            return 0
        fi
    done

    echo "ERROR: aseprite binary not found. Set ASEPRITE env or install Aseprite." >&2
    return 1
}

find_magick() {
    if [[ -n "${MAGICK:-}" ]]; then
        if [[ -x "$MAGICK" ]]; then
            echo "$MAGICK"
            return 0
        fi
        echo "ERROR: MAGICK env points to non-executable: $MAGICK" >&2
        return 1
    fi

    local candidates=(
        "$(command -v magick 2>/dev/null || true)"
        "$(command -v convert 2>/dev/null || true)"
    )
    for c in "${candidates[@]}"; do
        if [[ -n "$c" && -x "$c" ]]; then
            echo "$c"
            return 0
        fi
    done

    echo "ERROR: ImageMagick (magick/convert) not found. Set MAGICK env or install ImageMagick." >&2
    return 1
}

# Escape a path for safe insertion into a Lua double-quoted string.
lua_quote() {
    local s="$1"
    # Replace backslashes with forward slashes (defensive) and escape double quotes.
    s="${s//\\//}"
    s="${s//\"/\\\"}"
    printf '%s' "$s"
}

main() {
    local force=0
    local dry_run=0

    while [[ $# -gt 0 ]]; do
        case "$1" in
            -f|--force) force=1; shift ;;
            -n|--dry-run) dry_run=1; shift ;;
            -h|--help) usage; exit 0 ;;
            *) echo "Unknown option: $1" >&2; usage >&2; exit 1 ;;
        esac
    done

    local aseprite magick
    aseprite="$(find_aseprite)"
    magick="$(find_magick)"

    if [[ "$dry_run" -eq 0 ]]; then
        echo "Aseprite: $aseprite"
        echo "ImageMagick: $magick"
    fi

    mkdir -p "$TARGET_DIR"

    _TMPDIR="$(mktemp -d)"
    _LUA_SCRIPT="$_TMPDIR/convert.lua"
    trap cleanup EXIT

    local -a files
    mapfile -t files < <(find "$SOURCE_DIR" -type f \( -iname '*.png' -o -iname '*.psd' \) | sort)

    if [[ ${#files[@]} -eq 0 ]]; then
        echo "No PNG/PSD files found in $SOURCE_DIR"
        exit 0
    fi

    echo "Found ${#files[@]} file(s) to process."

    # Pre-compute basenames that exist as PNG and/or PSD to resolve collisions.
    local -A has_png has_psd
    local rel dir base name ext key
    for f in "${files[@]}"; do
        rel="${f#$SOURCE_DIR/}"
        dir="$(dirname "$rel")"
        base="$(basename "$f")"
        name="${base%.*}"
        ext="${base##*.}"
        key="$dir/$name"
        if [[ "${ext,,}" == "png" ]]; then
            has_png["$key"]=1
        elif [[ "${ext,,}" == "psd" ]]; then
            has_psd["$key"]=1
        fi
    done

    # Build conversion list and prepare temp PNGs for PSDs.
    local -a entries=()
    local processed=0 skipped=0

    for f in "${files[@]}"; do
        rel="${f#$SOURCE_DIR/}"
        dir="$(dirname "$rel")"
        base="$(basename "$f")"
        name="${base%.*}"
        ext="${base##*.}"
        key="$dir/$name"

        if [[ "$dir" == "." ]]; then
            out_dir="$TARGET_DIR"
        else
            out_dir="$TARGET_DIR/$dir"
        fi
        out_base="$name"

        if [[ "${ext,,}" == "psd" && -n "${has_png[$key]:-}" ]]; then
            out_base="${name}_psd"
        fi

        out_path="$out_dir/$out_base.aseprite"

        if [[ -e "$out_path" && "$force" -eq 0 ]]; then
            echo "[$processed/${#files[@]}] SKIP: $rel -> $out_path already exists"
            ((skipped++)) || true
            ((processed++)) || true
            continue
        fi

        if [[ "$dry_run" -eq 1 ]]; then
            echo "[$processed/${#files[@]}] $rel -> $out_path"
            ((processed++)) || true
            continue
        fi

        mkdir -p "$out_dir"

        local src_for_aseprite
        if [[ "${ext,,}" == "psd" ]]; then
            src_for_aseprite="$_TMPDIR/psd_${key//\//_}.png"
            # Flatten PSD composite to a Truecolor PNG to preserve alpha on import.
            "$magick" "$f"[0] -define png:color-type=6 "$src_for_aseprite" >/dev/null 2>&1
        else
            src_for_aseprite="$f"
        fi

        entries+=("{ input = \"$(lua_quote "$src_for_aseprite")\", output = \"$(lua_quote "$out_path")\" }")
        ((processed++)) || true
    done

    if [[ ${#entries[@]} -eq 0 ]]; then
        echo "Nothing to convert. Skipped: $skipped"
        exit 0
    fi

    if [[ "$dry_run" -eq 1 ]]; then
        echo ""
        echo "Dry run complete. Would convert ${#entries[@]} file(s)."
        exit 0
    fi

    # Generate the Lua conversion script.
    {
        echo "local files = {"
        for e in "${entries[@]}"; do
            echo "  $e,"
        done
        echo "}"
        echo ""
        echo "local errors = 0"
        echo "for _, f in ipairs(files) do"
        echo "    print('Processing: ' .. f.input)"
        echo "    local sprite = app.open(f.input)"
        echo "    if not sprite then"
        echo "        print('  FAILED to open ' .. f.input)"
        echo "        errors = errors + 1"
        echo "    else"
        echo "        if sprite.colorMode ~= ColorMode.RGB then"
        echo "            app.command.ChangePixelFormat{ format = \"rgb\" }"
        echo "        end"
        echo "        sprite:saveAs(f.output)"
        echo "        sprite:close()"
        echo "        print('  saved ' .. f.output)"
        echo "    end"
        echo "end"
        echo ""
        echo "if errors > 0 then"
        echo "    app.exit(1)"
        echo "else"
        echo "    app.exit(0)"
        echo "end"
    } > "$_LUA_SCRIPT"

    echo ""
    echo "Running Aseprite batch conversion (RGBA mode) ..."
    if ! "$aseprite" --batch --script "$_LUA_SCRIPT" 2>&1; then
        echo "ERROR: Aseprite conversion failed." >&2
        exit 1
    fi

    echo ""
    echo "Done. Converted: ${#entries[@]}, Skipped: $skipped"
}

main "$@"
