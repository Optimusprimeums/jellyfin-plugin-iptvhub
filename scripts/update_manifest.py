"""Add or correct a version in manifest.json from the zip that is actually published.

Usage:
  python scripts/update_manifest.py --version 1.2.1.0 --zip published/IptvHub_1.2.1.0.zip \
      --url https://github.com/.../IptvHub_1.2.1.0.zip --timestamp 2026-10-28T21:06:56Z \
      [--changelog "text"] [--target-abi 12.4.0.0]

The checksum is always the MD5 of --zip, so the manifest matches what Jellyfin downloads.
An existing entry keeps its changelog unless --changelog is given; a new entry goes on top.
Prints "changed" or "unchanged".
"""

import argparse
import hashlib
import json
import pathlib
import re

MANIFEST = pathlib.Path(__file__).resolve().parent.parent / "manifest.json"


def version_key(version):
    return tuple(int(p) for p in re.findall(r"\d+", version))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True)
    parser.add_argument("--zip", required=True, type=pathlib.Path)
    parser.add_argument("--url", required=True)
    parser.add_argument("--timestamp", required=True)
    parser.add_argument("--changelog", default=None)
    parser.add_argument("--target-abi", default="12.4.0.0")
    args = parser.parse_args()

    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", args.version):
        raise SystemExit(f"version must look like 1.2.3.4, got {args.version!r}")

    checksum = hashlib.md5(args.zip.read_bytes()).hexdigest()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    package = manifest[0]
    versions = package["versions"]

    existing = next((v for v in versions if v["version"] == args.version), None)
    entry = {
        "version": args.version,
        "changelog": args.changelog if args.changelog is not None else (existing or {}).get("changelog", ""),
        "targetAbi": (existing or {}).get("targetAbi", args.target_abi),
        "sourceUrl": args.url,
        "checksum": checksum,
        "timestamp": (existing or {}).get("timestamp", args.timestamp),
    }

    if existing == entry:
        print("unchanged")
        return

    if existing is not None:
        versions[versions.index(existing)] = entry
    else:
        versions.append(entry)
    versions.sort(key=lambda v: version_key(v["version"]), reverse=True)

    MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("changed")


if __name__ == "__main__":
    main()
