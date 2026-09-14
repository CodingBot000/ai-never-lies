#!/usr/bin/env python3
"""배포 폰트가 정본 콘텐츠의 모든 문자를 표시할 수 있는지 검사한다.

사용:
  python3 tools/check_font_glyphs.py            # 검사 + reports/glyph_report.json 기록
  python3 tools/check_font_glyphs.py --allow-missing  # 누락이 있어도 exit 0

표준 라이브러리만 사용한다. 결과가 비어 있지 않으면 폴백 폰트를 지정하거나
폰트를 교체한 뒤 다시 실행한다.
"""

from __future__ import annotations

import argparse
import json
import struct
import sys
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[1]
FONT_PATHS = [
    WORKSPACE / "web/public/fonts/NanumGothic-Regular.ttf",
    WORKSPACE / "web/public/fonts/NanumGothic-Bold.ttf",
]
CONTENT_DIR = WORKSPACE / "content/ep01"
EXTRA_TEXT_DIR = WORKSPACE / "web/src"
REPORT_PATH = WORKSPACE / "reports/glyph_report.json"

# UI 코드가 직접 표시하는 기호도 포함한다. 콘텐츠 문자열은 별도로 전체를 검사한다.
# (현재 웹 UI가 합성하는 기호만 넣는다. 신규 기호를 코드에 추가하면 여기에도 추가한다.)
REQUIRED_SYMBOLS = "·—→±²×…"


def read_cmap(path: Path) -> set[int]:
    data = path.read_bytes()
    if data[:4] not in (b"\x00\x01\x00\x00", b"true", b"ttcf"):
        raise ValueError(f"not a TrueType font: {path}")
    num_tables = struct.unpack(">H", data[4:6])[0]
    cmap_offset = None
    for index in range(num_tables):
        record = 12 + index * 16
        tag = data[record : record + 4]
        if tag == b"cmap":
            cmap_offset = struct.unpack(">I", data[record + 8 : record + 12])[0]
            break
    if cmap_offset is None:
        raise ValueError(f"cmap table missing: {path}")

    subtable_count = struct.unpack(">H", data[cmap_offset + 2 : cmap_offset + 4])[0]
    subtables = []
    for index in range(subtable_count):
        record = cmap_offset + 4 + index * 8
        platform = struct.unpack(">H", data[record : record + 2])[0]
        encoding = struct.unpack(">H", data[record + 2 : record + 4])[0]
        offset = struct.unpack(">I", data[record + 4 : record + 8])[0]
        subtables.append((platform, encoding, cmap_offset + offset))

    codes: set[int] = set()
    for _platform, _encoding, offset in subtables:
        fmt = struct.unpack(">H", data[offset : offset + 2])[0]
        if fmt == 4:
            seg_x2 = struct.unpack(">H", data[offset + 6 : offset + 8])[0]
            seg = seg_x2 // 2
            ends = struct.unpack(f">{seg}H", data[offset + 14 : offset + 14 + seg_x2])
            starts_offset = offset + 16 + seg_x2
            starts = struct.unpack(f">{seg}H", data[starts_offset : starts_offset + seg_x2])
            deltas_offset = starts_offset + seg_x2
            deltas = struct.unpack(f">{seg}h", data[deltas_offset : deltas_offset + seg_x2])
            ranges_offset = deltas_offset + seg_x2
            for i in range(seg):
                start = starts[i]
                end = ends[i]
                delta = deltas[i]
                range_offset = struct.unpack(">H", data[ranges_offset + i * 2 : ranges_offset + i * 2 + 2])[0]
                if start == 0xFFFF:
                    continue
                for code in range(start, min(end, 0xFFFF) + 1):
                    if range_offset == 0:
                        glyph = (code + delta) & 0xFFFF
                    else:
                        address = ranges_offset + i * 2 + range_offset + (code - start) * 2
                        if address + 2 > len(data):
                            continue
                        glyph = struct.unpack(">H", data[address : address + 2])[0]
                        if glyph:
                            glyph = (glyph + delta) & 0xFFFF
                    if glyph:
                        codes.add(code)
        elif fmt == 12:
            group_count = struct.unpack(">I", data[offset + 12 : offset + 16])[0]
            for group in range(group_count):
                group_offset = offset + 16 + group * 12
                start, end, _glyph = struct.unpack(">III", data[group_offset : group_offset + 12])
                for code in range(start, min(end, 0x10FFFF) + 1):
                    codes.add(code)
    return codes


def collect_content_chars() -> set[str]:
    chars: set[str] = set(REQUIRED_SYMBOLS)
    for path in sorted(CONTENT_DIR.glob("*.json")):
        chars.update(path.read_text(encoding="utf-8"))
    return {char for char in chars if not char.isspace()}


def main() -> int:
    parser = argparse.ArgumentParser(description="Check font glyph coverage for canonical content")
    parser.add_argument("--allow-missing", action="store_true", help="exit 0 even when glyphs are missing")
    args = parser.parse_args()

    content_chars = collect_content_chars()
    report: dict = {
        "checkedChars": len(content_chars),
        "fonts": [],
    }
    failed = False
    for font_path in FONT_PATHS:
        if not font_path.is_file():
            report["fonts"].append({"font": str(font_path.relative_to(WORKSPACE)), "error": "missing"})
            failed = True
            continue
        codes = read_cmap(font_path)
        missing = sorted(char for char in content_chars if ord(char) not in codes)
        report["fonts"].append(
            {
                "font": str(font_path.relative_to(WORKSPACE)),
                "glyphCount": len(codes),
                "missing": missing,
                "missingCount": len(missing),
            }
        )
        if missing:
            failed = True

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"check_font_glyphs.py: checked={len(content_chars)} fonts={len(FONT_PATHS)} report={REPORT_PATH}")
    for font in report["fonts"]:
        if font.get("error"):
            print(f"  - {font['font']}: {font['error']}")
        elif font["missingCount"]:
            print(f"  - {font['font']}: missing {font['missingCount']}: {''.join(font['missing'][:40])}")
        else:
            print(f"  - {font['font']}: all glyphs present")
    if failed and not args.allow_missing:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
