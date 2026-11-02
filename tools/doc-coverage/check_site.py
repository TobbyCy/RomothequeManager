"""Validate generated DocFX output, never any user library or executable."""
from __future__ import annotations

import argparse
import json
import re
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

REQUIRED = (
    "InventorySnapshot",
    "IInventoryApplicationService",
    "MediaIndexApplicationService",
    "MediaLinkProjection",
    "ObservedLibraryViewModel",
    "MediaPresentationService",
    "MediaPreviewService",
)

class Anchors(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.links = []
    def handle_starttag(self, tag, attrs):
        if tag.lower() not in ("a", "link", "script", "img"):
            return
        values = dict(attrs)
        target = values.get("href") if tag.lower() in ("a", "link") else values.get("src")
        if target:
            self.links.append(target)

def check(root: Path):
    api = root / "api"
    site = root / "_site"
    if not api.is_dir() or not site.is_dir():
        raise ValueError("DOCFX_METADATA_OR_SITE_MISSING")
    yaml_files = sorted(api.rglob("*.yml"))
    html_files = sorted(site.rglob("*.html"))
    if len(yaml_files) < 7 or len(html_files) < 7 or not (site / "index.html").is_file():
        raise ValueError("DOCFX_OUTPUT_EMPTY_OR_INCOMPLETE")
    uids = set()
    for f in yaml_files:
        for line in f.read_text(encoding="utf-8-sig").splitlines():
            m = re.match(r"\s*(?:-\s+)?uid:\s*['\"]?([^'\" #]+)", line)
            if m:
                uids.add(m.group(1))
    missing_uid = [want for want in REQUIRED if not any(uid.split(".")[-1] == want for uid in uids)]
    bad = []
    checked = 0
    for f in html_files:
        parser = Anchors()
        parser.feed(f.read_text(encoding="utf-8-sig"))
        for href in parser.links:
            parts = urlsplit(href)
            if parts.scheme or parts.netloc or href.startswith(("/", "//")):
                continue
            if not parts.path:
                continue  # A page-local anchor is not a missing file.
            relative = unquote(parts.path)
            if "\\" in relative or "\x00" in relative:
                bad.append((str(f.relative_to(site)), href, "INVALID_PATH"))
                continue
            target = (f.parent / relative).resolve()
            if not target.is_relative_to(site.resolve()):
                bad.append((str(f.relative_to(site)), href, "ESCAPES_SITE"))
            else:
                checked += 1
                if target.is_dir():
                    target = target / "index.html"
                if not target.is_file():
                    bad.append((str(f.relative_to(site)), href, "MISSING_TARGET"))
    result = dict(yamlFiles=len(yaml_files), htmlFiles=len(html_files), uidCount=len(uids),
                  requiredUids=list(REQUIRED), missingUids=missing_uid,
                  localLinksChecked=checked, brokenLinks=bad[:100],
                  errors=len(bad), passResult=(not bad and not missing_uid))
    (root / "site-check.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print("DOCFX_SITE_CHECK " + json.dumps(result, sort_keys=True))
    if bad or missing_uid:
        raise ValueError("DOCFX_BROKEN_LINK_OR_UID")
    return result

def main():
    p = argparse.ArgumentParser()
    p.add_argument("root", type=Path)
    args = p.parse_args()
    try:
        check(args.root.resolve(strict=True))
    except (OSError, ValueError) as exc:
        print("DOCFX_SITE_FAIL " + str(exc))
        raise SystemExit(1)

if __name__ == "__main__":
    main()
