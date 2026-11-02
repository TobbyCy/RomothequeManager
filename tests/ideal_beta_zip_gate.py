"""#149 Lot B: strict read-only verification of an extracted IDEAL beta ZIP.

This is a candidate VALIDATION gate, not publication or a permission grant.
It never scans a user library and intentionally rejects bundled game fixtures.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import unicodedata

MANIFEST = "BETA-IDEAL-MANIFEST.json"
PACKAGE = "RomothequeManager"
VERSION = "v0.5.0-beta.1"
RUNTIME = "Microsoft.NETCore.App.Runtime.win-x64/10.0.12"
HEX40 = re.compile(r"[0-9a-f]{40}\Z")
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
REQUIRED = {
    "RomothequeManager.V2.exe", "RomothequeManager.V2.dll",
    "RomothequeManager.V2.deps.json", "RomothequeManager.V2.runtimeconfig.json",
    "coreclr.dll", "hostfxr.dll", "e_sqlite3.dll", "Microsoft.Data.Sqlite.dll",
    "SQLitePCLRaw.core.dll", "SQLitePCLRaw.batteries_v2.dll",
    "SQLitePCLRaw.provider.e_sqlite3.dll",
    "LICENSE", "LEGAL.md", "BRANDING.md", "BETA-IDEAL-NOTICE.txt",
    "THIRD-PARTY-NOTICES.txt", "THIRD-PARTY-INVENTORY.md",
    "licenses/dotnet-runtime/LICENSE.TXT",
    "licenses/dotnet-runtime/THIRD-PARTY-NOTICES.TXT",
    "licenses/dotnet-runtime/PROVENANCE.json",
    "licenses/Microsoft.Data.Sqlite-LICENSE.txt",
    "licenses/SQLitePCLRaw-LICENSE.txt",
    "licenses/SQLitePCLRaw-NOTICE.txt",
    "licenses/SQLite-PUBLIC-DOMAIN.txt",
}
FORBIDDEN_EXT = {
    ".rom", ".chd", ".iso", ".cue", ".gdi", ".cdi", ".nes", ".sfc",
    ".smc", ".gba", ".gbc", ".n64", ".z64", ".nsp", ".xci", ".wad",
    ".sav", ".srm", ".pfx", ".pem", ".p12", ".snk", ".key",
}
FORBIDDEN_NAMES = {".env", "credentials.json", "secrets.json", "id_rsa", "id_ed25519"}
FORBIDDEN_DIRS = {"roms", "bios", "fixture", "fixtures", "userdata", "personal"}


class BetaZipRejected(ValueError):
    pass


def demand(ok: bool, reason: str) -> None:
    if not ok:
        raise BetaZipRejected(reason)


def sha256_file(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as reader:
        for chunk in iter(lambda: reader.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def valid_name(path: str) -> bool:
    if not isinstance(path, str) or not path or "\\" in path:
        return False
    for component in path.split("/"):
        if component in ("", ".", "..") or component.endswith((".", " ")):
            return False
        if re.search(r'[:<>"|?*\x00-\x1f]', component):
            return False
        if component.split(".")[0].upper() in {
            "CON", "PRN", "AUX", "NUL",
            *(f"COM{n}" for n in range(1, 10)),
            *(f"LPT{n}" for n in range(1, 10)),
        }:
            return False
    return True


def casefold(path: str) -> str:
    return unicodedata.normalize("NFC", path).casefold()


def verify(root: Path, source: str) -> dict:
    demand(HEX40.fullmatch(source) is not None, "BETA_SOURCE_SHA_INVALID")
    demand(root.exists() and root.is_dir() and not root.is_symlink(),
           "BETA_EXTRACT_ROOT_INVALID")
    manifest_path = root / MANIFEST
    demand(manifest_path.is_file() and not manifest_path.is_symlink(),
           "BETA_MANIFEST_MISSING")
    try:
        raw = manifest_path.read_bytes()
        doc = json.loads(raw.decode("utf-8", "strict"))
    except (OSError, UnicodeError, ValueError) as exc:
        raise BetaZipRejected("BETA_MANIFEST_UNREADABLE") from exc
    demand(isinstance(doc, dict) and set(doc) == {
        "schemaVersion", "package", "version", "sourceCommit",
        "phase", "distribution", "runtimeIdentifier", "runtimePack",
        "selfContained", "fixtureDataBundled", "files",
    }, "BETA_MANIFEST_SCHEMA_INVALID")
    demand(doc["schemaVersion"] == 1 and type(doc["schemaVersion"]) is int,
           "BETA_MANIFEST_SCHEMA_INVALID")
    demand(doc["package"] == PACKAGE and doc["version"] == VERSION
           and doc["sourceCommit"] == source and doc["phase"] == "validate"
           and doc["distribution"] == "LEGAL_AND_OWNER_PUBLISH_HOLD"
           and doc["runtimeIdentifier"] == "win-x64"
           and doc["runtimePack"] == RUNTIME
           and doc["selfContained"] is True
           and doc["fixtureDataBundled"] is False,
           "BETA_MANIFEST_CONTRACT_INVALID")
    records = doc["files"]
    demand(isinstance(records, list) and len(records) >= 40,
           "BETA_MANIFEST_FILES_EMPTY")
    seen: set[str] = set()
    declared: set[str] = set()
    last_path = ""
    for row in records:
        demand(isinstance(row, dict) and set(row) == {"path", "sizeBytes", "sha256"},
               "BETA_MANIFEST_FILE_SCHEMA")
        name = row["path"]
        demand(valid_name(name) and name != MANIFEST,
               "BETA_MANIFEST_FILE_PATH")
        demand(name > last_path and casefold(name) not in seen,
               "BETA_MANIFEST_DUPLICATE_OR_UNSORTED")
        last_path = name
        seen.add(casefold(name))
        declared.add(name)
        demand(Path(name).suffix.lower() not in FORBIDDEN_EXT
               and Path(name).name.lower() not in FORBIDDEN_NAMES
               and not any(part.lower() in FORBIDDEN_DIRS for part in name.split("/")[:-1]),
               "BETA_FORBIDDEN_FILE")
        expected_len, expected_hash = row["sizeBytes"], row["sha256"]
        demand(type(expected_len) is int and expected_len >= 0
               and isinstance(expected_hash, str)
               and HEX64.fullmatch(expected_hash) is not None,
               "BETA_MANIFEST_FILE_METADATA")
        absolute = root.joinpath(*name.split("/"))
        demand(absolute.is_file() and not absolute.is_symlink()
               and absolute.resolve().is_relative_to(root.resolve()),
               "BETA_MANIFEST_FILE_MISSING_OR_LINK")
        demand(absolute.stat().st_size == expected_len
               and sha256_file(absolute) == expected_hash,
               "BETA_MANIFEST_FILE_HASH_MISMATCH")
    actual: set[str] = set()
    for entry in root.rglob("*"):
        demand(not entry.is_symlink() and entry.resolve().is_relative_to(root.resolve()),
               "BETA_EXTRACT_REPARSE_REFUSED")
        if entry.is_file():
            name = entry.relative_to(root).as_posix()
            demand(valid_name(name), "BETA_EXTRACT_FILE_PATH")
            actual.add(name)
    demand(actual == declared | {MANIFEST},
           "BETA_EXTRACT_FILE_SET_MISMATCH")
    demand(REQUIRED <= declared, "BETA_REQUIRED_RUNTIME_OR_LICENSE_MISSING")
    notice = (root / "BETA-IDEAL-NOTICE.txt").read_text(encoding="utf-8")
    demand("NOT A RELEASE" in notice and "NO REAL ROM" in notice
           and "phase=validate" in notice and "LEGAL HOLD" in notice,
           "BETA_NOTICE_INVALID")
    provenance_file = root / "licenses/dotnet-runtime/PROVENANCE.json"
    try:
        provenance = json.loads(provenance_file.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise BetaZipRejected("BETA_RUNTIME_PROVENANCE_INVALID") from exc
    demand(provenance.get("package") == "Microsoft.NETCore.App.Runtime.win-x64"
           and provenance.get("version") == "10.0.12",
           "BETA_RUNTIME_PROVENANCE_INVALID")
    for key, filename in (
        ("licenseSha256", "LICENSE.TXT"),
        ("thirdPartyNoticesSha256", "THIRD-PARTY-NOTICES.TXT"),
    ):
        demand(provenance.get(key) == sha256_file(root / "licenses/dotnet-runtime" / filename),
               "BETA_RUNTIME_LEGAL_HASH_INVALID")
    return {"source": source, "files": len(records), "phase": "validate",
            "status": "BETA_IDEAL_ZIP_GATE_PASS"}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--source", required=True)
    args = parser.parse_args()
    try:
        result = verify(args.root, args.source)
    except (BetaZipRejected, OSError) as exc:
        print("BETA_IDEAL_ZIP_GATE_FAIL " + str(exc), file=sys.stderr)
        return 1
    print(f"BETA_IDEAL_ZIP_GATE_PASS source={result['source']} "
          f"files={result['files']} phase=validate no_personal_data=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
