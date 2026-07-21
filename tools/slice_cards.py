#!/usr/bin/env python3
"""Slice «Карты и ширмы/карты NN.jpg» into game/assets/cards/{id:03d}.png."""
from __future__ import annotations

import json
import re
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SHEETS = ROOT / "Карты и ширмы"
OUT = ROOT / "game" / "assets" / "cards"
DATA = ROOT / "data" / "cards.json"

ROWS, COLS = 3, 4
INSET = 0.012
TARGET_W = 280
SOURCE_RE = re.compile(r"s(\d+)_r(\d+)c(\d+)")


def load_sheet(n: int, cache: dict[int, Image.Image]) -> Image.Image:
    if n not in cache:
        path = SHEETS / f"карты {n:02d}.jpg"
        cache[n] = Image.open(path).convert("RGB")
    return cache[n]


def crop_cell(sheet: Image.Image, r: int, c: int) -> Image.Image:
    w, h = sheet.size
    cw, ch = w / COLS, h / ROWS
    x0, y0 = c * cw, r * ch
    ix, iy = cw * INSET, ch * INSET
    box = (int(x0 + ix), int(y0 + iy), int(x0 + cw - ix), int(y0 + ch - iy))
    return sheet.crop(box)


def save_resized(im: Image.Image, path: Path) -> None:
    w, h = im.size
    nh = int(h * (TARGET_W / w))
    im.resize((TARGET_W, nh), Image.Resampling.LANCZOS).save(path, optimize=True)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    data = json.loads(DATA.read_text(encoding="utf-8"))
    cache: dict[int, Image.Image] = {}

    # Sheet 01 bottom-left is a card back on character sheets.
    save_resized(crop_cell(load_sheet(1, cache), 2, 0), OUT / "back.png")

    count = 0
    for arr in (data["characters"], data["laws"]):
        for card in arr:
            m = SOURCE_RE.match(card["source"])
            if not m:
                print("skip", card["id"], card.get("source"))
                continue
            s, r, c = map(int, m.groups())
            save_resized(crop_cell(load_sheet(s, cache), r, c), OUT / f"{card['id']:03d}.png")
            count += 1

    print(f"wrote {count} cards + back → {OUT}")


if __name__ == "__main__":
    main()
