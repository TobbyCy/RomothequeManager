"""Synthetic policy canaries. No engine/ROM access and no dotnet application built."""
import contextlib
import copy
import io
import json
import os
import shutil
from pathlib import Path
import subprocess
import tempfile
import sys
import time
import unittest
from unittest.mock import patch

import ci_policy as p


class ClassificationTests(unittest.TestCase):
    def test_root_status_markdown_is_docs_only_and_exactly_allowlisted(self):
        self.assertEqual("DOCS_ONLY", p.classify([("A", "STATUS.md")]))
        self.assertEqual("INFRA_SECURITY_UNKNOWN", p.classify([("A", "STATUS.md.exe")]))
        self.assertEqual("INFRA_SECURITY_UNKNOWN", p.classify([("A", "STATUS.md/extra")]))
        self.assertTrue(p.re.fullmatch(p.DOC, "STATUS.md"))
        self.assertIsNone(p.re.fullmatch(p.DOC, "STATUS.md.exe"))
        self.assertIsNone(p.re.fullmatch(p.DOC, "STATUS.md/extra"))

    def test_routes(self):
        cases = {
            "DOCS_ONLY": ["README.md", "BRANDING.md", "LEGAL.md", "docs/developer/workflow.md", "docs/decisions/0001-demo.md"],
            "PS_ONLY": ["Modules/Inventory.psm1", "tests/Unit/Demo.Tests.ps1", "Export/ExportE1.psm1"],
            "DOTNET_ONLY": ["src/RomothequeManager.V2/Main.cs", "tests/dotnet/RomothequeManager.V2.Tests/Unit.cs"],
            "SHARED_OR_MIXED": ["contracts/demo.json", "Modules/StateCodec.psm1", "adapters/shared/Demo.cs"],
            "INFRA_SECURITY_UNKNOWN": [".github/workflows/ci.yml", "tests/ci_policy.py", "SECURITY.md", ".gitignore", "unexpected.cs", "src/other/Main.cs", "docs/demo.ps1", "tests/Invoke-CiPesterCoverage.ps1", "docs/CON.md", "src/RomothequeManager.V2/NUL.cs"],
        }
        for expected, paths in cases.items():
            for path in paths:
                for status in ("A", "M", "D", "T"):
                    with self.subTest(path=path, status=status):
                        self.assertEqual(expected, p.classify([(status, path)]))

    def test_mixed_and_doc_companions(self):
        self.assertEqual("SHARED_OR_MIXED", p.classify([("M", "Modules/Inventory.psm1"), ("A", "src/RomothequeManager.V2/Main.cs")]))
        self.assertEqual("PS_ONLY", p.classify([("M", "Modules/Inventory.psm1"), ("M", "README.md")]))
        self.assertEqual("INFRA_SECURITY_UNKNOWN", p.classify([("M", "README.md"), ("D", "unknown.bin")]))
        self.assertEqual("INFRA_SECURITY_UNKNOWN", p.classify([]))
        self.assertEqual("INFRA_SECURITY_UNKNOWN", p.classify([("M", "README.md")], True))

    def test_nul_and_rename_deletion_sides(self):
        self.assertEqual("INFRA_SECURITY_UNKNOWN", p.classify(p.diff_records(b"D\0unexpected.exe\0A\0docs/new.md\0")))
        for raw in (b"", b"M\0README.md", b"M\0", b"R100\0a\0b\0", b"U\0README.md\0", b"M\0../x\0", b"M\0doc\nname.md\0", b"M\0x\0M\0x\0"):
            with self.subTest(raw=raw), self.assertRaises((p.PolicyError, UnicodeError)):
                p.diff_records(raw)

    def test_plan_no_fake_dotnet_success(self):
        for category in ("DOCS_ONLY", "PS_ONLY", "DOTNET_ONLY", "SHARED_OR_MIXED", "INFRA_SECURITY_UNKNOWN"):
            jobs = p.job_plan(category, False)
            self.assertTrue({"policy", "ci-docs"} <= set(jobs["required"]))
            if category == "DOCS_ONLY":
                self.assertFalse(jobs["ps"] or jobs["dotnet"])
            if category == "DOTNET_ONLY":
                self.assertFalse(jobs["ps"])
                self.assertTrue(jobs["dotnet"])  # absence of project/tests must FAIL in the runner
        full = p.job_plan("INFRA_SECURITY_UNKNOWN", False)
        self.assertEqual("NOT_APPLICABLE_NO_DOTNET_PROJECT", full["not_required"]["ci-dotnet"])
        self.assertTrue(p.job_plan("INFRA_SECURITY_UNKNOWN", True)["dotnet"])
        self.assertFalse(p.job_plan("SHARED_OR_MIXED", False)["dotnet"])
        self.assertTrue(p.job_plan("SHARED_OR_MIXED", False, net_changed=True)["dotnet"])

    def test_required_skipped_failure_cancelled_missing_all_refused(self):
        for category in ("DOCS_ONLY", "PS_ONLY", "DOTNET_ONLY", "SHARED_OR_MIXED", "INFRA_SECURITY_UNKNOWN"):
            plan = {"error": None, "class": category, "jobs": p.job_plan(category, True)}
            needs = {job: {"result": "success" if job in plan["jobs"]["required"] else "skipped"} for job in p.NEEDS}
            needs["policy"]["outputs"] = {"policy": json.dumps(plan)}
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertTrue(p.final_gate(plan, needs))
            for job in plan["jobs"]["required"]:
                for result in ("skipped", "failure", "cancelled", None):
                    bad = copy.deepcopy(needs)
                    bad[job]["result"] = result
                    with self.subTest(category=category, job=job, result=result), self.assertRaises(p.PolicyError):
                        p.final_gate(plan, bad)
            bad = copy.deepcopy(needs)
            bad.pop("ci-docs")
            with self.assertRaises(p.PolicyError):
                p.final_gate(plan, bad)
            bad = copy.deepcopy(needs)
            bad["policy"]["outputs"]["policy"] = "{}"
            with self.assertRaises(p.PolicyError):
                p.final_gate(plan, bad)


class GitFixtureTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="rm-ci-policy-")
        self.root = Path(self.tmp.name).resolve()
        p.require(self.root.parent == Path(tempfile.gettempdir()).resolve() and not self.root.is_symlink(), "FIXTURE_BOUNDARY")
        self.run_git("init", "--quiet", "--initial-branch=main")
        self.run_git("config", "user.name", "Synthetic CI")
        self.run_git("config", "user.email", "ci@example.invalid")
        self.run_git("config", "commit.gpgsign", "false")
        self.run_git("config", "core.autocrlf", "false")
        (self.root / "README.md").write_text("# Synthetic\n", encoding="utf-8")
        self.base = self.save()

    def tearDown(self):
        p.require(self.root.parent == Path(tempfile.gettempdir()).resolve() and self.root.name.startswith("rm-ci-policy-") and not self.root.is_symlink(), "FIXTURE_CLEANUP_BOUNDARY")
        self.tmp.cleanup()

    def run_git(self, *args):
        return p.git(self.root, *args).decode().strip()

    def save(self):
        self.run_git("add", "--all")
        self.run_git("commit", "--quiet", "--allow-empty", "-m", "Synthetic change")
        return self.run_git("rev-parse", "HEAD")

    def change(self, path):
        file = self.root / path
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text("synthetic only\n", encoding="utf-8")
        return self.save()

    def context(self, head, event="pull_request"):
        if event == "push":
            return {"before": self.base, "after": head, "ref": "refs/heads/main", "forced": False}, {"GITHUB_SHA": head, "GITHUB_REF": "refs/heads/main"}
        return {"number": 7, "pull_request": {"base": {"sha": self.base, "ref": "main"}, "head": {"sha": head}}}, {"GITHUB_SHA": head, "GITHUB_REF": "refs/pull/7/head", "GITHUB_BASE_REF": "main"}

    def test_real_pr_push_docs_ps_net(self):
        for path, category in (("docs/guide.md", "DOCS_ONLY"), ("Modules/Inventory.psm1", "PS_ONLY"), ("src/RomothequeManager.V2/Main.cs", "DOTNET_ONLY")):
            self.run_git("checkout", "--quiet", "--detach", self.base)
            head = self.change(path)
            for event in ("pull_request", "push"):
                payload, env = self.context(head, event)
                plan = p.make_plan(self.root, event, payload, env)
                self.assertIsNone(plan["error"])
                self.assertEqual(category, plan["class"])
                print("ROUTING_FIXTURE_PASS " + json.dumps({"event": event, "class": category, "required": plan["jobs"]["required"], "not_required": plan["jobs"]["not_required"]}, sort_keys=True))

    def test_cumulative_pr_and_push_not_last_commit(self):
        self.change("Modules/Inventory.psm1")
        head = self.change("docs/guide.md")
        for event in ("pull_request", "push"):
            payload, env = self.context(head, event)
            self.assertEqual("PS_ONLY", p.make_plan(self.root, event, payload, env)["class"])

    def test_real_rename_cannot_hide_unknown(self):
        self.base = self.change("unknown.txt")
        self.run_git("mv", "unknown.txt", "new.md")
        head = self.save()
        payload, env = self.context(head)
        plan = p.make_plan(self.root, "pull_request", payload, env)
        self.assertEqual("INFRA_SECURITY_UNKNOWN", plan["class"])
        self.assertIn(["D", "unknown.txt"], plan["context"]["changes"])


    def test_initial_root_push_and_inconsistent_claim(self):
        payload = {"before": "0" * 40, "after": self.base,
                   "ref": "refs/heads/main", "forced": False}
        env = {"GITHUB_SHA": self.base, "GITHUB_REF": "refs/heads/main"}
        plan = p.make_plan(self.root, "push", payload, env)
        self.assertIsNone(plan["error"])
        self.assertEqual("INFRA_SECURITY_UNKNOWN", plan["class"])
        self.assertTrue(plan["context"]["root_push"])
        self.assertIn(["A", "README.md"], plan["context"]["changes"])
        later = self.change("docs/guide.md")
        payload["after"] = later
        env["GITHUB_SHA"] = later
        bad = p.make_plan(self.root, "push", payload, env)
        self.assertIn("ROOT_PUSH_NONROOT_COMMIT", bad["error"])

    def test_identity_rename_and_fork_fail_closed(self):
        head = self.change("docs/guide.md")
        payload, env = self.context(head)
        payload["repository"] = {"id": 4242, "full_name": "example/product"}
        env.update(GITHUB_ACTIONS="true", GITHUB_REPOSITORY="example/product",
                   GITHUB_REPOSITORY_ID="4242")
        self.assertIsNone(p.make_plan(self.root, "pull_request", payload, env)["error"])
        payload["repository"]["full_name"] = "example/other"
        self.assertIn("EVENT_REPOSITORY_MISMATCH",
                      p.make_plan(self.root, "pull_request", payload, env)["error"])
        payload["repository"]["full_name"] = "example/product"
        env["GITHUB_REPOSITORY_ID"] = "7"
        self.assertIn("EVENT_REPOSITORY_ID_MISMATCH",
                      p.make_plan(self.root, "pull_request", payload, env)["error"])
        env["GITHUB_REPOSITORY_ID"] = "4242"
        payload["pull_request"]["head"]["repo"] = {"id": 11}
        full = p.make_plan(self.root, "pull_request", payload, env)
        self.assertIsNone(full["error"])
        self.assertTrue(full["context"]["fork"])
        self.assertEqual("INFRA_SECURITY_UNKNOWN", full["class"])
        # An authorized rename is only an event/environment identity change.
        env["GITHUB_REPOSITORY"] = "example/renamed"
        payload["repository"]["full_name"] = "example/renamed"
        self.assertIsNone(p.make_plan(self.root, "pull_request", payload, env)["error"])

    def test_actual_plan_survives_github_json_transport(self):
        head = self.change("docs/guide.md")
        payload, env = self.context(head)
        plan = p.make_plan(self.root, "pull_request", payload, env)
        needs = {job: {"result": "success" if job in plan["jobs"]["required"] else "skipped"} for job in p.NEEDS}
        needs["policy"]["outputs"] = {"policy": json.dumps(plan)}
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertTrue(p.final_gate(plan, json.loads(json.dumps(needs))))

    def test_merge_refs_and_bad_events_fail_closed(self):
        head = self.change("docs/guide.md")
        payload, env = self.context(head)
        for broken in ({}, {"number": 7}, {"number": 7, "pull_request": {"base": {"sha": "0" * 40, "ref": "main"}, "head": {"sha": head}}}):
            plan = p.make_plan(self.root, "pull_request", broken, env)
            self.assertEqual("INFRA_SECURITY_UNKNOWN", plan["class"])
            self.assertTrue(plan["error"])
        env["GITHUB_SHA"] = self.base
        self.assertTrue(p.make_plan(self.root, "pull_request", payload, env)["error"])
        self.run_git("checkout", "--quiet", "--detach", self.base)
        self.run_git("merge", "--quiet", "--no-ff", head, "-m", "Synthetic merge")
        env["GITHUB_SHA"] = self.run_git("rev-parse", "HEAD")
        env["GITHUB_REF"] = "refs/pull/7/merge"
        self.assertFalse(p.make_plan(self.root, "pull_request", payload, env)["error"])
        payload["pull_request"]["base"]["sha"] = head
        self.assertTrue(p.make_plan(self.root, "pull_request", payload, env)["error"])

    def test_dispatch_empty_forced_and_missing_objects(self):
        payload, env = self.context(self.base, "push")
        self.assertTrue(p.make_plan(self.root, "push", payload, env)["error"])
        dispatch = p.make_plan(self.root, "workflow_dispatch", {}, env)
        self.assertIsNone(dispatch["error"])
        self.assertEqual("INFRA_SECURITY_UNKNOWN", dispatch["class"])
        head = self.change("docs/guide.md")
        payload, env = self.context(head, "push")
        payload["forced"] = True
        self.assertTrue(p.make_plan(self.root, "push", payload, env)["error"])
        payload["forced"] = False
        payload["before"] = "a" * 40
        self.assertTrue(p.make_plan(self.root, "push", payload, env)["error"])


class DocumentTests(unittest.TestCase):
    def test_windows_reserved_paths_without_creating_windows_device_files(self):
        for path in ("docs/CON.md", "docs/aux.md", "docs/COM1.md", "docs/LPT9.md", "docs/bad.md.", "docs/bad .md "):
            self.assertFalse(p.portable_path(path))
            with self.assertRaisesRegex(p.PolicyError, "WINDOWS_PATH_INVALID"):
                p.audit_entries(Path("."), {path: ("100644", "blob", "fake")}, [r".+"])
        self.assertTrue(p.portable_path("docs/CONTRIBUTING.md"))

    def test_security_rejects_paths_collisions_symlinks_size_and_secrets(self):
        with tempfile.TemporaryDirectory(prefix="rm-ci-policy-") as tmp:
            root = Path(tmp).resolve()
            p.require(root.parent == Path(tempfile.gettempdir()).resolve() and not root.is_symlink(), "FIXTURE_BOUNDARY")
            (root / "safe.md").write_text("synthetic\n", encoding="utf-8")
            entries = {"safe.md": ("100644", "blob", "safe")}
            with patch.object(p, "git", return_value=b"synthetic\n"):
                p.audit_entries(root, entries, [r"safe\.md"])
                for modes in (("120000", "blob", "safe"), ("160000", "commit", "safe")):
                    with self.assertRaises(p.PolicyError):
                        p.audit_entries(root, {"safe.md": modes}, [r"safe\.md"])
                with self.assertRaises(p.PolicyError):
                    p.audit_entries(root, entries, [r"other\.md"])
            for content in (("gh" + "p_" + "x" * 40).encode(), b"x" * 2000000):
                with patch.object(p, "git", return_value=content), self.assertRaises(p.PolicyError):
                    p.audit_entries(root, entries, [r"safe\.md"])
            (root / "SAFE.md").write_text("synthetic\n", encoding="utf-8")
            with patch.object(p, "git", return_value=b"synthetic\n"), self.assertRaises(p.PolicyError):
                p.audit_entries(root, {**entries, "SAFE.md": ("100644", "blob", "safe")}, [r"(?i)safe\.md"])

    def test_links_missing_deleted_escape_and_fences(self):
        entries = {"docs/guide.md": ("100644", "blob", "fake"), "README.md": ("100644", "blob", "fake")}
        for text in (b"[bad](missing.md)", b"[bad](../../escape.md)", b"[bad](/absolute.md)"):
            with patch.object(p, "git", return_value=text), self.assertRaises(p.PolicyError):
                p.check_links(Path("."), ["docs/guide.md"], entries)
        with patch.object(p, "git", return_value=b"[ok](../README.md)\n```text\n[example](not-a-link.md)\n```\n"):
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                p.check_links(Path("."), ["docs/guide.md"], entries)
            self.assertIn("count=1", output.getvalue())

    def test_shared_json_syntax_duplicate_keys_and_nonfinite_refused(self):
        with tempfile.TemporaryDirectory(prefix="rm-ci-policy-") as tmp:
            root = Path(tmp).resolve()
            p.require(root.parent == Path(tempfile.gettempdir()).resolve() and not root.is_symlink(), "FIXTURE_BOUNDARY")
            (root / "schema.json").write_text("{}", encoding="utf-8")
            entries = {"schema.json": ("100644", "blob", "fixture")}
            for data in (b'{"x":1,"x":2}', b'{"x":NaN}', b'{broken'):
                with patch.object(p, "git", return_value=data), self.assertRaises((p.PolicyError, ValueError)):
                    p.audit_entries(root, entries, [r"schema\.json"])
    def test_document_gate_positive_negative(self):
        entries = {k: ("100644", "blob", "fake") for k in p.REQUIRED_DOCS}
        with contextlib.redirect_stdout(io.StringIO()):
            p.check_currency([("M", "README.md")], entries)
        for changes in ([('M', 'Modules/Inventory.psm1')], [('M', 'tests/ci_policy.py')], [('D', 'docs/product-status.md'), ('M', 'tests/ci_policy.py')], [('M', 'src/RomothequeManager.V2/Main.cs')], [('M', 'contracts/demo.json')]):
            with self.assertRaises(p.PolicyError):
                p.check_currency(changes, entries)
        with contextlib.redirect_stdout(io.StringIO()):
            p.check_currency([('M', 'tests/ci_policy.py'), ('M', 'docs/product-status.md')], entries)
            p.check_currency([('M', 'Modules/Inventory.psm1'), ('M', 'docs/product-status.md'), ('M', 'CHANGELOG.md')], entries)
        for name in p.REQUIRED_DOCS:
            with self.assertRaises(p.PolicyError):
                p.check_currency([('M', 'README.md')], {k: v for k, v in entries.items() if k != name})

    def test_policy_uses_existing_allowlist(self):
        root = Path(__file__).resolve().parent.parent
        rules, _ = p.path_rules(root)
        for path in ("LICENSE", "BRANDING.md", "LEGAL.md", "README.md", "STATUS.md", "tests/ci_policy.py", "tests/test_ci_policy.py", "tests/Invoke-CiE1PreflightSynthetic.ps1"):
            self.assertTrue(any(__import__('re').fullmatch(r, path) for r in rules))
        license_text = (root / "LICENSE").read_text(encoding="utf-8")
        self.assertTrue(license_text.startswith("MIT License\n\nCopyright (c) 2026 Cyril Tobler\n\n"))
        self.assertTrue(license_text.endswith("OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\nSOFTWARE.\n"))
        self.assertEqual("INFRA_SECURITY_UNKNOWN", p.classify([("A", "LICENSE")]))
        for path in (".github/workflows/unreviewed.yml", "tests/ci_policy_extra.py", "docs/secret.exe", "ROMS/demo.gba", "SECURITY.txt", "LICENSE.txt", "LICENSE.bak", "license", "STATUS.md.bak", "STATUS.md/extra", "BRANDING.md.bak", "LEGAL.txt", "LEGAL.md/evil", "tests/Invoke-CiE1PreflightSynthetic.ps1.bak", "tests/Invoke-CiE1PreflightSynthetic.ps1/extra"):
            self.assertFalse(any(__import__('re').fullmatch(r, path) for r in rules))

    def test_ux_v2_mockup_index_only_no_image_or_archive(self):
        import re
        root = Path(__file__).resolve().parent.parent
        rules, _ = p.path_rules(root)
        index = "docs/design/mockups/v2/README.md"
        self.assertEqual("DOCS_ONLY", p.classify([("A", index)]))
        self.assertTrue(any(re.fullmatch(rule, index) for rule in rules))
        with patch.object(p, "git", return_value=b"synthetic reference index\n"):
            p.audit_entries(root, {index: ("100644", "blob", "f" * 40)}, rules)
        denied = (
            "docs/design/mockups/v2/01-dashboard.jpeg",
            "docs/design/mockups/v2/01-dashboard.png",
            "docs/design/mockups/v2/originals.zip",
            "docs/design/mockups/v2/manifest.json",
            "docs/design/mockups/v2/other.md",
            "docs/design/mockups/v2/README.md.bak",
            "docs/design/mockups/v2/README.md/extra",
            "docs/design/mockups/v2/nested/README.md",
            "docs/design/mockups/v2/../v2/README.md",
            "docs/design/mockups/v3/README.md",
            "Docs/design/mockups/v2/README.md",
            "docs/design/mockups/v2/readme.md",
        )
        for path in denied:
            with self.subTest(denied=path):
                self.assertFalse(any(re.fullmatch(rule, path) for rule in rules))
                with self.assertRaisesRegex(p.PolicyError, "TRACKED_PATH_NOT_ALLOWED"):
                    p.audit_entries(root, {path: ("100644", "blob", "f" * 40)}, rules)

    def test_brand01_exact_paths_and_binary_negative_canaries(self):
        import re
        root = Path(__file__).resolve().parent.parent
        rules, _ = p.path_rules(root)
        allowed = ("assets/branding/RM_V2_Horizontal_Fond_Sombre.png",
                   "assets/branding/README.md", *p.VISUAL67_ASSETS)
        for path in allowed:
            self.assertTrue(any(re.fullmatch(rule, path) for rule in rules), path)
        rejected = (
            "assets/branding/other.png",
            "assets/branding/logo.svg",
            "assets/branding/remote.svg",
            "assets/branding/nested/RM_V2_Horizontal_Fond_Sombre.png",
            "assets/branding/RM_V2_Horizontal_Fond_Sombre.png.svg",
            "assets/branding/RM_V2_Horizontal_Fond_Sombre.png.bak",
            "assets/branding/README.md.bak",
            "Assets/branding/RM_V2_Horizontal_Fond_Sombre.png",
            "assets/branding/README.md/extra",
            "assets/branding/../branding/RM_V2_Horizontal_Fond_Sombre.png",
        )
        for path in rejected:
            with self.subTest(rejected=path):
                self.assertFalse(any(re.fullmatch(rule, path) for rule in rules), path)
                with self.assertRaisesRegex(p.PolicyError, "TRACKED_PATH_NOT_ALLOWED"):
                    p.audit_entries(root, {path: ("100644", "blob", "f" * 40)}, rules)

        # Pin to the unchanged public-authorized source; content-type spoofing,
        # rewritten SVG-in-PNG, appended content and modified dimensions fail.
        original = (root / p.BRAND01_PNG).read_bytes()
        self.assertTrue(p.verify_brand01_banner(p.BRAND01_GIT_BLOB_SHA, original))
        with self.assertRaisesRegex(p.PolicyError, "BRAND01_UNAPPROVED_IMAGE_BLOB"):
            p.verify_brand01_banner("f" * 40, original)
        with self.assertRaisesRegex(p.PolicyError, "BRAND01_UNEXPECTED_IMAGE_SIZE"):
            p.verify_brand01_banner(p.BRAND01_GIT_BLOB_SHA, original[:-1])
        malformed = b"<svg onload='alert(1)'/>".ljust(len(original), b"x")
        with self.assertRaisesRegex(p.PolicyError, "BRAND01_NOT_PNG"):
            p.verify_brand01_banner(p.BRAND01_GIT_BLOB_SHA, malformed)
        wrong_dimensions = bytearray(original)
        wrong_dimensions[16:20] = (1).to_bytes(4, "big")
        with self.assertRaisesRegex(p.PolicyError, "BRAND01_DIMENSIONS_INVALID"):
            p.verify_brand01_banner(p.BRAND01_GIT_BLOB_SHA, bytes(wrong_dimensions))

        # VISUAL67: original Pilotage bytes only, no alternate image or extension.
        for asset_path, (expected, length) in p.VISUAL67_ASSETS.items():
            # Compare immutable Git blob bytes, not Windows checkout CRLF-converted SVG files.
            asset_data = p.git(root, "cat-file", "blob", expected)
            self.assertEqual(length, len(asset_data))
            self.assertTrue(p.verify_visual67_asset(asset_path, expected, asset_data))
            with self.assertRaisesRegex(p.PolicyError, "VISUAL67_UNAPPROVED_BLOB"):
                p.verify_visual67_asset(asset_path, "f" * 40, asset_data)
            with self.assertRaisesRegex(p.PolicyError, "VISUAL67_UNEXPECTED_SIZE"):
                p.verify_visual67_asset(asset_path, expected, asset_data[:-1])
            with self.assertRaisesRegex(p.PolicyError, "VISUAL67_UNLISTED_ASSET"):
                p.verify_visual67_asset("assets/branding/other.ico", expected, asset_data)
            if asset_path.endswith(".ico"):
                altered = b"FAKE" + asset_data[4:]
                with self.assertRaisesRegex(p.PolicyError, "VISUAL67_INVALID_ICO"):
                    p.verify_visual67_asset(asset_path, expected, altered)


    def test_public_issue_form_allowlist_and_unexpected_paths_fail_closed(self):
        root = Path(__file__).resolve().parent.parent
        rules, _ = p.path_rules(root)
        permitted = ("bug", "config", "docs", "feature", "question")
        for name in permitted:
            path = ".github/ISSUE_TEMPLATE/" + name + ".yml"
            with self.subTest(allowed=path):
                self.assertTrue(any(__import__("re").fullmatch(rule, path) for rule in rules))
        denied = (
            ".github/ISSUE_TEMPLATE/private.yml",
            ".github/ISSUE_TEMPLATE/internal.yml",
            ".github/ISSUE_TEMPLATE/docs.yaml",
            ".github/ISSUE_TEMPLATE/bug.yml.bak",
            ".github/ISSUE_TEMPLATE/../private.yml",
            ".github/ISSUE_TEMPLATE/question.yml/extra",
        )
        for path in denied:
            with self.subTest(denied=path):
                self.assertFalse(any(__import__("re").fullmatch(rule, path) for rule in rules))
                with self.assertRaisesRegex(p.PolicyError, "TRACKED_PATH_NOT_ALLOWED"):
                    p.audit_entries(root, {path: ("100644", "blob", "f" * 40)}, rules)


    def test_dotnet_missing_project_never_green(self):
        with patch.object(p, "tree", return_value={"README.md": ("100644", "blob", "fake")}):
            with self.assertRaisesRegex(p.PolicyError, "DOTNET_PROJECT_OR_TEST_MISSING"):
                p.dotnet(Path("."))

    def test_dotnet_mock_execution_and_zero_tests_refused(self):
        # No SDK/project is built: assert the real runner's commands and TRX checks via mocks.
        project = "src/RomothequeManager.V2/App.csproj"
        test = "tests/dotnet/RomothequeManager.V2.Tests/App.Tests.csproj"
        entries = {p: ("100644", "blob", p) for p in (project, test,
                   "src/RomothequeManager.V2/packages.lock.json", "tests/dotnet/RomothequeManager.V2.Tests/packages.lock.json")}
        def blob(root, *args):
            return b"<Project><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>" if args[-1] == test else b"<Project/>"
        for executed in (1, 0):
            commands = []
            def fake_run(args, **kwargs):
                commands.append(args)
                if args[1] == "--version":
                    return subprocess.CompletedProcess(args, 0, stdout="10.0.401\n", stderr="")
                if args[1] == "test":
                    dest = Path(args[args.index("--results-directory") + 1])
                    dest.mkdir(parents=True)
                    (dest / "tests.trx").write_text('<TestRun><ResultSummary><Counters total="' + str(executed) + '" passed="' + str(executed) + '" executed="' + str(executed) + '" failed="0"/></ResultSummary></TestRun>', encoding="utf-8")
            with patch.object(p, "tree", return_value=entries), patch.object(p, "git", side_effect=blob), patch.object(p, "verify_sdk_pin", return_value="10.0.401"), patch.object(p.subprocess, "run", side_effect=fake_run):
                if executed:
                    with contextlib.redirect_stdout(io.StringIO()):
                        p.dotnet(Path("."))
                else:
                    with self.assertRaisesRegex(p.PolicyError, "DOTNET_ZERO_OR_FAILED_TESTS"):
                        p.dotnet(Path("."))
            self.assertEqual(1, sum(c[1] == "--version" for c in commands))
            self.assertEqual(2, sum(c[1] == "restore" and "--locked-mode" in c for c in commands))
            self.assertEqual(2, sum(c[1] == "build" and "-p:EnableNETAnalyzers=true" in c for c in commands))
            self.assertTrue(all(c[0] == "dotnet" for c in commands))


class WorkflowTests(unittest.TestCase):
    def test_stab03a_ps51_unicode_sources_have_utf8_bom(self):
        # GitHub-stored bytes: PS5.1 must never interpret UTF-8 literals as ACP.
        root = Path(__file__).resolve().parent.parent
        paths = (
            "tests/Export-SyntheticCanonicalCatalogue.ps1",
            "tests/Export-SyntheticCatalogue.ps1",
            "tests/Invoke-CiCanonicalCatalogue.ps1",
            "tests/Invoke-CiCatalogueProjection.ps1",
        )
        for name in paths:
            with self.subTest(file=name):
                raw = (root / name).read_bytes()
                self.assertTrue(raw.startswith(bytes.fromhex("efbbbf")), name)
                decoded = raw.decode("utf-8-sig", errors="strict")
                self.assertNotIn("\u00c3\u2030", decoded)
        canonical = (root / paths[0]).read_bytes().decode("utf-8-sig")
        self.assertIn("\u00c9toile des disques", canonical)

    def test_stab03a_exact_path_allowlist(self):
        # Real CI-0 allowlist, not an alternate permissive matcher.
        import re
        root = Path(__file__).resolve().parent.parent
        rules, _ = p.path_rules(root)
        permitted = ["global.json", "tests/Invoke-CiE1PreflightSynthetic.ps1", "tests/edge-browser-smoke.cjs", "tests/Export-SyntheticUiProjection.ps1", "tests/fixtures/TEST_MATRIX_S3.md", "tests/fixtures/SyntheticLibrarySnapshots.ps1", "tests/fixtures/UiProjectionVectors.ps1", "tests/fixtures/CatalogueT3CanonicalSample.json", "tests/Export-SyntheticCanonicalCatalogue.ps1", "tests/catalogue_t3_consumer.test.cjs", "adapters/shared/catalogue-t3-consumer.js", "prototype/ui11/t4-catalogue.test.cjs"]
        forbidden = ["global.json.bak", "GLOBAL.json", "tests/Invoke-CiE1PreflightSynthetic.psm1", "tests/edge-browser-smoke.cjs.bak", "tests/EDGE-browser-smoke.cjs", "tests/fixtures/unapproved.json", "tests/Export-SyntheticUnverified.ps1", "tests/untrusted.test.cjs", "adapters/shared/unapproved.js", "prototype/ui11/unapproved.html"]
        for path in permitted:
            with self.subTest(permitted=path):
                self.assertTrue(any(re.fullmatch(rule, path) for rule in rules), path)
        for path in forbidden:
            with self.subTest(forbidden=path):
                self.assertFalse(any(re.fullmatch(rule, path) for rule in rules), path)


    def test_v2_01a_sdk_exact_pin_negative_canaries(self):
        import tempfile
        with tempfile.TemporaryDirectory(prefix="rm-sdk-pin-") as tmp:
            root = Path(tmp)
            target = root / "global.json"
            with self.assertRaisesRegex(p.PolicyError, "DOTNET_GLOBAL_JSON_REQUIRED"):
                p.verify_sdk_pin(root)
            correct = {"sdk": {"version": "10.0.401", "rollForward": "disable",
                                "allowPrerelease": False}}
            target.write_text(json.dumps(correct), encoding="utf-8")
            self.assertEqual("10.0.401", p.verify_sdk_pin(root))
            variants = [
                {"sdk": {**correct["sdk"], "version": "10.0.100"}},
                {"sdk": {**correct["sdk"], "rollForward": "latestFeature"}},
                {"sdk": {**correct["sdk"], "allowPrerelease": True}},
                {"sdk": {**correct["sdk"], "extra": "unauthorized"}}
            ]
            for variant in variants:
                target.write_text(json.dumps(variant), encoding="utf-8")
                with self.assertRaisesRegex(p.PolicyError, "DOTNET_SDK_PIN_INVALID"):
                    p.verify_sdk_pin(root)
            target.unlink()

    def test_v2_01c_zip_is_verified_and_distribution_stays_blocked(self):
        root = Path(__file__).resolve().parent.parent
        workflow = (root / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        for required in (
            "RomothequeManager-V2-01C-win-x64.zip",
            "Get-FileHash -LiteralPath $zip -Algorithm SHA256",
            "Expand-Archive -LiteralPath $zip",
            "V2C_ARCHIVE_FILE_SET_MISMATCH",
            "V2C_FORBIDDEN_PACKAGE_FILE",
            "V2C_EXTRACTED_WINDOW_FAILED",
            "views=Dashboard,Collection,GameDetail,Libraries fixtures=6 synthetic=1",
            "V2C_ZIP_VERIFIED_PASS",
            "POC_ZIP_QUARANTINED_NOT_UPLOADED",
            "HOLD_OWNER_GO",
        ):
            self.assertIn(required, workflow)
        self.assertNotIn("actions/upload-artifact@", workflow)
        self.assertNotIn("gh release", workflow)
        self.assertNotIn("v2-01a-win-x64-candidate", workflow)

    def test_dist_zip_01_manual_publication_is_fail_closed(self):
        root = Path(__file__).resolve().parent.parent
        workflow = (root / ".github/workflows/dist-zip.yml").read_text(encoding="utf-8")
        script = (root / "scripts/Build-DistZip.ps1").read_text(encoding="utf-8")
        self.assertIn("workflow_dispatch:", workflow)
        self.assertNotIn("\n  push:", workflow)
        self.assertNotIn("\n  pull_request:", workflow)
        self.assertNotIn("\n  schedule:", workflow)
        self.assertNotIn("actions/upload-artifact@", workflow)
        self.assertIn("github.ref == 'refs/heads/main' && github.actor == 'TobbyCy'", workflow)
        self.assertIn("inputs.phase == 'publish'", workflow)
        self.assertIn("vars.DIST_ZIP_FINAL_GO", workflow)
        self.assertIn("vars.DIST_ZIP_APPROVED_SOURCE_SHA", workflow)
        self.assertIn("vars.DIST_ZIP_APPROVED_SHA256", workflow)
        self.assertIn("vars.DIST_ZIP_APPROVED_TAG", workflow)
        self.assertIn("DIST_APPROVED_ZIP_HASH_MISMATCH", workflow)
        self.assertIn("gh release create", workflow)
        self.assertIn("--draft --prerelease --latest=false", workflow)
        self.assertIn("gh release download", workflow)
        self.assertIn("DIST_DRAFT_ASSETS_REDOWNLOADED_VERIFIED", workflow)
        self.assertIn("gh release edit", workflow)
        self.assertIn("--draft=false --prerelease", workflow)
        self.assertIn("packaging/THIRD-PARTY-NOTICES.txt", script)
        self.assertIn("docs/developer/lic-zip-01-inventory.md", script)
        self.assertIn("DIST_REQUIRED_NOTICE_OR_LEGAL_FILE_MISSING", script)
        self.assertIn("DIST_REQUIRED_FULL_LICENSE_MISSING", script)
        self.assertIn("DIST_EXACT_WINDOWS_RUNTIME_LEGAL_ASSETS_MISSING", script)
        self.assertIn("DIST_RUNTIME_LEGAL_ASSETS_VERIFIED", script)
        self.assertIn("DIST_LICENCE_VALIDATION_HOLD", script)
        self.assertIn("[ValidateSet('validate', 'publish')]", script)
        self.assertIn("if ($Phase -ceq 'publish' -and -not $legalApproved)", script)
        self.assertIn("DIST_CANDIDATE_VERIFIED_LEGAL_HOLD", script)
        self.assertIn("('legalStatus=' + $legalStatus", script)
        self.assertNotIn("if ($inventory -cnotmatch '(?m)^LIC_ZIP_01_BINARY_DISTRIBUTION_APPROVED$')", script)
        self.assertIn("-Phase $env:DIST_PHASE", workflow)
        self.assertIn("DIST_LEGAL_APPROVAL_MISSING_BEFORE_UPLOAD", workflow)
        self.assertIn("steps.bundle.outputs.legalStatus", workflow)
        self.assertIn("DIST_ZIP_SHA256_MISMATCH", script)
        self.assertIn("DIST_ARCHIVE_REPRODUCIBILITY_FAILED", script)
        self.assertIn("DIST_EXTRACTED_FILE_SET_MISMATCH", script)
        self.assertIn("DIST_EXTRACTED_FILE_SHA256_MISMATCH", script)
        self.assertIn("DIST_FORBIDDEN_CONTENT", script)
        self.assertIn("DIST_EXTRACTED_SMOKE_FAILED", script)
        self.assertIn("DIST_ZIP_VALIDATED_PASS", script)
        self.assertIn("DIST_PUBLISH_FILE_MANIFEST_BEGIN", script)
        self.assertIn("PUBLISH_FILE_MANIFEST_BEGIN", (root / ".github/workflows/ci.yml").read_text(encoding="utf-8"))
        self.assertIn("DOTNET_RUNTIME_LEGAL_DISCOVERY_BEGIN", (root / ".github/workflows/ci.yml").read_text(encoding="utf-8"))
        self.assertNotIn("gh release", script)
        self.assertNotIn("upload-artifact", script)

    def test_dist_remote_tag_guard_negative_canaries(self):
        # Static policy canaries for the manual publish step; no API call or release.
        workflow = (Path(__file__).resolve().parent.parent / ".github/workflows/dist-zip.yml").read_text(encoding="utf-8")

        def guarded(candidate):
            required = (
                "Authorization = ('Bearer ' + $env:GH_TOKEN)",
                "Invoke-WebRequest -Uri $uri -Headers $headers -SkipHttpErrorCheck",
                "if ($status -eq 404 -and $AllowNotFound) { return $null }",
                "if ($status -ne 200) { throw ('DIST_REMOTE_TAG_API_HTTP_FAILED_' + $status) }",
                "DIST_REMOTE_TAG_API_TRANSPORT_FAILED",
                "Get-DistRemoteGitObject -ApiPath $tagPath -AllowNotFound $true",
                "if ($null -ne $preexistingTag) { throw 'DIST_REMOTE_TAG_ALREADY_EXISTS_BEFORE_UPLOAD' }",
                "$createBody = @{ ref = $refName; sha = $env:SOURCE_SHA } | ConvertTo-Json -Compress",
                "Invoke-WebRequest -Uri $createUri -Method Post -Headers $createHeaders",
                "if ($createStatus -ne 201) { throw ('DIST_REMOTE_TAG_CREATE_HTTP_FAILED_' + $createStatus) }",
                "DIST_REMOTE_TAG_CREATE_TRANSPORT_FAILED",
                "DIST_REMOTE_TAG_CREATE_RESPONSE_INVALID",
                "if ($null -eq $createdRef -or $createdRef.ref -cne $refName -or $createdRef.object.type -cne 'commit' -or $createdRef.object.sha -cne $env:SOURCE_SHA) { throw 'DIST_REMOTE_TAG_CREATED_REF_MISMATCH' }",
                "if ($null -eq $readbackRef -or $readbackRef.ref -cne $refName -or $readbackRef.object.type -cne 'commit' -or $readbackRef.object.sha -cne $env:SOURCE_SHA) { throw 'DIST_REMOTE_TAG_CREATE_READBACK_MISMATCH' }",
                "DIST_REMOTE_TAG_CREATED_VERIFIED",
                "--verify-tag --target $env:SOURCE_SHA",
                "Get-DistRemoteGitObject -ApiPath $tagPath",
                "Get-DistRemoteGitObject -ApiPath ('git/tags/' + $tagObject.sha)",
                "if ($resolvedCommit -cne $env:SOURCE_SHA) { throw 'DIST_REMOTE_TAG_TARGET_MISMATCH' }",
                "DIST_REMOTE_TAG_TARGET_VERIFIED",
            )
            if not all(value in candidate for value in required):
                return False
            return (
                candidate.index("DIST_REMOTE_TAG_ALREADY_EXISTS_BEFORE_UPLOAD")
                < candidate.index("DIST_REMOTE_TAG_CREATED_VERIFIED")
                < candidate.index("gh release create")
                < candidate.index("DIST_REMOTE_TAG_TARGET_VERIFIED")
                < candidate.index("gh release edit")
            )

        self.assertTrue(guarded(workflow))  # Absent tag, then exact source SHA: accepted route.
        mutations = {
            "preexisting_wrong_target_must_reject": workflow.replace(
                "if ($null -ne $preexistingTag) { throw 'DIST_REMOTE_TAG_ALREADY_EXISTS_BEFORE_UPLOAD' }",
                "Write-Host 'unsafe preexisting tag accepted'", 1),
            "remote_api_error_must_reject": workflow.replace(
                "if ($status -ne 200) { throw ('DIST_REMOTE_TAG_API_HTTP_FAILED_' + $status) }",
                "Write-Host 'unsafe API error accepted'", 1),
            "remote_transport_error_must_reject": workflow.replace(
                "DIST_REMOTE_TAG_API_TRANSPORT_FAILED", "DIST_REMOTE_TAG_API_IGNORED", 1),
            "no_explicit_remote_tag_create": workflow.replace(
                "Invoke-WebRequest -Uri $createUri -Method Post -Headers $createHeaders",
                "Write-Host 'unsafe no tag create'", 1),
            "wrong_sha_remote_tag_create": workflow.replace(
                "$createBody = @{ ref = $refName; sha = $env:SOURCE_SHA } | ConvertTo-Json -Compress",
                "$createBody = @{ ref = $refName; sha = '0000000000000000000000000000000000000000' } | ConvertTo-Json -Compress", 1),
            "create_201_gate_removed": workflow.replace(
                "if ($createStatus -ne 201) { throw ('DIST_REMOTE_TAG_CREATE_HTTP_FAILED_' + $createStatus) }",
                "Write-Host 'unsafe non-201 accepted'", 1),
            "create_transport_error_ignored": workflow.replace(
                "DIST_REMOTE_TAG_CREATE_TRANSPORT_FAILED", "DIST_REMOTE_TAG_CREATE_TRANSPORT_IGNORED", 1),
            "create_response_not_checked": workflow.replace(
                "if ($null -eq $createdRef -or $createdRef.ref -cne $refName -or $createdRef.object.type -cne 'commit' -or $createdRef.object.sha -cne $env:SOURCE_SHA) { throw 'DIST_REMOTE_TAG_CREATED_REF_MISMATCH' }",
                "Write-Host 'unsafe create response accepted'", 1),
            "create_readback_not_checked": workflow.replace(
                "if ($null -eq $readbackRef -or $readbackRef.ref -cne $refName -or $readbackRef.object.type -cne 'commit' -or $readbackRef.object.sha -cne $env:SOURCE_SHA) { throw 'DIST_REMOTE_TAG_CREATE_READBACK_MISMATCH' }",
                "Write-Host 'unsafe readback accepted'", 1),
            "tag_must_preexist_for_gh_release": workflow.replace(
                "--verify-tag --target $env:SOURCE_SHA",
                "--target $env:SOURCE_SHA", 1),
            "tag_created_after_release_upload": workflow.replace(
                "Write-Host ('DIST_REMOTE_TAG_CREATED_VERIFIED sha=' + $env:SOURCE_SHA)",
                "Write-Host ('DIST_REMOTE_TAG_CREATED_LATE sha=' + $env:SOURCE_SHA)", 1),
            "annotated_tag_must_be_dereferenced": workflow.replace(
                "Get-DistRemoteGitObject -ApiPath ('git/tags/' + $tagObject.sha)",
                "$null", 1),
            "postdraft_wrong_target_must_reject": workflow.replace(
                "if ($resolvedCommit -cne $env:SOURCE_SHA) { throw 'DIST_REMOTE_TAG_TARGET_MISMATCH' }",
                "Write-Host 'unsafe tag target accepted'", 1),
        }
        for name, mutant in mutations.items():
            with self.subTest(name=name):
                self.assertFalse(guarded(mutant))

    def test_val01_edge_is_required_and_fail_closed(self):
        root = Path(__file__).resolve().parent.parent
        workflow = (root / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        script = (root / "tests/edge-browser-smoke.cjs").read_text(encoding="utf-8")
        self.assertIn("VAL-01 real Microsoft Edge headless browser", workflow)
        self.assertIn(r"node .\tests\edge-browser-smoke.cjs", workflow)
        self.assertIn("if ($LASTEXITCODE -ne 0) { throw 'EDGE_BROWSER_SMOKE_FAILED' }", workflow)
        self.assertIn("BROWSER_REAL_FAIL", script)
        self.assertIn("BROWSER_REAL_PASS", script)
        self.assertIn("Browser.getVersion", script)
        self.assertIn("Input.dispatchMouseEvent", script)
        self.assertIn("Network.setBlockedURLs", script)


    def test_library_ux02_workflow_is_branch_only_temporary_and_fail_closed(self):
        root = Path(__file__).resolve().parent.parent
        workflow = (root / ".github/workflows/library-ux02-proof.yml").read_text(encoding="utf-8")
        self.assertIn("branches: [dev3/library-ux-02]", workflow)
        self.assertIn("permissions:\n  contents: read", workflow)
        self.assertIn("github.actor == 'TobbyCy'", workflow)
        self.assertIn("LIBRARY_UX02_WINDOWS_CAPTURE_PASS", workflow)
        self.assertIn("LIBRARY_UX02_ZIP_VERIFIED_PASS", workflow)
        self.assertEqual(1, workflow.count("actions/upload-artifact@"))
        self.assertIn("retention-days: 3", workflow)
        self.assertIn("if-no-files-found: error", workflow)
        for bad in ("pull_request_target:", "workflow_run:", "gh release",
                    "contents: write", "secrets.", "continue-on-error:"):
            self.assertNotIn(bad, workflow)
        rules, _ = p.path_rules(root)
        self.assertTrue(any(p.re.fullmatch(rule, ".github/workflows/library-ux02-proof.yml") for rule in rules))
        self.assertFalse(any(p.re.fullmatch(rule, ".github/workflows/library-ux02-other.yml") for rule in rules))

    def test_next04_ux_workflow_is_branch_only_and_temporary(self):
        root = Path(__file__).resolve().parent.parent
        workflow = (root / ".github/workflows/next04-ux-proof.yml").read_text(encoding="utf-8")
        self.assertIn("branches: [dev3/next04-esde-ux, dev3/portable-packaging-101]", workflow)
        self.assertNotIn("branches: [main]", workflow)
        self.assertIn("PORTABLE101_ZIP_VERIFIED_PASS", workflow)
        self.assertIn("PublishSingleFile=true", workflow)
        self.assertIn("IncludeNativeLibrariesForSelfExtract=true", workflow)
        self.assertIn("permissions:\n  contents: read", workflow)
        self.assertIn("github.actor == 'TobbyCy'", workflow)
        self.assertIn("NEXT04_GUI_WINDOWS_CAPTURE_PASS", workflow)
        self.assertIn("NEXT04_STANDARD_BUILD_WITHOUT_CI_HARNESS_PASS", workflow)
        self.assertIn("NEXT04_ZIP_VERIFIED_PASS", workflow)
        self.assertIn("NEXT04_DEMO_ZIP_VERIFIED_PASS", workflow)
        self.assertIn("SOURCE-AND-HASHES.json", workflow)
        self.assertIn("requiresNodeForUser=false", workflow)
        self.assertIn("MALFORMED", workflow)
        self.assertIn("LONG", workflow)
        self.assertEqual(1, workflow.count("actions/upload-artifact@"))
        self.assertIn("retention-days: 3", workflow)
        self.assertIn("if-no-files-found: error", workflow)
        for bad in ("pull_request_target:", "workflow_run:", "gh release",
                    "contents: write", "secrets.", "continue-on-error:"):
            self.assertNotIn(bad, workflow)
        rules, _ = p.path_rules(root)
        self.assertTrue(any(p.re.fullmatch(rule, ".github/workflows/next04-ux-proof.yml") for rule in rules))
        self.assertFalse(any(p.re.fullmatch(rule, ".github/workflows/next04-other.yml") for rule in rules))

    def test_visual67_capture_workflow_stays_branch_only_and_release_free(self):
        root = Path(__file__).resolve().parent.parent
        source = (root / ".github/workflows/visual-proof.yml").read_text(encoding="utf-8")
        self.assertIn("branches: [dev3/visual-poc-01]", source)
        self.assertIn("github.actor == 'TobbyCy'", source)
        self.assertIn("permissions:\n  contents: read", source)
        self.assertIn("retention-days: 3", source)
        self.assertIn("VISUAL_SCREENSHOTS_VERIFIED_PASS", source)
        self.assertIn("if-no-files-found: error", source)
        for bad in ("pull_request_target:", "workflow_run:", "gh release",
                    "contents: write", "secrets.", "continue-on-error:"):
            self.assertNotIn(bad, source)
        self.assertEqual(source.count("actions/upload-artifact@"), 1)
        self.assertIn("dotnet publish", source)

    def test_product_integrity_guard_has_locked_baseline(self):
        root = Path(__file__).resolve().parent.parent
        import hashlib
        import re
        content = (root / "tests/Invoke-CiStatic.ps1").read_bytes()
        self.assertTrue(content.startswith(bytes.fromhex("efbbbf")))
        raw = content.decode("utf-8-sig")
        baseline = re.search(r"(?s)\$baseline = @'\r?\n(.*?)\r?\n'@", raw)
        self.assertIsNotNone(baseline)
        locked = re.findall(r"(?m)^([0-9a-f]{64})  ([^\r\n]+)", baseline[1])
        self.assertEqual(19, len(locked))
        self.assertEqual(19, len({path for _, path in locked}))
        for checksum, path in locked:
            data = p.git(root, "cat-file", "blob", "HEAD:" + path)
            self.assertEqual(checksum, hashlib.sha256(data).hexdigest(), path)
        rules, _ = p.path_rules(root)
        matches = lambda path: any(re.fullmatch(rule, path) for rule in rules)
        for path in ("docs/product-status.md", "docs/roadmap.md",
                     "docs/developer/workflow.md", ".github/ISSUE_TEMPLATE/feature.yml"):
            self.assertTrue(matches(path), path)
        for path in ("docs/INTERNAL_HANDOFF_NOT_ALLOWED.md", "docs/INTERNAL_RECOVERY_NOT_ALLOWED.md",
                      ".github/ISSUE_TEMPLATE/tache-technique.yml"):
            self.assertFalse(matches(path), path)

    def test_pub01a_public_workflow_guard_and_negative_canaries(self):
        # Exercise the real portable verifier over disposable workflow/owner files.
        source = Path(__file__).resolve().parent.parent
        workflow = (source / ".github/workflows/ci.yml").read_text(encoding="utf-8-sig")
        owners = (source / ".github/CODEOWNERS").read_text(encoding="utf-8-sig")
        with tempfile.TemporaryDirectory(prefix="rm-pub01a-policy-") as tmp:
            root = Path(tmp)
            target = root / ".github/workflows/ci.yml"
            target.parent.mkdir(parents=True)
            target.write_text(workflow, encoding="utf-8")
            owners_path = root / ".github/CODEOWNERS"
            owners_path.write_text(owners, encoding="utf-8")
            self.assertTrue(p.verify_public_ci_guard(root))
            bad_workflows = {
                "unprivileged_event": workflow.replace("  pull_request:", "  pull_request_target:"),
                "write_token": workflow.replace("  contents: read", "  contents: write", 1),
                "unpinned_action": workflow.replace("actions/checkout@fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09", "actions/checkout@v5"),
                "checkout_credential": workflow.replace("persist-credentials: false", "persist-credentials: true", 1),
                "setup_dotnet_unpinned": workflow.replace("actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9", "actions/setup-dotnet@v4"),
                "setup_dotnet_wrong_sdk": workflow.replace("dotnet-version: '10.0.401'", "dotnet-version: '10.0.402'"),
                "setup_dotnet_floating_sdk": workflow.replace("dotnet-version: '10.0.401'", "dotnet-version: '10.0.x'"),
                "setup_dotnet_missing": workflow.replace("actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9", "actions/checkout@fbc6f3992d24b796d5a048ff273f7fcc4a7b6c09"),
                "setup_dotnet_extra_source": workflow.replace("dotnet-version: '10.0.401'", "dotnet-version: '10.0.401'\n          source-url: https://untrusted.example.invalid"),
                "script_policy_bypass": workflow.replace("    if: ${{ always() }}", "    if: ${{ success() }}", 1),
                "untrusted_shell": workflow + "\n        run: echo ${{ github.event.pull_request.title }}\n",
                "artifact_unpinned": workflow.replace("  ci-policy-gate:",
                    "      - name: Unapproved artifact\n        uses: actions/upload-artifact@v4\n  ci-policy-gate:"),
                "artifact_pinned_but_unreviewed": workflow.replace("  ci-policy-gate:",
                    "      - name: Unapproved artifact\n        uses: actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02\n  ci-policy-gate:"),
            }
            for name, text_value in bad_workflows.items():
                with self.subTest(case=name):
                    target.write_text(text_value, encoding="utf-8")
                    with self.assertRaises(p.PolicyError):
                        p.verify_public_ci_guard(root)
            target.write_text(workflow, encoding="utf-8")
            owners_path.write_text(owners.replace("/.github/ @TobbyCy", ""), encoding="utf-8")
            with self.assertRaisesRegex(p.PolicyError, "PUBLIC_CI_CODEOWNERS_MISSING"):
                p.verify_public_ci_guard(root)

    def test_pub01a_main_only_manual_dispatch(self):
        # A manually dispatched workflow on a feature branch must not qualify.
        with tempfile.TemporaryDirectory(prefix="rm-pub01a-dispatch-") as tmp:
            root = Path(tmp)
            subprocess.run(["git", "init", "-q", str(root)], check=True)
            subprocess.run(["git", "-C", str(root), "config", "user.email", "ci@example.invalid"], check=True)
            subprocess.run(["git", "-C", str(root), "config", "user.name", "CI synthetic"], check=True)
            (root / "README.md").write_text("synthetic only\n", encoding="utf-8")
            subprocess.run(["git", "-C", str(root), "add", "--all"], check=True)
            subprocess.run(["git", "-C", str(root), "commit", "-qm", "test"], check=True)
            head = p.git(root, "rev-parse", "HEAD").decode().strip()
            env = {"GITHUB_SHA": head, "GITHUB_REF": "refs/heads/feature"}
            self.assertEqual("DISPATCH_MAIN_ONLY",
                p.make_plan(root, "workflow_dispatch", {}, env)["error"])

    def test_final_workflow_dependencies_are_not_optional(self):
        import re
        root = Path(__file__).resolve().parent.parent
        doc = (root / ".github/workflows/ci.yml").read_text(encoding="utf-8-sig")
        def block(job):
            match = re.search(r"(?ms)^  " + job + r":\n(.*?)(?=^  [a-z0-9-]+:\n|\Z)", doc)
            self.assertIsNotNone(match, job)
            return match[1]
        scripts = {
            "ci0-static": ["Invoke-CiStatic.ps1", "Invoke-CiNegativeCanary.ps1",
                           "Invoke-CiDocsSync.ps1", "Invoke-CiDocsCanary.ps1",
                           "Invoke-CiUiAdapterContract.ps1"],
            "ci1-runtime": ["Invoke-CiSmoke.ps1"],
            "ci2-pester": ["Invoke-CiPesterDiscovery.ps1", "Invoke-CiPester.ps1",
                           "Invoke-CiPesterExecutionCanary.ps1", "Invoke-CiPesterCoverage.ps1",
                           "Invoke-CiE1NegativeCanary.ps1"],
            "ci3-e1-integration": ["Invoke-CiE1Integration.ps1"],
            "ci4-preflight-synthetic": ["Invoke-CiE1PreflightSynthetic.ps1"],
        }
        for job, names in scripts.items():
            contents = block(job)
            self.assertIn("always()", contents)
            self.assertIn("needs.policy.outputs.ps == 'true'", contents)
            for name in names:
                self.assertEqual(1, contents.count(name), (job, name))
        # Once the UI tree is present, the workflow must execute its existing
        # adapter harness and reject any missing/new Node smoke file.
        static_job = block("ci0-static")
        self.assertIn("UI_ADAPTER_SOURCES_MISSING", static_job)
        self.assertIn("UI_TEST_DISCOVERY_MISMATCH", static_job)
        self.assertIn("UI_SMOKE_FAILED:", static_job)
        # Every suite shipped by Dev2 #30 must be discoverable; the Unicode
        # producer suite runs with the real PS5.1 output, not as a no-arg smoke.
        for suite in ("smoke.cjs", "projection-smoke.cjs", "integrated-smoke.cjs",
                      "t2-catalogue.test.cjs", "t4-catalogue.test.cjs",
                      "synthetic-import.test.cjs", "ui-journey.test.cjs",
                      "unicode-producer.test.cjs"):
            self.assertIn(suite, static_job)
        self.assertIn("UI_TEST_DISCOVERY_MISMATCH", static_job)
        self.assertIn("if ($test -ceq 'unicode-producer.test.cjs')", static_job)
        self.assertIn("node $unicodeTest $syntheticFile", static_job)
        self.assertIn("UI_UNICODE_PRODUCER_FAILED", static_job)
        final = block("ci-policy-gate")
        self.assertIn("always()", final)
        self.assertIn("toJSON(needs)", final)
        listed = re.search(r"needs: \[([^]]+)\]", final)
        self.assertIsNotNone(listed)
        self.assertEqual(p.NEEDS, {x.strip() for x in listed[1].split(",")})
        self.assertNotIn("continue-on-error", doc)
        self.assertNotIn("pull_request_target", doc)
        self.assertNotIn("contents: write", doc)
        self.assertIn("workflow_dispatch:", doc)
        for job in ("policy", "ci-docs", "ci-policy-gate"):
            self.assertIn("ubuntu-24.04", block(job))


class ReleaseTests(unittest.TestCase):
    def test_boundary_valid_candidate_still_not_authorized(self):
        valid = {"ref": "refs/heads/main", "event": "workflow_dispatch", "commit": "a" * 40,
                 "main_sha": "a" * 40, "tag_sha": "a" * 40, "ci_policy_gate": "success",
                 "dotnet_tests_executed": 1, "rid": "win-x64", "self_contained": True,
                 "zip_name": "synthetic.zip", "zip_sha256": "b" * 64}
        self.assertEqual("NOT_AUTHORIZED_BY_CI", p.release_boundary(valid))
        for field, value in (("ref", "refs/pull/7/merge"), ("event", "pull_request"), ("tag_sha", "c" * 40),
                             ("ci_policy_gate", "skipped"), ("dotnet_tests_executed", 0), ("rid", "linux-x64"),
                             ("self_contained", False), ("zip_sha256", "invalid")):
            bad = dict(valid, **{field: value})
            with self.subTest(field=field), self.assertRaises(p.PolicyError):
                p.release_boundary(bad)


class CliRoutingProofTests(unittest.TestCase):
    def test_real_cli_docs_pass_ps_skip_refusal_and_dotnet_no_project(self):
        # Execute the actual classifier/docs/final commands in an isolated tracked clone.
        # No PS or SDK process is launched; PS-only missing-job refusal is deliberately tested.
        source = Path(__file__).resolve().parent.parent
        with tempfile.TemporaryDirectory(prefix="rm-ci-policy-") as tmp:
            workspace = Path(tmp).resolve()
            p.require(workspace.parent == Path(tempfile.gettempdir()).resolve() and not workspace.is_symlink(), "FIXTURE_BOUNDARY")
            root = workspace / "repo"
            subprocess.run(["git", "clone", "--quiet", "--no-hardlinks", "--no-local", "--", str(source), str(root)], check=True, capture_output=True)
            # A local clone checks out the committed main, which may predate the
            # policy under test. Use exactly the current checkout's policy in
            # the disposable clone; do not alter the source checkout or gates.
            source_policy = source / "tests" / "ci_policy.py"
            cloned_policy = root / "tests" / "ci_policy.py"
            p.require(source_policy.is_file() and cloned_policy.is_file()
                      and cloned_policy.resolve().is_relative_to(root.resolve()),
                      "ROUTING_POLICY_FIXTURE_BOUNDARY")
            shutil.copyfile(source_policy, cloned_policy)
            for key, value in (("user.name", "Synthetic CI"), ("user.email", "ci@example.invalid"), ("commit.gpgsign", "false"), ("core.autocrlf", "false")):
                p.git(root, "config", key, value)
            # The clone tests policy behavior, not the separate documentation PR.
            # When a real .NET POC exists, this *routing* canary still needs a
            # disposable repository without a .NET project to prove fail-closed.
            # Never delete files from the checked-out Product; only from tmp/repo.
            for rel in ("src/RomothequeManager.V2", "tests/dotnet/RomothequeManager.V2.Tests"):
                candidate = (root / rel).resolve()
                p.require(candidate.is_relative_to(root.resolve()) and candidate != root.resolve(),
                          "DOTNET_ROUTING_FIXTURE_BOUNDARY")
                if candidate.exists():
                    shutil.rmtree(candidate)
            # Complete the disposable fixture only; real repository checks stay strict.
            for name in p.REQUIRED_DOCS:
                target = root / name
                if not target.exists():
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_text("# Synthetic CI test document\n", encoding="utf-8")
            p.git(root, "add", "--all")
            if p.git(root, "status", "--porcelain").strip():
                p.git(root, "commit", "--quiet", "-m", "Synthetic baseline documents")
            def save():
                p.git(root, "add", "--all")
                p.git(root, "commit", "--quiet", "-m", "Synthetic routing proof")
                return p.git(root, "rev-parse", "HEAD").decode().strip()
            def append(name):
                target = root / name
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open("a", encoding="utf-8") as out:
                    out.write("\n# Synthetic routing-only observation\n")
            def exercise(before, after, category, pass_gate):
                payload = workspace / "event.json"
                payload.write_text(json.dumps({"before": before, "after": after, "forced": False, "ref": "refs/heads/main"}), encoding="utf-8")
                env = dict(os.environ, GITHUB_EVENT_NAME="push", GITHUB_EVENT_PATH=str(payload), GITHUB_REF="refs/heads/main", GITHUB_SHA=after)
                # Do not write canary outputs into the production job's GitHub command files.
                for key in ("GITHUB_OUTPUT", "GITHUB_STEP_SUMMARY", "GITHUB_ACTIONS", "GITHUB_REPOSITORY", "GITHUB_REPOSITORY_ID"):
                    env.pop(key, None)
                start = time.monotonic()
                def run(command, expected=0):
                    result = subprocess.run([sys.executable, "-B", "tests/ci_policy.py", command], cwd=root, env=env, text=True, capture_output=True)
                    self.assertEqual(expected, result.returncode, result.stdout + result.stderr)
                    return result.stdout + result.stderr
                output = run("classify")
                plan = json.loads(next(line[len("CI_POLICY "):] for line in output.splitlines() if line.startswith("CI_POLICY ")))
                self.assertEqual(category, plan["class"])
                run("docs")
                needs = {job: {"result": "success" if job in ("policy", "ci-docs") else "skipped"} for job in p.NEEDS}
                needs["policy"]["outputs"] = {"policy": json.dumps(plan)}
                env["CI_NEEDS_JSON"] = json.dumps(needs)
                verdict = run("gate", 0 if pass_gate else 1)
                if not pass_gate:
                    self.assertIn("REQUIRED_JOB_NOT_SUCCESS", verdict)
                if category == "DOTNET_ONLY":
                    self.assertIn("DOTNET_PROJECT_OR_TEST_MISSING", run("dotnet", 1))
                print("CLI_ROUTING_PROOF " + json.dumps({"class": category, "docs": "actual_PASS", "gate": "actual_PASS" if pass_gate else "actual_REFUSAL_required_jobs_skipped", "seconds": round(time.monotonic() - start, 3), "ps_and_dotnet_products_executed": False}, sort_keys=True))
            base = p.git(root, "rev-parse", "HEAD").decode().strip()
            append("README.md")
            docs = save()
            exercise(base, docs, "DOCS_ONLY", True)
            unit = sorted(name for name in p.tree(root) if name.startswith("tests/Unit/") and name.endswith(".ps1"))[0]
            append(unit)
            append("docs/product-status.md")
            ps = save()
            exercise(docs, ps, "PS_ONLY", False)
            append("src/RomothequeManager.V2/SyntheticProbe.cs")
            append("docs/product-status.md")
            append("CHANGELOG.md")
            net = save()
            exercise(ps, net, "DOTNET_ONLY", False)
            p.require(workspace.parent == Path(tempfile.gettempdir()).resolve() and not workspace.is_symlink(), "FIXTURE_CLEANUP_BOUNDARY")



class Pr64UxArtifactPolicyTests(unittest.TestCase):
    """Negative canaries for the isolated temporary PR64-only artifact workflow."""

    def test_ux_artifact_workflow_is_strictly_pinned_and_manual_release_free(self):
        workflow = (Path(__file__).resolve().parents[1] / ".github/workflows/ux-artifact.yml").read_text(encoding="utf-8")
        self.assertIn("  pull_request:\n    branches: [main]", workflow)
        self.assertIn("github.event.pull_request.number == 64", workflow)
        self.assertIn("github.actor == 'TobbyCy'", workflow)
        self.assertIn("github.event.pull_request.head.repo.full_name == 'TobbyCy/RomothequeManager'", workflow)
        self.assertIn("ref: ${{ github.event.pull_request.head.sha }}", workflow)
        self.assertIn("github.event.pull_request.head.ref == 'dev3/next-01-ux'", workflow)
        self.assertIn("UX_RECIPE_UNVERIFIED_BUILD_SOURCE", workflow)
        self.assertNotIn("e775cdd64522d45ee2fb848df232fa1f19023326", workflow)
        self.assertIn("UX_RECIPE_OFFLINE_FIREWALL_ACTIVE", workflow)
        self.assertIn("UX_RECIPE_OFFLINE_ESDE_GUI_PASS", workflow)
        self.assertIn("$coverPng = [Convert]::FromBase64String(", workflow)
        self.assertIn("$thumbPng = [Convert]::FromBase64String(", workflow)
        self.assertIn("'covers/corrupt.png'", workflow)
        self.assertNotIn("$onePixel", workflow)
        self.assertIn("UX_RECIPE_SHELL_GUI_WINDOW_PASS", workflow)
        self.assertIn("permissions:\n  contents: read", workflow)
        self.assertIn("retention-days: 3", workflow)
        self.assertIn("if-no-files-found: error", workflow)
        self.assertIn("UX_RECIPE_LEGAL_APPROVAL_REQUIRED", workflow)
        self.assertIn("UX_RECIPE_WINEXE_GUI_PASS", workflow)
        self.assertIn("UX_RECIPE_CONSOLE_SUBSYSTEM_FOUND", workflow)
        self.assertNotIn("cc35abf70acc12120f7617f8e5ab0a8ea84b8801", workflow)
        self.assertIn("UX-PR64-win-x64-${{ github.event.pull_request.head.sha }}", workflow)
        self.assertIn("UX_RECIPE_FILE_HASH_MISMATCH", workflow)
        self.assertIn("UX_RECIPE_FORBIDDEN_FILE", workflow)
        self.assertIn("actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02", workflow)
        for forbidden in ("pull_request_target:", "workflow_run:", "repository_dispatch:",
                          "release create", "gh release", "permissions: write-all",
                          "contents: write", "secrets.", "continue-on-error:"):
            self.assertNotIn(forbidden, workflow, forbidden)
        self.assertEqual(workflow.count("actions/upload-artifact@"), 1)
        self.assertIn("FICTITIOUS TEXT ONLY; NOT A ROM", workflow)
        self.assertIn("syntheticOnly=$true", workflow)

    def test_workflow_refuses_unreviewed_source_and_upload_gating_changes(self):
        workflow = (Path(__file__).resolve().parents[1] / ".github/workflows/ux-artifact.yml").read_text(encoding="utf-8")
        for replacement, sentinel in (
            ("ref: main", "ref: ${{ github.event.pull_request.head.sha }}"),
            ("retention-days: 90", "retention-days: 3"),
            ("if-no-files-found: warn", "if-no-files-found: error"),
        ):
            mutated = workflow.replace(sentinel, replacement, 1)
            self.assertNotEqual(mutated, workflow)
            self.assertNotIn(sentinel, mutated)


if __name__ == "__main__":
    unittest.main(verbosity=2)
