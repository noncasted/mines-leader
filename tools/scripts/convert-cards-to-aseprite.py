#!/usr/bin/env python3
"""One-shot convert of client/Assets/Art/Cards PNG/PSD into .aseprite.

PNG sheets are a grid of variants with one Unity Sprite rect (the picked cell).
That rect is cropped out (Unity Y is bottom-left) and saved as its own .aseprite.

PSD files here are already single 64x64 icons — flatten the composite and convert
the full canvas. Extra leftover slices in .meta are ignored.

Existing .aseprite files are skipped unless --force.
Sources are not deleted.

Requires Aseprite CLI and ImageMagick (magick/convert/identify).
"""

from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
CARDS_DIR = REPO_ROOT / "client" / "Assets" / "Art" / "Cards"

ASEPRITE_CANDIDATES = (
    os.environ.get("ASEPRITE", ""),
    shutil.which("aseprite") or "",
    str(Path.home() / ".local/share/Steam/steamapps/common/Aseprite/aseprite"),
    str(Path.home() / ".steam/steam/steamapps/common/Aseprite/aseprite"),
    "/usr/bin/aseprite",
    "/usr/local/bin/aseprite",
    "/opt/aseprite/aseprite",
)

MAGICK_CANDIDATES = (
    os.environ.get("MAGICK", ""),
    shutil.which("magick") or "",
    shutil.which("convert") or "",
)

IDENTIFY_CANDIDATES = (
    shutil.which("identify") or "",
    shutil.which("magick") or "",
)


@dataclass(frozen=True)
class SpriteRect:
    name: str
    x: int
    y: int
    w: int
    h: int

    @property
    def area(self) -> int:
        return self.w * self.h


@dataclass
class Job:
    source: Path
    dest: Path
    image_w: int
    image_h: int
    crop: SpriteRect | None
    crop_top_y: int | None
    note: str


def find_executable(candidates: tuple[str, ...], label: str) -> str:
    for path in candidates:
        if path and os.path.isfile(path) and os.access(path, os.X_OK):
            return path
    sys.exit(f"ERROR: {label} not found. Set the matching env var or install it.")


def run(cmd: list[str], **kwargs) -> subprocess.CompletedProcess[str]:
    return subprocess.run(cmd, check=True, text=True, **kwargs)


def image_size(identify: str, path: Path) -> tuple[int, int]:
    target = f"{path}[0]" if path.suffix.lower() == ".psd" else str(path)
    if os.path.basename(identify) == "magick":
        cmd = [identify, "identify", "-format", "%w %h", target]
    else:
        cmd = [identify, "-format", "%w %h", target]
    out = run(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL).stdout.strip()
    w, h = out.split()
    return int(w), int(h)


def parse_sprite_rects(meta_text: str) -> list[SpriteRect]:
    match = re.search(
        r"\n  spriteSheet:\n(.*?)(?:\n  mipmapLimitGroupName:|\n  pSDRemoveMatte:|\Z)",
        meta_text,
        re.S,
    )
    if not match:
        return []

    block = match.group(1)
    sprites_match = re.search(r"\n    sprites:\n(.*?)(?:\n    outline:|\n    nameFileIdTable:|\Z)", block, re.S)
    if not sprites_match:
        return []

    sprites_block = sprites_match.group(1)
    if not sprites_block.startswith("\n"):
        sprites_block = "\n" + sprites_block
    parts = re.split(r"\n    - serializedVersion:", sprites_block)
    rects: list[SpriteRect] = []
    for part in parts[1:]:
        name = re.search(r"\n      name: (.*)", part)
        x = re.search(r"\n        x: ([-\d.]+)", part)
        y = re.search(r"\n        y: ([-\d.]+)", part)
        w = re.search(r"\n        width: ([-\d.]+)", part)
        h = re.search(r"\n        height: ([-\d.]+)", part)
        if not (name and x and y and w and h):
            continue
        rects.append(
            SpriteRect(
                name=name.group(1).strip(),
                x=int(float(x.group(1))),
                y=int(float(y.group(1))),
                w=int(float(w.group(1))),
                h=int(float(h.group(1))),
            )
        )
    return rects


def pick_sprite(stem: str, rects: list[SpriteRect]) -> tuple[SpriteRect | None, str]:
    if not rects:
        return None, "no sprite rect in meta, using full image"

    if len(rects) == 1:
        return rects[0], f"sprite {rects[0].name} {rects[0].w}x{rects[0].h}"

    stem_l = stem.lower()
    exact = [
        r
        for r in rects
        if r.name.lower() in {stem_l, f"{stem_l}_0"}
    ]
    # Prefer the largest among exact name matches, but only if it is not a leftover sliver.
    if exact:
        best_exact = max(exact, key=lambda r: r.area)
        largest = max(rects, key=lambda r: r.area)
        if best_exact.area >= largest.area * 0.5:
            others = ", ".join(f"{r.name} {r.w}x{r.h}" for r in rects if r is not best_exact)
            return best_exact, f"picked {best_exact.name} {best_exact.w}x{best_exact.h} (also: {others})"

    best = max(rects, key=lambda r: r.area)
    others = ", ".join(f"{r.name} {r.w}x{r.h}" for r in rects if r is not best)
    return best, f"largest {best.name} {best.w}x{best.h} (also: {others})"


def unity_to_top_left(rect: SpriteRect, image_h: int) -> int:
    return image_h - rect.y - rect.h


def plan_jobs(force: bool) -> list[Job]:
    jobs: list[Job] = []
    identify = find_executable(IDENTIFY_CANDIDATES, "ImageMagick identify")

    sources = sorted(
        p
        for p in CARDS_DIR.iterdir()
        if p.is_file() and p.suffix.lower() in {".png", ".psd"}
    )
    if not sources:
        sys.exit(f"ERROR: no PNG/PSD files in {CARDS_DIR}")

    for source in sources:
        dest = source.with_suffix(".aseprite")
        if dest.exists() and not force:
            continue

        meta = Path(str(source) + ".meta")
        rects = parse_sprite_rects(meta.read_text()) if meta.exists() else []
        image_w, image_h = image_size(identify, source)

        if source.suffix.lower() == ".psd":
            jobs.append(
                Job(
                    source=source,
                    dest=dest,
                    image_w=image_w,
                    image_h=image_h,
                    crop=None,
                    crop_top_y=None,
                    note=f"psd flatten full {image_w}x{image_h}",
                )
            )
            continue

        rect, note = pick_sprite(source.stem, rects)
        crop_top_y = None
        if rect is not None:
            crop_top_y = unity_to_top_left(rect, image_h)
            if rect.x < 0 or rect.y < 0 or rect.x + rect.w > image_w or rect.y + rect.h > image_h:
                sys.exit(
                    f"ERROR: sprite rect {rect} is outside {source.name} ({image_w}x{image_h})"
                )
            if crop_top_y < 0 or crop_top_y + rect.h > image_h:
                sys.exit(f"ERROR: flipped Y {crop_top_y} is outside {source.name}")
            if rect.w * rect.h > 64 * 64:
                note += "  [unusual: larger than a 64x64 card canvas]"
        jobs.append(
            Job(
                source=source,
                dest=dest,
                image_w=image_w,
                image_h=image_h,
                crop=rect,
                crop_top_y=crop_top_y,
                note=note,
            )
        )
    return jobs


def lua_quote(path: str) -> str:
    return path.replace("\\", "/").replace('"', '\\"')


def convert(jobs: list[Job], aseprite: str, magick: str, tmp: Path) -> None:
    entries: list[str] = []

    for i, job in enumerate(jobs):
        cropped = tmp / f"{i:03d}_{job.source.stem}_{uuid.uuid4().hex[:8]}.png"
        src = f"{job.source}[0]" if job.source.suffix.lower() == ".psd" else str(job.source)
        cmd = [magick, src]
        if job.crop is not None and job.crop_top_y is not None:
            cmd += [
                "-crop",
                f"{job.crop.w}x{job.crop.h}+{job.crop.x}+{job.crop_top_y}",
                "+repage",
            ]
        cmd += ["-define", "png:color-type=6", str(cropped)]
        run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        entries.append(
            f'  {{ input = "{lua_quote(str(cropped))}", output = "{lua_quote(str(job.dest))}" }},'
        )

    lua_path = tmp / "convert.lua"
    lua_path.write_text(
        "local files = {\n"
        + "\n".join(entries)
        + "\n}\n"
        + """
local errors = 0
for _, f in ipairs(files) do
    print("Processing: " .. f.output)
    local sprite = app.open(f.input)
    if not sprite then
        print("  FAILED to open " .. f.input)
        errors = errors + 1
    else
        if sprite.colorMode ~= ColorMode.RGB then
            app.command.ChangePixelFormat{ format = "rgb" }
        end
        sprite:saveAs(f.output)
        sprite:close()
        print("  saved " .. f.output)
    end
end
if errors > 0 then app.exit(1) else app.exit(0) end
"""
    )
    result = subprocess.run([aseprite, "--batch", "--script", str(lua_path)], text=True)
    if result.returncode != 0:
        sys.exit("ERROR: Aseprite conversion failed.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("-f", "--force", action="store_true", help="overwrite existing .aseprite files")
    parser.add_argument("-n", "--dry-run", action="store_true", help="print plan without writing files")
    args = parser.parse_args()

    if not CARDS_DIR.is_dir():
        sys.exit(f"ERROR: missing {CARDS_DIR}")

    jobs = plan_jobs(force=args.force)
    skipped = sum(
        1
        for p in CARDS_DIR.iterdir()
        if p.suffix.lower() in {".png", ".psd"} and p.with_suffix(".aseprite").exists()
    )
    if not args.force:
        already = skipped
    else:
        already = 0

    print(f"Cards dir: {CARDS_DIR}")
    print(f"To convert: {len(jobs)}")
    if not args.force:
        print(f"Skip existing .aseprite: {already}")
    print()

    for job in jobs:
        if job.crop is None:
            action = f"full {job.image_w}x{job.image_h}"
        else:
            action = (
                f"crop {job.crop.w}x{job.crop.h}+{job.crop.x}+{job.crop_top_y} "
                f"(unity y={job.crop.y})"
            )
        print(f"  {job.source.name:32} -> {job.dest.name:32} {action}  {job.note}")

    if args.dry_run or not jobs:
        if args.dry_run:
            print("\nDry run, nothing written.")
        return

    aseprite = find_executable(ASEPRITE_CANDIDATES, "aseprite")
    magick = find_executable(MAGICK_CANDIDATES, "ImageMagick")
    print(f"\nAseprite: {aseprite}")
    print(f"ImageMagick: {magick}")

    with tempfile.TemporaryDirectory(prefix="cards-aseprite-") as tmp:
        convert(jobs, aseprite, magick, Path(tmp))

    print(f"\nDone. Converted: {len(jobs)}, skipped existing: {already}")


if __name__ == "__main__":
    main()
