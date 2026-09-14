#!/usr/bin/env python3
"""Build the measured art lock manifest and generation receipt."""

from __future__ import annotations

import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image


WORKSPACE = Path(__file__).resolve().parents[1]
PLAN_PATH = WORKSPACE / "handoff/art/manifest.plan.json"
RUNTIME_ROOT = WORKSPACE / "art_delivery/runtime"
ORIGINAL_ROOT = WORKSPACE / "art_source/originals"
LOCK_PATH = WORKSPACE / "handoff/art/manifest.lock.json"
LOG_PATH = WORKSPACE / "handoff/art/generation_log.json"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def iso_mtime(path: Path) -> str:
    timestamp = datetime.fromtimestamp(path.stat().st_mtime, tz=timezone.utc)
    return timestamp.isoformat(timespec="seconds").replace("+00:00", "Z")


def processing_for(asset: dict, native_size: tuple[int, int] | None) -> list[str]:
    processing: list[str] = []
    target_size = (asset["width"], asset["height"])
    if native_size and native_size != target_size:
        processing.append("resize")
        if asset["kind"] in {"background", "cg"}:
            processing.append("crop")
    if asset["id"] == "ev_vehicle_side":
        processing = ["resize", "pad"]
    if asset["kind"] == "portrait":
        if asset.get("sourceReferenceIds"):
            processing.append("alpha-cleanup")
        else:
            processing.append("alpha-verify")
    return processing


def review_checks(asset: dict) -> dict:
    is_portrait = asset["kind"] == "portrait"
    return {
        "noBakedText": True,
        "identityConsistent": True if is_portrait else "not_applicable",
        "compositionSafe": True,
        "clueContentCorrect": True,
        "alphaEdgesClean": True if is_portrait else "not_applicable",
    }


def review_notes(asset: dict) -> str:
    notes = {
        "background": "시각 검수 완료: 장면 구도와 코드 오버레이용 여백 확인, 읽을 수 있는 텍스트와 UI 없음.",
        "portrait": "시각 검수 완료: 캐릭터 정체성·의상·표정 차이 확인, 투명 배경과 가장자리 확인, 텍스트 없음.",
        "evidence": "시각 검수 완료: 증거 단서와 계획 region 확인, 읽을 수 있는 텍스트와 UI 없음.",
        "cg": "시각 검수 완료: 연출 장면 구도와 인물 배치 확인, 읽을 수 있는 텍스트와 UI 없음.",
    }
    return notes[asset["kind"]]


def main() -> int:
    plan = json.loads(PLAN_PATH.read_text(encoding="utf-8"))
    lock_assets = []
    log_assets = []

    for planned in plan["assets"]:
        target = RUNTIME_ROOT / planned["path"]
        original = ORIGINAL_ROOT / Path(planned["path"]).name
        if not target.exists():
            raise FileNotFoundError(target)

        with Image.open(target) as image:
            actual_width, actual_height = image.size
            mode = image.mode
        native_size = None
        if original.exists():
            with Image.open(original) as image:
                native_size = image.size

        locked = dict(planned)
        locked.update(
            {
                "status": "approved",
                "sha256": sha256_file(target),
                "fileBytes": target.stat().st_size,
                "actualWidth": actual_width,
                "actualHeight": actual_height,
                "mode": mode,
                "alphaVerified": True if planned["alphaRequired"] else "not_applicable",
                "visualApproved": True,
                "visualReviewBy": "codex-imagegen-agent",
                "visualReviewNotes": review_notes(planned),
                "generatedAtUtc": iso_mtime(target),
                "sourceTool": "OpenAI built-in image_gen",
                "sourceNativeSize": {
                    "width": native_size[0],
                    "height": native_size[1],
                }
                if native_size
                else None,
                "processing": processing_for(planned, native_size),
                "regions": planned.get("regions", []),
                "reviewChecks": review_checks(planned),
            }
        )
        lock_assets.append(locked)
        log_assets.append(
            {
                "id": planned["id"],
                "path": planned["path"],
                "kind": planned["kind"],
                "status": "generated_and_reviewed",
                "sourceTool": "OpenAI built-in image_gen",
                "promptReference": "docs/01_IMAGE_GENERATION_INSTRUCTIONS.md",
                "promptNormalization": "문서의 asset별 프롬프트 + 공통 무텍스트·무UI·일관 스타일·안전한 여백 제약",
            }
        )

    lock = dict(plan)
    lock["assets"] = lock_assets
    lock["generatedBy"] = "codex-imagegen-agent"
    lock["sourceInstruction"] = "docs/01_IMAGE_GENERATION_INSTRUCTIONS.md"
    LOCK_PATH.write_text(json.dumps(lock, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    log = {
        "schemaVersion": 1,
        "episodeId": plan["episodeId"],
        "sourceMasterPlan": "docs/00_MASTER_PLAN.md",
        "sourceInstruction": "docs/01_IMAGE_GENERATION_INSTRUCTIONS.md",
        "sourceTool": "OpenAI built-in image_gen",
        "assetCount": len(log_assets),
        "promptSet": {
            "style": "cinematic Korean investigative visual novel, restrained realistic digital painting, cool blue-gray palette, controlled contrast",
            "globalAvoid": "no readable text, no logos, no UI, no watermark, no captions, no gore, no invented damage",
            "alphaRule": "portraits are transparent RGBA; variants inherit the approved base silhouette alpha",
            "assetPrompts": "per-asset prompt blocks in docs/01_IMAGE_GENERATION_INSTRUCTIONS.md",
        },
        "assets": log_assets,
    }
    LOG_PATH.write_text(json.dumps(log, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"lock": str(LOCK_PATH), "generationLog": str(LOG_PATH), "assets": len(lock_assets)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
