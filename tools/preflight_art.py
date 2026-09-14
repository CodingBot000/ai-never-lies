#!/usr/bin/env python3
"""G1 art handoff preflight: documents, READY, manifest, and runtime PNG checks."""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
READY_PATH = ROOT / "handoff/art/ASSET_READY.json"
LOCK_PATH = ROOT / "handoff/art/manifest.lock.json"
PLAN_PATH = ROOT / "handoff/art/manifest.plan.json"
RUNTIME_ROOT = ROOT / "art_delivery/runtime"

DOCS = {
    "masterDocumentSha256": ROOT / "docs/00_MASTER_PLAN.md",
    "artInstructionSha256": ROOT / "docs/01_IMAGE_GENERATION_INSTRUCTIONS.md",
    "codeInstructionSha256": ROOT / "docs/02_CODE_GENERATION_INSTRUCTIONS.md",
}

FROZEN_ASSET_IDS = [
    "bg_bureau_night", "bg_interview_room", "bg_neovia_meeting", "bg_hearing_room",
    "bg_crosswalk_rain", "bg_crosswalk_dawn", "bg_maintenance_bay", "bg_development_lab",
    "bg_operations_room", "bg_hearing_corridor",
    "chr_nari_neutral", "chr_nari_distressed", "chr_nari_remembering",
    "chr_han_composed", "chr_han_guarded", "chr_han_strained", "chr_han_admitting",
    "chr_min_defensive", "chr_min_angry", "chr_min_cornered", "chr_min_resigned",
    "chr_yun_firm", "chr_yun_uneasy", "chr_yun_resolved",
    "chr_park_analytical", "chr_park_defensive", "chr_park_shaken", "chr_park_regretful",
    "chr_lee_confident", "chr_lee_irritated", "chr_lee_shaken",
    "chr_seo_calm", "chr_seo_stern", "chr_seo_unsettled", "chr_seo_accepting",
    "chr_cho_neutral", "chr_cho_firm", "chr_mira_reserved", "chr_mira_grief",
    "ev_front_base", "ev_rear_empty", "ev_junction_wide", "ev_cabin_base",
    "ev_radar_module", "ev_sensor_mount", "ev_vehicle_side", "ev_victim_photo",
    "ev_phone_base", "ev_service_workbench",
    "cg_ride_before", "cg_emergency_before", "cg_scene_after", "cg_hearing_wide",
    "cg_epilogue_window",
]


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    issues: list[dict] = []
    report: dict = {"generatedAtUtc": subprocess.run(
        ["date", "-u", "+%Y-%m-%dT%H:%M:%SZ"], capture_output=True, text=True).stdout.strip()}

    for name, path in DOCS.items():
        report[name] = {"path": str(path.relative_to(ROOT)), "actual": sha256_file(path)}
    report["manifestSha256"] = {"path": "handoff/art/manifest.lock.json",
                                "actual": sha256_file(LOCK_PATH)}

    if not READY_PATH.exists():
        issues.append({"id": "", "path": "handoff/art/ASSET_READY.json", "reason": "missing"})
        ready = {}
    else:
        ready = json.loads(READY_PATH.read_text(encoding="utf-8"))
    report["ready"] = ready
    report["checks"] = []

    def check(name: str, ok: bool, detail: str = "") -> None:
        report["checks"].append({"name": name, "ok": bool(ok), "detail": detail})
        if not ok:
            issues.append({"id": "", "path": name, "reason": detail or "check failed"})

    check("ready.ready", ready.get("ready") is True, "ready must be true")
    check("ready.contractVersion", ready.get("contractVersion") == "art.v1", "art.v1 required")
    check("ready.episodeId", ready.get("episodeId") == "ep01", "ep01 required")
    check("ready.counts",
          ready.get("requiredAssetCount") == 54 and ready.get("approvedAssetCount") == 54,
          f"required={ready.get('requiredAssetCount')} approved={ready.get('approvedAssetCount')}")
    check("ready.blockers", not ready.get("blockers"), "blockers must be empty")

    for name, path in DOCS.items():
        check(f"doc.{name}", ready.get(name) == report[name]["actual"],
              f"expected {ready.get(name)} actual {report[name]['actual']}")
    check("manifest.sha256", ready.get("manifestSha256") == report["manifestSha256"]["actual"],
          f"expected {ready.get('manifestSha256')} actual {report['manifestSha256']['actual']}")

    lock = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    plan = json.loads(PLAN_PATH.read_text(encoding="utf-8"))
    lock_ids = [a.get("id") for a in lock.get("assets", [])]
    plan_ids = [a.get("id") for a in plan.get("assets", [])]
    check("lock.count", len(lock_ids) == 54 and len(set(lock_ids)) == 54,
          f"count={len(lock_ids)} unique={len(set(lock_ids))}")
    check("plan.count", len(plan_ids) == 54 and len(set(plan_ids)) == 54,
          f"count={len(plan_ids)} unique={len(set(plan_ids))}")
    check("id-set.lock", set(lock_ids) == set(FROZEN_ASSET_IDS),
          f"missing={sorted(set(FROZEN_ASSET_IDS) - set(lock_ids))} extra={sorted(set(lock_ids) - set(FROZEN_ASSET_IDS))}")
    check("id-set.plan", set(plan_ids) == set(FROZEN_ASSET_IDS),
          f"missing={sorted(set(FROZEN_ASSET_IDS) - set(plan_ids))} extra={sorted(set(plan_ids) - set(FROZEN_ASSET_IDS))}")
    lock_paths = [a.get("path") for a in lock.get("assets", [])]
    plan_paths = [a.get("path") for a in plan.get("assets", [])]
    check("path-set", set(lock_paths) == set(plan_paths), "plan and lock paths must match")
    bad_case = [p for p in lock_paths if p != p.lower()]
    check("path.lowercase", not bad_case, f"non-lowercase paths: {bad_case}")

    runtime_pngs = sorted(p.relative_to(RUNTIME_ROOT).as_posix() for p in RUNTIME_ROOT.rglob("*.png"))
    report["runtimePngCount"] = len(runtime_pngs)
    check("runtime.png.count", len(runtime_pngs) == 54, f"actual={len(runtime_pngs)}")
    check("runtime.png.set", set(runtime_pngs) == set(lock_paths),
          f"missing={sorted(set(lock_paths) - set(runtime_pngs))} extra={sorted(set(runtime_pngs) - set(lock_paths))}")

    reports_dir = ROOT / "reports"
    reports_dir.mkdir(parents=True, exist_ok=True)
    validator = subprocess.run(
        [sys.executable, str(ROOT / "tools/validate_art.py"),
         "--plan", str(PLAN_PATH), "--manifest", str(LOCK_PATH), "--root", str(RUNTIME_ROOT),
         "--report-json", str(ROOT / "handoff/art/validation_report.json"),
         "--report-md", str(ROOT / "handoff/art/ART_REVIEW.md")],
        capture_output=True, text=True)
    report["validateArt"] = {"returncode": validator.returncode,
                             "stdout": validator.stdout.strip(), "stderr": validator.stderr.strip()}
    if validator.returncode != 0:
        issues.append({"id": "", "path": "tools/validate_art.py",
                       "reason": "validate_art.py reported failures", "detail": validator.stdout.strip()})

    report["ok"] = not issues
    report["issueCount"] = len(issues)
    report["issues"] = issues
    (reports_dir / "preflight_art.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    lines = [
        "# G1 Art Preflight",
        "",
        f"- 결과: {'PASS' if report['ok'] else 'BLOCKED_ART_CONTRACT'}",
        f"- 검사 시각(UTC): {report['generatedAtUtc']}",
        f"- 아트 자산: {report['runtimePngCount']}/54",
        f"- 이슈: {report['issueCount']}",
        "",
        "| 검사 | 결과 | 상세 |",
        "| --- | --- | --- |",
    ]
    for item in report["checks"]:
        lines.append(f"| {item['name']} | {'PASS' if item['ok'] else 'FAIL'} | {item['detail']} |")
    if issues:
        lines.extend(["", "## Issues", ""])
        for item in issues:
            lines.append(f"- {item['path']} — {item['reason']} ({item.get('detail','')})")
    (reports_dir / "preflight_art.md").write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(json.dumps({"ok": report["ok"], "issues": report["issueCount"],
                      "runtimePngCount": report["runtimePngCount"]}, ensure_ascii=False))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
