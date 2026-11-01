"""The shared colour palette: one small texture, one material for every model.

Each colour is a 16 x 16 px swatch on an 8 x 8 grid (128 x 128 px). A face takes its colour by putting all its UVs on
the centre of a swatch, so a model needs one material and one draw call. Slots never move: add new colours in free slots.
"""
import os
import struct
import zlib

GRID = 8          # swatches per row and column
SWATCH = 16       # pixels per swatch side
FREE = "808080"   # colour of unused slots

# slot index -> (name, sRGB hex). Era I: timber, thatch, plaster, warm greens.
PALETTE = [
    ("grass", "7DB24C"), ("grass_dark", "5C9A3B"), ("dirt", "B98C5C"), ("stone", "B0AA9F"),
    ("stone_dark", "7D786F"), ("wood", "C98E54"), ("wood_dark", "7A4E2D"), ("wood_end", "EBC88E"),
    ("thatch", "E5B24C"), ("thatch_dark", "B8862F"), ("plaster", "F4E8CF"), ("red", "D4553F"),
    ("blue", "4F80B9"), ("window", "3A3F57"), ("leaf", "57A443"), ("metal", "C8CDD5"),
    ("leaf_dark", "3B7D34"), ("water", "6EC1E4"), ("skin", "F0C08A"), ("gold", "F2C230"),
]

INDEX = {name: i for i, (name, _) in enumerate(PALETTE)}


def uv_of(name):
    """UV coordinate of the centre of a colour's swatch (V goes up from the bottom row)."""
    i = INDEX[name]
    col, row = i % GRID, i // GRID
    return ((col + 0.5) / GRID, 1.0 - (row + 0.5) / GRID)


def _rgb(hex_):
    return bytes(int(hex_[k:k + 2], 16) for k in (0, 2, 4))


def write_png(path):
    """Writes the palette as an 8-bit RGB PNG with no dependencies (byte-identical on every run)."""
    size = GRID * SWATCH
    colours = [hex_ for _, hex_ in PALETTE] + [FREE] * (GRID * GRID - len(PALETTE))
    rows = []
    for y in range(size):
        row = bytearray([0])  # filter type: none
        for x in range(size):
            row += _rgb(colours[(y // SWATCH) * GRID + x // SWATCH])
        rows.append(bytes(row))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(b"".join(rows), 9))
    png += chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)
