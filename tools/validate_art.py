#!/usr/bin/env python3
"""Validate the Episode 01 art contract and write machine/readable reports."""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path

from PIL import Image


MAX_FILE_BYTES = 80 * 1024 * 1024
MAX_PIXELS = 40_000_000


def fail(issues, asset_id, path, reason, **extra):
    item = {"id": asset_id, "path": path, "reason": reason}
    item.update(extra)
    issues.append(item)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_regions(issues, asset_id, path, regions, required_ids):
    seen = set()
    for region in regions or []:
        region_id = region.get("regionId")
        if not isinstance(region_id, str) or not region_id:
            fail(issues, asset_id, path, "regionId missing or invalid")
            continue
        if region_id in seen:
            fail(issues, asset_id, path, "duplicate regionId", regionId=region_id)
        seen.add(region_id)
        values = [region.get(k) for k in ("x", "y", "width", "height")]
        if any(not isinstance(v, (int, float)) for v in values):
            fail(issues, asset_id, path, "region coordinate is not numeric", regionId=region_id)
            continue
        x, y, width, height = values
        if width <= 0 or height <= 0 or x < 0 or y < 0 or x + width > 1 or y + height > 1:
            fail(issues, asset_id, path, "region outside normalized image bounds", regionId=region_id)
    for region_id in sorted(set(required_ids) - seen):
        fail(issues, asset_id, path, "required region missing", regionId=region_id)


def load_json(path: Path, issues):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:
        issues.append({"id": "", "path": str(path), "reason": f"invalid JSON: {exc}"})
        return {}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--root", required=True, type=Path)
    parser.add_argument("--report-json", type=Path, default=Path("handoff/art/validation_report.json"))
    parser.add_argument("--report-md", type=Path, default=Path("handoff/art/ART_REVIEW.md"))
    args = parser.parse_args()

    issues = []
    plan = load_json(args.plan, issues)
    lock = load_json(args.manifest, issues)
    plan_assets = plan.get("assets", [])
    lock_assets = lock.get("assets", [])
    expected_count = plan.get("requiredAssetCount")
    if expected_count != 54:
        issues.append({"id": "", "path": str(args.plan), "reason": "plan requiredAssetCount must be 54", "actual": expected_count})

    def index_assets(items, label):
        result = {}
        paths = defaultdict(list)
        for asset in items:
            asset_id = asset.get("id")
            path = asset.get("path")
            if not isinstance(asset_id, str) or not asset_id:
                fail(issues, "", str(args.plan), f"{label} asset id missing")
                continue
            if asset_id in result:
                fail(issues, asset_id, path or "", f"duplicate id in {label}")
            result[asset_id] = asset
            paths[path].append(asset_id)
            if not isinstance(path, str) or Path(path).is_absolute() or ".." in Path(path).parts:
                fail(issues, asset_id, path or "", f"unsafe path in {label}")
        for path, ids in paths.items():
            if len(ids) > 1:
                for asset_id in ids:
                    fail(issues, asset_id, path or "", f"duplicate path in {label}")
        return result

    plan_index = index_assets(plan_assets, "plan")
    lock_index = index_assets(lock_assets, "lock")
    if set(plan_index) != set(lock_index):
        issues.append({"id": "", "path": "", "reason": "plan and lock asset sets differ",
                       "missingInLock": sorted(set(plan_index) - set(lock_index)),
                       "extraInLock": sorted(set(lock_index) - set(plan_index))})
    if len(plan_assets) != expected_count or len(lock_assets) != expected_count:
        issues.append({"id": "", "path": "", "reason": "asset count mismatch",
                       "planCount": len(plan_assets), "lockCount": len(lock_assets), "expected": expected_count})

    root = args.root.resolve()
    planned_paths = set()
    seen_hashes = defaultdict(list)
    checked = []
    for asset_id, planned in plan_index.items():
        path_text = planned.get("path", "")
        target = (root / path_text).resolve()
        planned_paths.add(target)
        try:
            target.relative_to(root)
        except ValueError:
            fail(issues, asset_id, path_text, "resolved path escapes root")
            continue
        if not target.exists():
            fail(issues, asset_id, path_text, "file missing")
            continue
        info = {"id": asset_id, "path": path_text}
        try:
            size = target.stat().st_size
            info["fileBytes"] = size
            if size > MAX_FILE_BYTES:
                fail(issues, asset_id, path_text, "file exceeds size limit", fileBytes=size)
            with target.open("rb") as handle:
                if handle.read(8) != b"\x89PNG\r\n\x1a\n":
                    fail(issues, asset_id, path_text, "invalid PNG signature")
            with Image.open(target) as image:
                image.verify()
            with Image.open(target) as image:
                info["actualWidth"], info["actualHeight"] = image.size
                info["mode"] = image.mode
                if image.width * image.height > MAX_PIXELS:
                    fail(issues, asset_id, path_text, "image exceeds pixel limit")
                if (image.width, image.height) != (planned.get("width"), planned.get("height")):
                    fail(issues, asset_id, path_text, "size mismatch",
                         expected=[planned.get("width"), planned.get("height")],
                         actual=[image.width, image.height])
                if image.mode not in ("RGB", "RGBA"):
                    fail(issues, asset_id, path_text, "mode must be RGB or RGBA", actual=image.mode)
                if planned.get("alphaRequired"):
                    if image.mode != "RGBA":
                        fail(issues, asset_id, path_text, "alphaRequired asset is not RGBA")
                    else:
                        alpha = image.getchannel("A")
                        if alpha.getbbox() is None or alpha.getextrema()[1] == 0:
                            fail(issues, asset_id, path_text, "alpha channel has no visible pixels")
                elif image.mode == "RGBA" and image.getchannel("A").getextrema() != (255, 255):
                    fail(issues, asset_id, path_text, "opaque asset has non-opaque alpha")
            digest = sha256_file(target)
            info["sha256"] = digest
            seen_hashes[digest].append(asset_id)
            locked = lock_index.get(asset_id, {})
            for key in ("sha256", "fileBytes", "actualWidth", "actualHeight", "mode"):
                if key in locked and locked.get(key) != info.get(key):
                    fail(issues, asset_id, path_text, f"lock field mismatch: {key}",
                         expected=locked.get(key), actual=info.get(key))
            if locked.get("status") != "approved":
                fail(issues, asset_id, path_text, "status is not approved")
            if locked.get("visualApproved") is not True:
                fail(issues, asset_id, path_text, "visualApproved is not true")
            if not locked.get("visualReviewBy") or not locked.get("visualReviewNotes"):
                fail(issues, asset_id, path_text, "visual review metadata is incomplete")
            required_regions = {r.get("regionId") for r in planned.get("regions", [])}
            validate_regions(issues, asset_id, path_text, locked.get("regions", []), required_regions)
            checked.append(info)
        except Exception as exc:
            fail(issues, asset_id, path_text, f"image decode/check failed: {exc}")

    for digest, asset_ids in seen_hashes.items():
        if len(asset_ids) > 1:
            for asset_id in asset_ids:
                fail(issues, asset_id, plan_index[asset_id].get("path", ""), "duplicate SHA-256",
                     sha256=digest, assets=asset_ids)

    for candidate in root.rglob("*.png"):
        if candidate.resolve() not in planned_paths:
            fail(issues, "", str(candidate.relative_to(root)), "PNG is not in plan")

    report = {
        "ok": not issues,
        "plan": str(args.plan),
        "manifest": str(args.manifest),
        "root": str(args.root),
        "expectedAssetCount": expected_count,
        "checkedAssetCount": len(checked),
        "issueCount": len(issues),
        "issues": issues,
        "assets": checked,
    }
    args.report_json.parent.mkdir(parents=True, exist_ok=True)
    args.report_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = [
        "# ART_REVIEW",
        "",
        f"- 결과: {'PASS' if not issues else 'FAIL'}",
        f"- 계획 개수: {expected_count}",
        f"- 실제 검사 개수: {len(checked)}",
        f"- 이슈 수: {len(issues)}",
        "",
        "| assetId | path | size | mode | sha256 |",
        "| --- | --- | --- | --- | --- |",
    ]
    for info in checked:
        lines.append(f"| {info['id']} | {info['path']} | {info.get('actualWidth')}×{info.get('actualHeight')} | {info.get('mode')} | {info.get('sha256')} |")
    if issues:
        lines.extend(["", "## Issues", ""])
        for item in issues:
            lines.append(f"- {item.get('id','')} {item.get('path','')} — {item.get('reason')}")
    args.report_md.parent.mkdir(parents=True, exist_ok=True)
    args.report_md.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"ok": not issues, "checked": len(checked), "issues": len(issues), "report": str(args.report_md)}, ensure_ascii=False))
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
