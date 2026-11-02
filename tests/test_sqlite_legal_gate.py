"""Synthetic positive/negative SQLite provenance and legal gate fixtures only."""
import base64
import contextlib
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
import sqlite_legal_gate as gate


class SqliteLegalGateTests(unittest.TestCase):
    def setUp(self):
        self.t = tempfile.TemporaryDirectory(prefix="sqlite-legal-fixture-")
        self.addCleanup(self.t.cleanup)
        self.root = Path(self.t.name)
        self.publish = self.root / "publish"
        self.packages = self.root / "packages"
        self.licenses = self.root / "licenses"
        for folder in (self.publish, self.packages, self.licenses):
            folder.mkdir()
        self.manifest_file = self.root / "manifest.json"
        self.lock_file = self.root / "packages.lock.json"
        self.notices_file = self.root / "THIRD-PARTY-NOTICES.txt"
        self.m = {"schema": 1, "rid": "win-x64", "packages": [], "binaries": [], "legalFiles": []}
        self.lock = {"dependencies": {"net10.0": {}}}
        names_to_package = {
            "Microsoft.Data.Sqlite.dll": "Microsoft.Data.Sqlite.Core",
            "SQLitePCLRaw.batteries_v2.dll": "SQLitePCLRaw.bundle_e_sqlite3",
            "SQLitePCLRaw.core.dll": "SQLitePCLRaw.core",
            "SQLitePCLRaw.provider.e_sqlite3.dll": "SQLitePCLRaw.provider.e_sqlite3",
            "e_sqlite3.dll": "SQLitePCLRaw.lib.e_sqlite3",
        }
        for idx, name in enumerate(gate.PACKAGE_IDS):
            version = "10.0.14" if name.startswith("Microsoft.") else "2.1.13"
            digest = base64.b64encode(hashlib.sha512(name.encode()).digest()).decode()
            lic = "MIT" if name.startswith("Microsoft.") else "Apache-2.0"
            self.m["packages"].append({"id": name, "version": version,
                                       "nupkgSha512": digest, "license": lic})
            self.lock["dependencies"]["net10.0"][name] = {
                "resolved": version, "contentHash": digest}
            pkg = self.packages / name.lower() / version
            pkg.mkdir(parents=True)
            (pkg / ".nupkg.metadata").write_text(json.dumps({
                "contentHash": digest, "source": "https://api.nuget.org/v3/index.json"}), encoding="utf-8")
            (pkg / (name.lower() + "." + version + ".nupkg.sha512")).write_text(digest, encoding="utf-8")
            (pkg / (name.lower() + ".nuspec")).write_text(
                '<package><metadata><id>' + name + '</id><version>' + version +
                '</version><license type="expression">' + lic +
                '</license></metadata></package>', encoding="utf-8")
        for name, package in names_to_package.items():
            data = ("synthetic-only: " + name).encode()
            (self.publish / name).write_bytes(data)
            pkg = self.packages / package.lower() / ("10.0.14" if package.startswith("Microsoft.") else "2.1.13")
            sub = "runtimes/win-x64/native" if name == "e_sqlite3.dll" else "lib/net8.0"
            location = pkg / sub
            location.mkdir(parents=True, exist_ok=True)
            (location / name).write_bytes(data)
            self.m["binaries"].append({"name": name, "package": package,
                                        "sha256": hashlib.sha256(data).hexdigest()})
        for name in gate.LEGAL_FILES:
            data = ("Synthetic legal fixture: " + name + "\n" + ("legal test data\n" * 10)).encode()
            (self.licenses / name).write_bytes(data)
            self.m["legalFiles"].append({"file": name, "gitBlobSha": gate.git_blob_sha(data)})
        self.notice()
        self.save()

    def notice(self):
        self.notices_file.write_text("SQLITE-LEGAL-01\npublic domain\n" + "\n".join(
            [*gate.PACKAGE_IDS, *gate.LEGAL_FILES, "e_sqlite3.dll"]), encoding="utf-8")

    def save(self):
        self.manifest_file.write_text(json.dumps(self.m), encoding="utf-8")
        self.lock_file.write_text(json.dumps(self.lock), encoding="utf-8")

    def verify(self):
        with contextlib.redirect_stdout(io.StringIO()):
            return gate.validate(self.manifest_file, self.publish, self.packages,
                                 self.lock_file, self.notices_file, self.licenses)

    def reject(self, expected):
        with self.assertRaises(gate.LegalGateError) as caught:
            self.verify()
        self.assertIn(expected, str(caught.exception))

    def test_green_all_five_binaries_six_nupkgs_legal(self):
        self.assertIsNone(self.verify())

    def test_red_missing_binary(self):
        (self.publish / gate.BINARY_NAMES[0]).unlink()
        self.reject("SQLITE_PUBLISH_BINARY_SET_MISMATCH")

    def test_red_additional_sqlite_binary(self):
        (self.publish / "unexpected.SQLite.dll").write_bytes(b"unsafe")
        self.reject("SQLITE_PUBLISH_BINARY_SET_MISMATCH")

    def test_red_binary_sha256_drift(self):
        (self.publish / gate.BINARY_NAMES[0]).write_bytes(b"replaced")
        self.reject("SQLITE_PUBLISHED_BINARY_SHA256_MISMATCH")

    def test_red_binary_package_origin_wrong(self):
        cache = self.packages / "microsoft.data.sqlite.core" / "10.0.14" / "lib/net8.0/Microsoft.Data.Sqlite.dll"
        cache.write_bytes(b"another-package-payload")
        self.reject("SQLITE_NUGET_BINARY_PROVENANCE_MISMATCH")

    def test_red_missing_component_in_manifest(self):
        self.m["binaries"].pop()
        self.save()
        self.reject("SQLITE_BINARY_COMPONENT_SET_MISMATCH")

    def test_red_missing_legal_text(self):
        (self.licenses / gate.LEGAL_FILES[0]).unlink()
        self.reject("SQLITE_LEGAL_TEXT_MISSING")

    def test_red_tampered_legal_text(self):
        path = self.licenses / gate.LEGAL_FILES[1]
        path.write_bytes(path.read_bytes() + b"extra")
        self.reject("SQLITE_LEGAL_TEXT_HASH_MISMATCH")

    def test_red_notice_missing(self):
        self.notices_file.unlink()
        self.reject("SQLITE_NOTICE_BLOCK_MISSING")

    def test_red_notice_component_absent(self):
        content = self.notices_file.read_text(encoding="utf-8")
        self.notices_file.write_text(content.replace("SQLitePCLRaw.core", ""), encoding="utf-8")
        self.reject("SQLITE_NOTICE_REFERENCE_MISSING")

    def test_red_missing_nupkg_provenance(self):
        (self.packages / "sqlitepclraw.core/2.1.13/.nupkg.metadata").unlink()
        self.reject("SQLITE_NUGET_PROVENANCE_MISSING")

    def test_red_bad_nupkg_hash(self):
        target = self.packages / "sqlitepclraw.core/2.1.13/sqlitepclraw.core.2.1.13.nupkg.sha512"
        target.write_text("bad", encoding="utf-8")
        self.reject("SQLITE_NUGET_PACKAGE_SHA512_MISMATCH")

    def test_red_nupkg_untrusted_feed(self):
        path = self.packages / "sqlitepclraw.core/2.1.13/.nupkg.metadata"
        obj = json.loads(path.read_text(encoding="utf-8"))
        obj["source"] = "https://untrusted.example/v3/index.json"
        path.write_text(json.dumps(obj), encoding="utf-8")
        self.reject("SQLITE_NUGET_SOURCE_NOT_ATTESTED")

    def test_red_nuspec_license_mismatch(self):
        path = self.packages / "sqlitepclraw.core/2.1.13/sqlitepclraw.core.nuspec"
        path.write_text(path.read_text(encoding="utf-8").replace("Apache-2.0", "MIT"), encoding="utf-8")
        self.reject("SQLITE_NUGET_NUSPEC_LICENSE_MISMATCH")

    def test_red_locked_version_drift(self):
        self.lock["dependencies"]["net10.0"]["SQLitePCLRaw.core"]["resolved"] = "2.1.12"
        self.save()
        self.reject("SQLITE_NUGET_LOCK_HASH_OR_VERSION_MISMATCH")

    def test_red_nupkg_bytes_not_matching_locked_sha512(self):
        root = self.packages / "sqlitepclraw.core/2.1.13"
        (root / "sqlitepclraw.core.2.1.13.nupkg").write_bytes(b"malicious")
        self.reject("SQLITE_NUGET_NUPKG_BYTES_HASH_MISMATCH")

    def test_red_native_rid_mismatch(self):
        root = self.packages / "sqlitepclraw.lib.e_sqlite3/2.1.13"
        file = root / "runtimes/win-x64/native/e_sqlite3.dll"
        moved = root / "runtimes/linux-x64/native/e_sqlite3.dll"
        moved.parent.mkdir(parents=True)
        file.rename(moved)
        self.reject("SQLITE_NATIVE_RID_MISMATCH")


if __name__ == "__main__":
    unittest.main(verbosity=2)
