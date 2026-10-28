#!/usr/bin/env python3
"""Verify tracked Fox files or reconstitute the exact upstream snapshot in scratch space.

No third-party dependencies. Does not patch source/config/SQL or run upstream code.
Never put the full snapshot in vendor/: old records and large maps stay out of Git.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import stat
import sys
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
COMMIT = "70f3560465ba73cef0250109af7cf167b508516f"
ARCHIVE_SHA256 = "6c0561b395869bcfb8f6b633efa38916ae8dffa1d2ad52ac6a7fceb14febeb20"
URL = f"https://codeload.github.com/darkfoxdeveloper/FoxConquer5517/zip/{COMMIT}"
PREFIX = f"FoxConquer5517-{COMMIT}/"
MAX_ARCHIVE_BYTES = 8 * 1024 * 1024
MAX_EXPANDED_BYTES = 300 * 1024 * 1024


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def safe_relative(name: str) -> str:
    p = PurePosixPath(name)
    if (not name or p.is_absolute() or ".." in p.parts or "\\" in name
            or ":" in name or str(p) != name):
        raise ValueError(f"Unsafe archive/manifest path: {name!r}")
    return name


def manifest() -> dict:
    data = json.loads((ROOT / "audit/fox5517-manifest.json").read_text(encoding="utf-8"))
    if data["commit"] != COMMIT or data["archive_sha256"] != ARCHIVE_SHA256:
        raise ValueError("Unexpected baseline identity")
    paths = [safe_relative(f["path"]) for f in data["files"]]
    if len(paths) != 810 or len(set(paths)) != len(paths):
        raise ValueError("Unexpected or duplicate baseline entries")
    if sum(f["bytes"] for f in data["files"]) != 278410351:
        raise ValueError("Unexpected expanded baseline size")
    return data


def verify(directory: Path, *, full: bool = False) -> dict:
    files = [f for f in manifest()["files"] if full or f["tracked"]]
    for f in files:
        p = directory / f["path"]
        if p.is_symlink() or not p.is_file():
            raise ValueError(f"Missing/unsafe file: {f['path']}")
        if p.stat().st_size != f["bytes"] or digest(p) != f["sha256"]:
            raise ValueError(f"Upstream bytes differ: {f['path']}")
    return {"verified_files": len(files), "verified_bytes": sum(f["bytes"] for f in files),
            "mode": "full-original" if full else "tracked-original", "source_modified": False}


def ensure_archive(path: Path) -> None:
    if path.exists():
        if digest(path) != ARCHIVE_SHA256:
            raise ValueError("Existing archive checksum differs; refusing to replace it")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    partial = path.with_suffix(path.suffix + ".partial")
    try:
        with urllib.request.urlopen(URL, timeout=90) as response, partial.open("wb") as out:
            size = 0
            for block in iter(lambda: response.read(1024 * 1024), b""):
                size += len(block)
                if size > MAX_ARCHIVE_BYTES:
                    raise ValueError("Archive exceeds download size limit")
                out.write(block)
        if digest(partial) != ARCHIVE_SHA256:
            raise ValueError("Downloaded archive checksum differs")
        os.replace(partial, path)
    finally:
        if partial.exists():
            partial.unlink()


def materialize(destination: Path, archive: Path) -> dict:
    destination = destination.resolve()
    # This tool never writes an original SQL dump or native binary into tracked payload.
    if ROOT == destination or ROOT in destination.parents:
        cache = (ROOT / ".cache").resolve()
        if destination != cache and cache not in destination.parents:
            raise ValueError("Inside this repository, use only .cache/ for the full snapshot")
    if destination.is_symlink():
        raise ValueError("Destination must not be a symlink")
    if destination.exists() and any(destination.iterdir()):
        # A repeat verifies rather than overwriting potentially changed code/data.
        return verify(destination, full=True)
    expected = {f["path"]: f for f in manifest()["files"]}
    ensure_archive(archive)
    with zipfile.ZipFile(archive) as source:
        members = []
        seen = set()
        for item in source.infolist():
            if not item.filename.startswith(PREFIX) or stat.S_ISLNK(item.external_attr >> 16):
                raise ValueError("Unexpected root or symlink in upstream archive")
            if item.is_dir():
                continue
            name = safe_relative(item.filename[len(PREFIX):])
            if name not in expected or name in seen:
                raise ValueError("Unexpected/duplicate file in upstream archive")
            if item.file_size != expected[name]["bytes"]:
                raise ValueError("Unexpected expanded member size")
            members.append((name, item)); seen.add(name)
        if seen != set(expected) or sum(i.file_size for _, i in members) > MAX_EXPANDED_BYTES:
            raise ValueError("Unexpected/incomplete archive inventory")
        destination.mkdir(parents=True, exist_ok=True)
        for name, item in members:
            raw = source.read(item)
            if hashlib.sha256(raw).hexdigest() != expected[name]["sha256"]:
                raise ValueError(f"Member checksum differs: {name}")
            path = destination / name
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("xb") as output:
                output.write(raw)
    return verify(destination, full=True)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    check = sub.add_parser("verify")
    check.add_argument("--source", type=Path, default=ROOT / "vendor/FoxConquer5517")
    check.add_argument("--full", action="store_true")
    fetch = sub.add_parser("materialize")
    fetch.add_argument("--dest", type=Path, default=ROOT / ".cache/fox5517/original")
    fetch.add_argument("--archive", type=Path, default=ROOT / ".cache/fox5517/upstream.zip")
    args = parser.parse_args()
    result = (verify(args.source, full=args.full) if args.command == "verify"
              else materialize(args.dest, args.archive))
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, zipfile.BadZipFile) as exc:
        sys.exit(f"Baseline verification stopped: {exc}")
