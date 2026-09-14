#!/usr/bin/env python3
"""웹 정적 자산 동기화 + asset 카탈로그 생성.

정본:
  - 아트: art_delivery/runtime/<manifest.path>   (handoff/art/manifest.lock.json 기준)
  - 콘텐츠: content/ep01/*.json

생성(웹):
  - web/public/art/<manifest.path>
  - web/public/content/ep01/*.json
  - web/public/content/manifest.json
  - web/src/shared/assets/assetCatalog.generated.json

사용:
  python3 tools/sync_web_assets.py            # 동기화
  python3 tools/sync_web_assets.py --check    # 드리프트 검사만 (파일 수정 없음)
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
import tempfile
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[1]
MANIFEST_PATH = WORKSPACE / "handoff/art/manifest.lock.json"
ART_SOURCE_ROOT = WORKSPACE / "art_delivery/runtime"
CONTENT_SOURCE_DIR = WORKSPACE / "content/ep01"

ART_DEST_ROOT = WORKSPACE / "web/public/art"
CONTENT_DEST_DIR = WORKSPACE / "web/public/content/ep01"
CONTENT_MANIFEST_PATH = WORKSPACE / "web/public/content/manifest.json"
CATALOG_PATH = WORKSPACE / "web/src/shared/assets/assetCatalog.generated.json"

CONTENT_FILES = [
    "episode.json",
    "scenes.json",
    "dialogues.ko.json",
    "evidence.json",
    "testimonies.json",
    "branches.json",
    "puzzles.json",
    "hints.ko.json",
    "ui.ko.json",
    "graph.json",
    "matrix.json",
]

NAME_PATTERN = re.compile(r"^[a-z0-9_./-]+$")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


class Report:
    def __init__(self) -> None:
        self.issues: list[str] = []
        self.copied = 0
        self.verified = 0

    def issue(self, message: str) -> None:
        self.issues.append(message)

    @property
    def ok(self) -> bool:
        return not self.issues


def validate_manifest(manifest: dict, report: Report) -> list[dict]:
    if manifest.get("schemaVersion") != 1:
        report.issue("manifest.schemaVersion != 1")
    if manifest.get("contractVersion") != "art.v1":
        report.issue("manifest.contractVersion != art.v1")
    if manifest.get("episodeId") != "ep01":
        report.issue("manifest.episodeId != ep01")
    assets = manifest.get("assets")
    if not isinstance(assets, list):
        report.issue("manifest.assets is not a list")
        return []
    required_count = manifest.get("requiredAssetCount")
    if required_count != 54:
        report.issue(f"manifest.requiredAssetCount {required_count} != 54")
    if len(assets) != 54:
        report.issue(f"manifest assets {len(assets)} != 54")
    ids = [asset.get("id") for asset in assets]
    paths = [asset.get("path") for asset in assets]
    if len(set(ids)) != len(ids):
        report.issue("manifest asset id duplication")
    lower_paths = [str(path).lower() for path in paths]
    if len(set(lower_paths)) != len(lower_paths):
        report.issue("manifest asset path case/duplication collision")
    hashes: dict[str, str] = {}
    for asset in assets:
        asset_id = str(asset.get("id"))
        path_text = str(asset.get("path"))
        if not NAME_PATTERN.match(path_text):
            report.issue(f"{asset_id}: path has invalid characters: {path_text}")
        if path_text.startswith("/") or ".." in Path(path_text).parts:
            report.issue(f"{asset_id}: path escapes root: {path_text}")
        digest = asset.get("sha256")
        if not digest or len(str(digest)) != 64:
            report.issue(f"{asset_id}: missing or malformed sha256")
        elif digest in hashes:
            report.issue(f"{asset_id}: duplicate sha256 with {hashes[digest]}")
        else:
            hashes[str(digest)] = asset_id
        if asset.get("status") != "approved":
            report.issue(f"{asset_id}: status is not approved")
    return assets


def validate_content_sources(report: Report) -> list[str]:
    if not CONTENT_SOURCE_DIR.is_dir():
        report.issue(f"missing content dir: {CONTENT_SOURCE_DIR}")
        return []
    present = sorted(p.name for p in CONTENT_SOURCE_DIR.glob("*.json"))
    missing = [name for name in CONTENT_FILES if name not in present]
    extra = [name for name in present if name not in CONTENT_FILES]
    for name in missing:
        report.issue(f"missing content file: {name}")
    for name in extra:
        report.issue(f"unexpected content file: {name}")
    return [name for name in CONTENT_FILES if name in present]


def copy_verified(source: Path, dest: Path, expected_sha: str | None, report: Report, copy: bool) -> None:
    if not source.is_file():
        report.issue(f"missing source: {source}")
        return
    actual = sha256_file(source)
    if expected_sha and actual != expected_sha:
        report.issue(f"source hash mismatch: {source} ({actual} != {expected_sha})")
        return
    if copy:
        dest.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=dest.parent, delete=False) as handle:
            temp_path = Path(handle.name)
        try:
            shutil.copyfile(source, temp_path)
            if sha256_file(temp_path) != actual:
                report.issue(f"copy verification failed: {dest}")
                return
            temp_path.replace(dest)
        finally:
            temp_path.unlink(missing_ok=True)
        report.copied += 1
    else:
        if not dest.is_file():
            report.issue(f"missing web copy: {dest}")
            return
        dest_hash = sha256_file(dest)
        if dest_hash != actual:
            report.issue(f"web copy drift: {dest} ({dest_hash} != {actual})")
            return
    report.verified += 1


def find_extra_files(root: Path, expected_relatives: set[str]) -> list[Path]:
    if not root.is_dir():
        return []
    extras = []
    for candidate in root.rglob("*"):
        if candidate.is_file():
            relative = candidate.relative_to(root).as_posix()
            if relative not in expected_relatives:
                extras.append(candidate)
    return extras


def build_catalog(assets: list[dict], manifest_sha: str) -> dict:
    entries = {}
    for asset in sorted(assets, key=lambda item: str(item["id"])):
        regions = []
        for region in asset.get("regions") or []:
            regions.append(
                {
                    "regionId": region.get("regionId"),
                    "x": region.get("x"),
                    "y": region.get("y"),
                    "width": region.get("width"),
                    "height": region.get("height"),
                }
            )
        entries[str(asset["id"])] = {
            "id": asset["id"],
            "url": "/art/" + str(asset["path"]),
            "kind": asset["kind"],
            "width": asset.get("actualWidth") or asset.get("width"),
            "height": asset.get("actualHeight") or asset.get("height"),
            "anchor": asset.get("anchor"),
            "regions": regions,
            "sha256": asset["sha256"],
        }
    return {
        "schemaVersion": 1,
        "contractVersion": "art.v1",
        "episodeId": "ep01",
        "manifestSha256": manifest_sha,
        "assetCount": len(entries),
        "assets": entries,
    }


def write_if_changed(path: Path, text: str, report: Report, copy: bool) -> None:
    if path.is_file() and path.read_text(encoding="utf-8") == text:
        return
    if not copy:
        report.issue(f"generated file drift: {path}")
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def run(copy: bool) -> Report:
    report = Report()
    if not MANIFEST_PATH.is_file():
        report.issue(f"missing manifest: {MANIFEST_PATH}")
        return report
    manifest = load_json(MANIFEST_PATH)
    if manifest.get("ready") is False:
        report.issue("manifest.lock.json is not ready")
    assets = validate_manifest(manifest, report)
    if not assets:
        return report

    manifest_sha = sha256_file(MANIFEST_PATH)
    expected_art_relatives = {str(asset["path"]) for asset in assets}
    for asset in assets:
        source = ART_SOURCE_ROOT / str(asset["path"])
        dest = ART_DEST_ROOT / str(asset["path"])
        copy_verified(source, dest, str(asset.get("sha256")), report, copy)

    actual_art_relatives = {
        candidate.relative_to(ART_SOURCE_ROOT).as_posix()
        for candidate in ART_SOURCE_ROOT.rglob("*.png")
    } if ART_SOURCE_ROOT.is_dir() else set()
    for extra in sorted(actual_art_relatives - expected_art_relatives):
        report.issue(f"source PNG not in manifest: {extra}")

    for extra in find_extra_files(ART_DEST_ROOT, expected_art_relatives):
        report.issue(f"stale web asset not in manifest: {extra.relative_to(ART_DEST_ROOT)}")

    content_names = validate_content_sources(report)
    content_hashes: dict[str, str] = {}
    for name in content_names:
        source = CONTENT_SOURCE_DIR / name
        dest = CONTENT_DEST_DIR / name
        digest = sha256_file(source)
        content_hashes[name] = digest
        copy_verified(source, dest, digest, report, copy)

    episode = load_json(CONTENT_SOURCE_DIR / "episode.json")
    content_version = (episode.get("episode") or {}).get("contentVersion", "")
    content_manifest = {
        "schemaVersion": 1,
        "episodeId": "ep01",
        "contentVersion": content_version,
        "files": content_hashes,
    }
    content_manifest_text = json.dumps(content_manifest, ensure_ascii=False, indent=2) + "\n"
    write_if_changed(CONTENT_MANIFEST_PATH, content_manifest_text, report, copy)

    for extra in find_extra_files(CONTENT_DEST_DIR, set(content_names)):
        report.issue(f"stale web content not in canonical set: {extra.relative_to(CONTENT_DEST_DIR)}")

    catalog = build_catalog(assets, manifest_sha)
    catalog_text = json.dumps(catalog, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    write_if_changed(CATALOG_PATH, catalog_text, report, copy)

    return report


def main() -> int:
    parser = argparse.ArgumentParser(description="Sync canonical content/art into web/ and generate catalog")
    parser.add_argument("--check", action="store_true", help="verify only; do not modify files")
    args = parser.parse_args()

    report = run(copy=not args.check)
    mode = "check" if args.check else "sync"
    print(
        f"sync_web_assets.py[{mode}]: ok={report.ok} copied={report.copied} "
        f"verified={report.verified} issues={len(report.issues)}"
    )
    for issue in report.issues:
        print("  - " + issue)
    return 0 if report.ok else 1


if __name__ == "__main__":
    sys.exit(main())
