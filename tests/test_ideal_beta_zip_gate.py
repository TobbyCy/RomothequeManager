"""Negative canaries for the #149 Lot B extracted candidate gate.

All files are invented temporary bytes. No application is executed, no ROMs are read.
"""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from ideal_beta_zip_gate import MANIFEST, REQUIRED, BetaZipRejected, verify, sha256_file


class IdealBetaZipGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="rm-beta-manifest-fixture-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = "a" * 40
        self.names = set(REQUIRED) | {f"extras/filler-{i:02}.txt" for i in range(30)}
        for name in self.names:
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            if name == "BETA-IDEAL-NOTICE.txt":
                data = b"NOT A RELEASE; NO REAL ROM; phase=validate; LEGAL HOLD\n"
            else:
                data = ("synthetic-" + name).encode()
            path.write_bytes(data)
        provenance = {
            "package": "Microsoft.NETCore.App.Runtime.win-x64",
            "version": "10.0.12",
            "hostfxrProductVersion": "10.0.12",
            "licenseSha256": sha256_file(
                self.root / "licenses/dotnet-runtime/LICENSE.TXT"),
            "thirdPartyNoticesSha256": sha256_file(
                self.root / "licenses/dotnet-runtime/THIRD-PARTY-NOTICES.TXT"),
        }
        (self.root / "licenses/dotnet-runtime/PROVENANCE.json").write_text(
            json.dumps(provenance), encoding="utf-8")
        self.write_manifest()

    def write_manifest(self):
        self.rows = []
        for name in sorted(self.names):
            path = self.root / name
            self.rows.append({"path": name, "sizeBytes": path.stat().st_size,
                              "sha256": sha256_file(path)})
        self.manifest = {
            "schemaVersion": 1, "package": "RomothequeManager",
            "version": "v0.5.0-beta.1", "sourceCommit": self.source,
            "phase": "validate", "distribution": "LEGAL_AND_OWNER_PUBLISH_HOLD",
            "runtimeIdentifier": "win-x64",
            "runtimePack": "Microsoft.NETCore.App.Runtime.win-x64/10.0.12",
            "selfContained": True, "fixtureDataBundled": False,
            "files": self.rows,
        }
        self.flush()

    def flush(self):
        (self.root / MANIFEST).write_text(json.dumps(self.manifest), encoding="utf-8")

    def test_exact_fixture_passes(self):
        result = verify(self.root, self.source)
        self.assertEqual(result["status"], "BETA_IDEAL_ZIP_GATE_PASS")
        self.assertEqual(result["files"], len(self.names))

    def test_missing_manifest_refused(self):
        (self.root / MANIFEST).unlink()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_MANIFEST_MISSING"):
            verify(self.root, self.source)

    def test_missing_extracted_binary_refused(self):
        (self.root / "e_sqlite3.dll").unlink()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_MANIFEST_FILE_MISSING"):
            verify(self.root, self.source)

    def test_tampered_content_refused(self):
        (self.root / "e_sqlite3.dll").write_bytes(b"COMPROMISED")
        with self.assertRaisesRegex(BetaZipRejected, "HASH_MISMATCH"):
            verify(self.root, self.source)

    def test_extra_unlisted_file_refused(self):
        (self.root / "extra.txt").write_bytes(b"unlisted")
        with self.assertRaisesRegex(BetaZipRejected, "BETA_EXTRACT_FILE_SET_MISMATCH"):
            verify(self.root, self.source)

    def test_wrong_source_refused(self):
        with self.assertRaisesRegex(BetaZipRejected, "BETA_MANIFEST_CONTRACT_INVALID"):
            verify(self.root, "b" * 40)

    def test_publish_phase_refused(self):
        self.manifest["phase"] = "publish"
        self.flush()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_MANIFEST_CONTRACT_INVALID"):
            verify(self.root, self.source)

    def test_missing_legal_text_refused(self):
        (self.root / "licenses/SQLitePCLRaw-NOTICE.txt").unlink()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_MANIFEST_FILE_MISSING"):
            verify(self.root, self.source)

    def test_native_legal_provenance_forgery_refused(self):
        path = self.root / "licenses/dotnet-runtime/PROVENANCE.json"
        data = json.loads(path.read_text(encoding="utf-8"))
        data["licenseSha256"] = "b" * 64
        path.write_text(json.dumps(data), encoding="utf-8")
        self.write_manifest()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_RUNTIME_LEGAL_HASH_INVALID"):
            verify(self.root, self.source)

    def test_relative_parent_escape_refused(self):
        self.manifest["files"][0]["path"] = "../outside.txt"
        self.flush()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_MANIFEST_FILE_PATH"):
            verify(self.root, self.source)

    def test_duplicate_casefold_refused(self):
        self.manifest["files"].insert(0, dict(self.manifest["files"][0]))
        self.flush()
        with self.assertRaisesRegex(BetaZipRejected, "DUPLICATE_OR_UNSORTED"):
            verify(self.root, self.source)

    def test_forbidden_rom_content_refused(self):
        name = "extras/game.gba"
        (self.root / name).write_bytes(b"fake")
        self.names.add(name)
        self.write_manifest()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_FORBIDDEN_FILE"):
            verify(self.root, self.source)

    def test_fixture_data_directory_refused(self):
        name = "fixtures/demo.txt"
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"fake")
        self.names.add(name)
        self.write_manifest()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_FORBIDDEN_FILE"):
            verify(self.root, self.source)

    def test_wrong_runtime_version_refused(self):
        self.manifest["runtimePack"] = "Microsoft.NETCore.App.Runtime.win-x64/0.0.0"
        self.flush()
        with self.assertRaisesRegex(BetaZipRejected, "BETA_MANIFEST_CONTRACT_INVALID"):
            verify(self.root, self.source)


if __name__ == "__main__":
    unittest.main()
