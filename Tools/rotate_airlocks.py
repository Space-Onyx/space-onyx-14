#!/usr/bin/env python3
"""Rotate door-like structures to match neighboring blockers on one map."""

from __future__ import annotations

import argparse
import base64
import math
import re
import shutil
import struct
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    raise SystemExit("PyYAML required: python -m pip install pyyaml")

CHUNK_SIZE = 16
WALL_PREFIXES = ("Wall", "WallReinforced", "WallSolid", "WallRock", "WallPlastitanium", "WallShuttle")
TARGET_SUBSTRINGS = ("Airlock", "Shutter", "BlastDoor", "HighSec")
TARGET_PREFIXES = ("Firelock", "PlasticFlapsAirtight")
TARGET_IDS = ("WoodDoor", "PlasmaDoor", "GoldDoor", "SilverDoor", "PaperDoor", "WebDoor", "CardDoor", "SolidSecretDoor")
NON_STRUCTURE_TARGETS = ("FirelockFrame", "FirelockElectronics", "FirelockEdge")


class MapLoader(yaml.SafeLoader):
    pass


def construct_tagged(loader: yaml.Loader, suffix: str, node: yaml.Node):
    if isinstance(node, yaml.ScalarNode):
        return loader.construct_scalar(node)
    if isinstance(node, yaml.SequenceNode):
        return loader.construct_sequence(node, deep=True)
    return loader.construct_mapping(node, deep=True)


MapLoader.add_multi_constructor("!", construct_tagged)


def vec(value: str) -> tuple[float, float]:
    x, y = (float(part) for part in value.split(",", 1))
    return x, y


def angle(value: object) -> float:
    if isinstance(value, str):
        value = value.split()[0]
    return float(value)


def orientation(rotation: float) -> str:
    # 0 / pi = horizontal, pi/2 / -pi/2 = vertical
    return "horizontal" if abs((rotation / math.pi) % 1) < 0.25 or abs((rotation / math.pi) % 1) > 0.75 else "vertical"


def parse_chunks(root: dict) -> set[tuple[int, tuple[int, int]]]:
    tile_names = root.get("tilemap", {})
    wall_ids = {int(k) for k, v in tile_names.items() if isinstance(v, str) and v.startswith("Wall")}
    walls: set[tuple[int, tuple[int, int]]] = set()
    chunks_found: list[tuple[int | None, dict]] = []

    def find_chunks(node: object, owner: int | None = None):
        if isinstance(node, dict):
            if "uid" in node and any(c.get("type") == "MapGrid" for c in node.get("components", [])):
                owner = int(node["uid"])
            if isinstance(node.get("chunks"), dict):
                chunks_found.extend((owner, ch) for ch in node["chunks"].values())
            for v in node.values():
                find_chunks(v, owner)
        elif isinstance(node, list):
            for v in node:
                find_chunks(v, owner)

    find_chunks(root)
    for owner, chunk_data in chunks_found:
        if owner is None:
            continue
        origin = tuple(int(x) for x in chunk_data["ind"].split(","))
        raw = base64.b64decode(chunk_data["tiles"])
        for idx in range(CHUNK_SIZE * CHUNK_SIZE):
            tid = struct.unpack_from("<i", raw, idx * 7)[0]
            if tid not in wall_ids:
                continue
            x = origin[0] * CHUNK_SIZE + idx % CHUNK_SIZE
            y = origin[1] * CHUNK_SIZE + idx // CHUNK_SIZE
            walls.add((owner, (x, y)))
    return walls


def collect_entities(root: dict):
    """Return entities with prototype, grid, tile position, rotation, and UID."""
    result = []
    parents: dict[int, int] = {}
    grids: set[int] = set(root.get("grids", []))

    def visit(node: object, proto: str = "", owner: int | None = None):
        if isinstance(node, list):
            for ch in node:
                visit(ch, proto, owner)
            return
        if not isinstance(node, dict):
            return
        cur_proto = str(node.get("proto", proto))
        cur_owner = owner
        ent = node if "uid" in node else None
        if ent is not None and any(c.get("type") == "MapGrid" for c in ent.get("components", [])):
            cur_owner = int(ent["uid"])
            grids.add(cur_owner)
        if ent is not None:
            tr = next((c for c in ent.get("components", []) if c.get("type") == "Transform"), None)
            if tr and isinstance(tr.get("parent"), int):
                parents[int(ent["uid"])] = int(tr["parent"])
            if tr and "pos" in tr:
                x, y = vec(tr["pos"])
                result.append((ent, cur_proto, cur_owner, (math.floor(x), math.floor(y)), angle(tr.get("rot", 0)), int(ent["uid"])))
        for ch in node.get("entities", []):
            visit(ch, cur_proto, cur_owner)

    visit(root.get("entities", []))
    resolved = []
    for ent, proto, owner, pos, rot, uid in result:
        orig_uid = uid
        cur = uid
        while owner is None and cur in parents:
            cur = parents[cur]
            if cur in grids:
                owner = cur
                break
        resolved.append((ent, proto, owner, pos, rot, orig_uid))
    return resolved


def is_blocker(proto: str) -> bool:
    return (
        proto.startswith(WALL_PREFIXES)
        or "Window" in proto
        or "Windoor" in proto
        or is_target(proto)
    )


def is_target(proto: str) -> bool:
    return (
        proto not in NON_STRUCTURE_TARGETS
        and (proto in TARGET_IDS or any(part in proto for part in TARGET_SUBSTRINGS) or proto.startswith(TARGET_PREFIXES))
    )


def desired_orientation(pos: tuple[int, int], owner: int | None, blockers: set[tuple[int, tuple[int, int]]]) -> str | None:
    x, y = pos
    left = (owner, (x - 1, y)) in blockers
    right = (owner, (x + 1, y)) in blockers
    up = (owner, (x, y + 1)) in blockers
    down = (owner, (x, y - 1)) in blockers

    if left and right:
        return "horizontal"
    if up and down:
        return "vertical"
    if left or right:
        return "horizontal"
    if up or down:
        return "vertical"
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Rotate door-like structures on one map by neighboring blockers. Writes file and .bak unless --dry-run.")
    parser.add_argument("map", type=Path, help="path to one map YAML file")
    parser.add_argument("--dry-run", action="store_true", help="report changes without writing")
    args = parser.parse_args()

    path = args.map
    if not path.is_file():
        raise SystemExit(f"Map not found: {path}")

    with path.open(encoding="utf-8") as f:
        root = yaml.load(f, Loader=MapLoader)

    all_ents = collect_entities(root)
    blockers: set[tuple[int, tuple[int, int]]] = set()
    for _, proto, owner, pos, _, _ in all_ents:
        if is_blocker(proto):
            blockers.add((owner, pos))
    blockers |= parse_chunks(root)

    targets = [(owner, pos, rot, uid, proto) for _, proto, owner, pos, rot, uid in all_ents if is_target(proto)]

    replacements: dict[int, str] = {}
    for owner, pos, rot, uid, proto in targets:
        want = desired_orientation(pos, owner, blockers)
        if want is None:
            continue
        cur = orientation(rot)
        if want == cur:
            continue
        new_rot = "0 rad" if want == "horizontal" else "1.5707963267948966 rad"
        replacements[uid] = new_rot
        print(f"{path}:{pos[0]},{pos[1]} uid={uid} {proto} {cur} -> {want}")

    print(f"Changed: {len(replacements)}")

    if not args.dry_run and replacements:
        backup = path.with_name(path.name + ".bak")
        shutil.copy2(path, backup)
        text = path.read_text(encoding="utf-8")
        for uid, new_rot in replacements.items():
            m = re.search(rf"(?ms)^  - uid: {uid}\n.*?(?=^  - uid:|\Z)", text)
            if not m:
                raise SystemExit(f"Could not locate entity block for uid {uid}")
            block = m.group(0)
            def repl_transform(match):
                header = match.group(1)
                body = match.group(2)
                if re.search(r"(?m)^      rot:", body):
                    body = re.sub(r"(?m)^      rot:.*?$", f"      rot: {new_rot}", body, count=1)
                else:
                    body = f"      rot: {new_rot}\n" + body
                return header + body

            new_block, n = re.subn(r"(?ms)(^    - type: Transform\n)(.*?)(?=^    - type:|\Z)", repl_transform, block, count=1)
            if n == 0:
                raise SystemExit(f"Transform not found for uid {uid}")
            if len(re.findall(r"(?m)^      rot:", new_block)) > 1:
                raise SystemExit(f"Duplicate rot created for uid {uid}")
            text = text[: m.start()] + new_block + text[m.end() :]
        path.write_text(text, encoding="utf-8", newline="\n")
        print(f"Wrote: {path}")
        print(f"Backup: {backup}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
