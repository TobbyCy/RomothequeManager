"""SQLite legal/package provenance gate for synthetic win-x64 publish outputs.

Read-only. No package download, real-library access or Release operation.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
from urllib.parse import urlparse

PACKAGE_IDS = (
    "Microsoft.Data.Sqlite", "Microsoft.Data.Sqlite.Core",
    "SQLitePCLRaw.bundle_e_sqlite3", "SQLitePCLRaw.core",
    "SQLitePCLRaw.lib.e_sqlite3", "SQLitePCLRaw.provider.e_sqlite3",
)
BINARY_NAMES = (
    "Microsoft.Data.Sqlite.dll", "SQLitePCLRaw.batteries_v2.dll",
    "SQLitePCLRaw.core.dll", "SQLitePCLRaw.provider.e_sqlite3.dll", "e_sqlite3.dll",
)
LEGAL_FILES = (
    "Microsoft.Data.Sqlite-LICENSE.txt", "SQLitePCLRaw-LICENSE.txt",
    "SQLitePCLRaw-NOTICE.txt", "SQLite-PUBLIC-DOMAIN.txt",
)


class LegalGateError(Exception):
    pass


def require(test, code):
    if not test:
        raise LegalGateError(code)


def hash_file(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65536), b""):
            h.update(block)
    return h.hexdigest()


def git_blob_sha(data):
    return hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\x00" + data).hexdigest()


def parse_json(path, code):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise LegalGateError(code) from exc


def validate(manifest_file, publish_dir, packages_dir, lock_file, notices_file, licenses_dir):
    manifest = parse_json(manifest_file, "SQLITE_MANIFEST_MISSING_OR_INVALID")
    require(manifest.get("schema") == 1 and manifest.get("rid") == "win-x64",
            "SQLITE_MANIFEST_PROFILE_MISMATCH")
    packages = manifest.get("packages")
    binaries = manifest.get("binaries")
    legal = manifest.get("legalFiles")
    require(isinstance(packages, list) and isinstance(binaries, list)
            and isinstance(legal, list), "SQLITE_MANIFEST_SECTIONS_INVALID")
    require(len(packages) == len(PACKAGE_IDS)
            and {x.get("id") for x in packages} == set(PACKAGE_IDS),
            "SQLITE_PACKAGE_COMPONENT_SET_MISMATCH")
    require(len(binaries) == len(BINARY_NAMES)
            and {x.get("name") for x in binaries} == set(BINARY_NAMES),
            "SQLITE_BINARY_COMPONENT_SET_MISMATCH")
    require(len(legal) == len(LEGAL_FILES)
            and {x.get("file") for x in legal} == set(LEGAL_FILES),
            "SQLITE_LEGAL_FILE_SET_MISMATCH")
    lock = parse_json(lock_file, "SQLITE_NUGET_LOCK_MISSING_OR_INVALID")
    deps = lock.get("dependencies", {}).get("net10.0")
    require(isinstance(deps, dict), "SQLITE_LOCK_TFM_MISSING")
    locked_ids = {x for x in deps if x.startswith(("SQLitePCLRaw.", "Microsoft.Data.Sqlite"))}
    require(locked_ids == set(PACKAGE_IDS), "SQLITE_NUGET_LOCK_COMPONENT_DRIFT")
    notice = notices_file.read_text(encoding="utf-8") if notices_file.is_file() else ""
    require("SQLITE-LEGAL-01" in notice, "SQLITE_NOTICE_BLOCK_MISSING")
    for required in (*PACKAGE_IDS, *LEGAL_FILES, "e_sqlite3.dll", "public domain"):
        require(required.casefold() in notice.casefold(), "SQLITE_NOTICE_REFERENCE_MISSING:" + required)
    for record in legal:
        filename = record["file"]
        blob = record.get("gitBlobSha")
        path = licenses_dir / filename
        require(re.fullmatch(r"[a-f0-9]{40}", str(blob)) is not None,
                "SQLITE_LEGAL_BLOB_HASH_INVALID:" + filename)
        require(path.is_file() and not path.is_symlink() and path.stat().st_size >= 100,
                "SQLITE_LEGAL_TEXT_MISSING:" + filename)
        raw_text = path.read_bytes()
        # GitHub Actions Windows text=auto may checkout CRLF while the
        # canonical Git blob remains LF. Only this reversible EOL change
        # is accepted; changed wording/whitespace still fails closed.
        canonical_text = raw_text.replace(b"\r\n", b"\n")
        require(git_blob_sha(raw_text) == blob or git_blob_sha(canonical_text) == blob,
                "SQLITE_LEGAL_TEXT_HASH_MISMATCH:" + filename)
        print("SQLITE_LEGAL_TEXT_VERIFIED file=" + filename + " gitBlob=" + blob)
    versions = {}
    for record in packages:
        name = record["id"]
        version = record.get("version")
        digest = record.get("nugetContentSha512")
        expression = record.get("license")
        expected_expr = "MIT" if name.startswith("Microsoft.Data.Sqlite") else "Apache-2.0"
        require(expression == expected_expr, "SQLITE_PACKAGE_LICENSE_MISMATCH:" + name)
        require(re.fullmatch(r"[0-9]+(?:\.[0-9]+){2,3}", str(version)) is not None
                and re.fullmatch(r"[A-Za-z0-9+/]{86}==", str(digest)) is not None,
                "SQLITE_PACKAGE_PIN_INVALID:" + name)
        dep = deps.get(name, {})
        require(dep.get("resolved") == version and dep.get("contentHash") == digest,
                "SQLITE_NUGET_LOCK_HASH_OR_VERSION_MISMATCH:" + name)
        root = packages_dir / name.lower() / version
        require(root.is_dir() and not root.is_symlink(),
                "SQLITE_NUGET_PACKAGE_MISSING:" + name)
        metadata = parse_json(root / ".nupkg.metadata", "SQLITE_NUGET_PROVENANCE_MISSING:" + name)
        require(metadata.get("contentHash") == digest,
                "SQLITE_NUGET_PROVENANCE_HASH_MISMATCH:" + name)
        source = metadata.get("source")
        parsed = urlparse(source) if isinstance(source, str) else None
        require(parsed is not None and parsed.scheme == "https"
                and parsed.hostname in ("api.nuget.org", "www.nuget.org")
                and not parsed.username and not parsed.password,
                "SQLITE_NUGET_SOURCE_NOT_ATTESTED:" + name)
        # NuGet deliberately maintains TWO different SHA-512 hashes for signed
        # NUPKGs: unsigned CONTENT (packages.lock/.nupkg.metadata) and archive
        # BYTES (id.version.nupkg.sha512). Repository signatures change the latter.
        # Independently verify BOTH without comparing different hash domains.
        checksum = root / (name.lower() + "." + version + ".nupkg.sha512")
        nupkg = root / (name.lower() + "." + version + ".nupkg")
        require(checksum.is_file() and not checksum.is_symlink(),
                "SQLITE_NUGET_ARCHIVE_SHA512_MISSING:" + name)
        signed_archive_sha512 = checksum.read_text(encoding="utf-8-sig").strip()
        require(re.fullmatch(r"[A-Za-z0-9+/]{86}==", signed_archive_sha512) is not None
                and len(base64.b64decode(signed_archive_sha512, validate=True)) == 64,
                "SQLITE_NUGET_ARCHIVE_SHA512_INVALID:" + name)
        require(nupkg.is_file() and not nupkg.is_symlink(),
                "SQLITE_NUGET_ARCHIVE_BYTES_MISSING:" + name)
        observed_archive_sha512 = base64.b64encode(hashlib.sha512(nupkg.read_bytes()).digest()).decode("ascii")
        require(observed_archive_sha512 == signed_archive_sha512,
                "SQLITE_NUGET_ARCHIVE_BYTES_SHA512_MISMATCH:" + name)
        nuspec = root / (name.lower() + ".nuspec")
        require(nuspec.is_file(), "SQLITE_NUGET_NUSPEC_MISSING:" + name)
        try:
            xml = ET.parse(nuspec)
            metadata_node = next(n for n in xml.iter() if n.tag.split("}")[-1] == "metadata")
            fields = {n.tag.split("}")[-1]: n for n in metadata_node}
        except (ET.ParseError, StopIteration, OSError) as exc:
            raise LegalGateError("SQLITE_NUGET_NUSPEC_INVALID:" + name) from exc
        require(fields.get("id") is not None and fields["id"].text == name
                and fields.get("version") is not None and fields["version"].text == version,
                "SQLITE_NUGET_NUSPEC_ID_OR_VERSION_MISMATCH:" + name)
        license_node = fields.get("license")
        require(license_node is not None and license_node.attrib.get("type") == "expression"
                and (license_node.text or "").strip() == expected_expr,
                "SQLITE_NUGET_NUSPEC_LICENSE_MISMATCH:" + name)
        versions[name] = (version, root)
        print("SQLITE_NUGET_PROVENANCE_PASS id=" + name + " version=" + version
              + " contentSha512=" + digest + " archiveSha512=" + signed_archive_sha512
              + " source=" + source)
    published = [p for p in publish_dir.rglob("*") if p.is_file() and
                 ("sqlite" in p.name.casefold() or p.name.casefold() == "e_sqlite3.dll")]
    require({p.name for p in published} == set(BINARY_NAMES)
            and len(published) == len(BINARY_NAMES), "SQLITE_PUBLISH_BINARY_SET_MISMATCH")
    for record in binaries:
        name = record["name"]
        source_id = record.get("package")
        digest = record.get("sha256")
        require(source_id in versions and re.fullmatch(r"[a-f0-9]{64}", str(digest)) is not None,
                "SQLITE_BINARY_MANIFEST_INVALID:" + name)
        destination = publish_dir / name
        require(destination.is_file() and hash_file(destination) == digest,
                "SQLITE_PUBLISHED_BINARY_SHA256_MISMATCH:" + name)
        package_root = versions[source_id][1]
        candidates = [p for p in package_root.rglob("*") if p.is_file() and p.name == name]
        found = [p for p in candidates if hash_file(p) == digest]
        require(len(found) == 1, "SQLITE_NUGET_BINARY_PROVENANCE_MISMATCH:" + name)
        if name == "e_sqlite3.dll":
            require("runtimes/win-x64/native/" in found[0].relative_to(package_root).as_posix(),
                    "SQLITE_NATIVE_RID_MISMATCH")
        print("SQLITE_PUBLISHED_BINARY_VERIFIED name=" + name + " sha256=" + digest
              + " nupkg=" + source_id + "/" + versions[source_id][0]
              + " packagePath=" + found[0].relative_to(package_root).as_posix())
    print("SQLITE_LEGAL_ZIP_A_PASS components=5 packages=6 legalTexts=4 failClosed=true")


def main():
    parser = argparse.ArgumentParser()
    for field in ("manifest", "publish", "packages", "lock", "notices", "licenses"):
        parser.add_argument("--" + field, type=Path, required=True)
    args = parser.parse_args()
    try:
        validate(args.manifest, args.publish, args.packages, args.lock,
                 args.notices, args.licenses)
    except (LegalGateError, OSError, ValueError, KeyError, TypeError) as exc:
        print("SQLITE_LEGAL_ZIP_A_FAIL " + str(exc), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
