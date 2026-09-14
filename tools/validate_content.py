#!/usr/bin/env python3
"""Static validation of Episode 01 content; writes reports/content_validation_py.json."""

from __future__ import annotations

import json
import sys
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[1]
CONTENT_DIR = WORKSPACE / "Game/Assets/_Game/Content/ep01"
MANIFEST_PATH = WORKSPACE / "handoff/art/manifest.lock.json"
REPORT_PATH = WORKSPACE / "reports/content_validation_py.json"

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

EXPECTED_SCENES = [
    "s000", "s010", "s020",
    "s100", "s110", "s120", "s130", "s140", "s150", "s160", "s170",
    "s200", "s210", "s220", "s230", "s240",
    "s300", "s310", "s320", "s330", "s340",
    "s400", "s410", "s411", "s420", "s430", "s440", "s450", "s460",
    "s500", "s510", "s520", "s530", "s540", "s550",
    "s600", "s610", "s620", "s630", "s640", "s650",
    "s700", "s710",
]
EXPECTED_EVIDENCE = ["ev%02d" % n for n in range(1, 37)]
EXPECTED_PUZZLES = ["p%02d" % n for n in range(1, 21)]

CONDITION_KINDS = {
    "SceneCompleted",
    "EvidenceOwned",
    "EvidenceRead",
    "PuzzleSolved",
    "TopicVisited",
    "BranchEquals",
    "PostDialogueComplete",
    "AllRequiredChildrenComplete",
}
GRANT_TRIGGERS = {"scene_start", "interaction_start", "post_start", "dialogue"}
EFFECT_KINDS = {"SetBranch", "GrantEvidence", "Feedback"}

P20_CORRECT_ITEMS = [
    "unsafe_brake_limiting",
    "radar_fault",
    "override_after_warning",
    "health_check_removed",
    "release_conditions_open",
    "conflicting_kpi",
    "known_risk_continued",
    "misleading_public_summary",
]
P20_LIMIT_ITEM = "no_intent_or_criminal_verdict"
P20_SECTIONS = {"direct", "contributions", "organizational", "postIncident"}
P20_WRONG_ITEMS = [
    "no_one_responsible",
    "all_ai_innocent",
    "intentional_murder_proven",
    "publicity_caused_collision",
]


class Checker:
    def __init__(self):
        self.checks = []
        self.issue_total = 0

    def add(self, name, issues):
        issues = [str(item) for item in issues]
        if issues:
            self.issue_total += len(issues)
        detail = "; ".join(issues)
        if len(detail) > 6000:
            detail = detail[:6000] + " ... (truncated, %d issues)" % len(issues)
        self.checks.append({"name": name, "ok": not issues, "detail": detail})

    def failed_names(self):
        return [check["name"] for check in self.checks if not check["ok"]]

    def report(self):
        return {
            "ok": not self.failed_names(),
            "checks": self.checks,
            "errorCount": self.issue_total,
        }


def load_content():
    data = {}
    issues = []
    for name in CONTENT_FILES:
        path = CONTENT_DIR / name
        if not path.exists():
            issues.append("missing file %s" % name)
            continue
        try:
            with path.open(encoding="utf-8") as handle:
                data[name] = json.load(handle)
        except Exception as exc:
            issues.append("%s: parse error: %s" % (name, exc))
    return data, issues


def duplicate_issues(label, items):
    counts = {}
    for item in items:
        item_id = item.get("id")
        counts[item_id] = counts.get(item_id, 0) + 1
    return [
        "%s duplicate id %r x%d" % (label, item_id, count)
        for item_id, count in sorted(counts.items(), key=lambda pair: str(pair[0]))
        if count > 1
    ]


def id_set_issues(label, expected, items):
    actual = [item.get("id") for item in items]
    actual_set = set(actual)
    issues = []
    if len(actual) != len(expected):
        issues.append("%s count %d != %d" % (label, len(actual), len(expected)))
    for item_id in expected:
        if item_id not in actual_set:
            issues.append("%s missing %s" % (label, item_id))
    for item_id in actual:
        if item_id not in set(expected):
            issues.append("%s unexpected %s" % (label, item_id))
    return issues


def check_scene_dialogue_refs(scenes, dialogues):
    line_ids = {line["id"]: line for line in dialogues}
    issues = []
    for scene in scenes:
        for key in ("preDialogueIds", "postDialogueIds"):
            refs = scene.get(key) or []
            if len(refs) != len(set(refs)):
                issues.append("scene %s %s contains duplicate dialogue references" % (scene["id"], key))
            for line_id in refs:
                if line_id not in line_ids:
                    issues.append("scene %s %s unknown dialogue %s" % (scene["id"], key, line_id))
                elif line_ids[line_id].get("sceneId") != scene["id"]:
                    issues.append(
                        "scene %s %s line %s belongs to scene %s"
                        % (scene["id"], key, line_id, line_ids[line_id].get("sceneId"))
                    )
    return issues


def check_scene_grant_refs(scenes, evidence_ids, line_ids):
    issues = []
    for scene in scenes:
        for grant in scene.get("grants") or []:
            evidence_id = grant.get("evidenceId")
            trigger = grant.get("trigger")
            if evidence_id not in evidence_ids:
                issues.append("scene %s grant unknown evidence %s" % (scene["id"], evidence_id))
            if trigger not in GRANT_TRIGGERS:
                issues.append("scene %s grant unknown trigger %s" % (scene["id"], trigger))
            if trigger == "dialogue":
                line_id = grant.get("atDialogueId")
                if not line_id:
                    issues.append("scene %s dialogue grant for %s has no atDialogueId" % (scene["id"], evidence_id))
                elif line_id not in line_ids:
                    issues.append("scene %s dialogue grant unknown line %s" % (scene["id"], line_id))
                elif line_ids[line_id].get("sceneId") != scene["id"]:
                    issues.append(
                        "scene %s dialogue grant line %s belongs to scene %s"
                        % (scene["id"], line_id, line_ids[line_id].get("sceneId"))
                    )
    return issues


def check_scene_conditions(scenes):
    issues = []
    scene_ids = {scene["id"] for scene in scenes}
    for scene in scenes:
        for condition in scene.get("entryConditions") or []:
            if condition.get("kind") not in CONDITION_KINDS:
                issues.append("scene %s entry unknown condition kind %s" % (scene["id"], condition.get("kind")))
        for transition in scene.get("transitions") or []:
            target = transition.get("target")
            if target not in scene_ids:
                issues.append("scene %s transition target unknown scene %s" % (scene["id"], target))
            for condition in transition.get("conditions") or []:
                if condition.get("kind") not in CONDITION_KINDS:
                    issues.append(
                        "scene %s transition %s unknown condition kind %s"
                        % (scene["id"], target, condition.get("kind"))
                    )
    return issues


def check_interactions(scenes, puzzles):
    puzzle_map = {puzzle["id"]: puzzle for puzzle in puzzles}
    scene_map = {scene["id"]: scene for scene in scenes}
    issues = []
    for scene in scenes:
        for puzzle_id in scene.get("interactionIds") or []:
            if puzzle_id not in puzzle_map:
                issues.append("scene %s interaction unknown puzzle %s" % (scene["id"], puzzle_id))
            elif puzzle_map[puzzle_id].get("sceneId") != scene["id"]:
                issues.append(
                    "scene %s interaction %s declared in scene %s"
                    % (scene["id"], puzzle_id, puzzle_map[puzzle_id].get("sceneId"))
                )
    for puzzle in puzzles:
        scene = scene_map.get(puzzle.get("sceneId"))
        if scene is None:
            issues.append("puzzle %s unknown scene %s" % (puzzle["id"], puzzle.get("sceneId")))
        elif puzzle["id"] not in (scene.get("interactionIds") or []):
            issues.append("puzzle %s not listed in scene %s interactionIds" % (puzzle["id"], scene["id"]))
    return issues


def check_branches(scenes, branches):
    scene_map = {scene["id"]: scene for scene in scenes}
    issues = []
    for branch in branches:
        scene = scene_map.get(branch.get("sceneId"))
        if scene is None:
            issues.append("branch %s unknown scene %s" % (branch["id"], branch.get("sceneId")))
            continue
        if branch.get("groupId") not in (scene.get("branchGroupIds") or []):
            issues.append(
                "branch %s group %s not listed in scene %s.branchGroupIds"
                % (branch["id"], branch.get("groupId"), scene["id"])
            )
    for scene in scenes:
        for group_id in scene.get("branchGroupIds") or []:
            group = [b for b in branches if b.get("sceneId") == scene["id"] and b.get("groupId") == group_id]
            if not group:
                issues.append("scene %s branch group %s has no branches" % (scene["id"], group_id))
        for group_id in scene.get("advancingGroupIds") or []:
            group = [b for b in branches if b.get("sceneId") == scene["id"] and b.get("groupId") == group_id]
            if not group:
                issues.append("scene %s advancing group %s has no branches" % (scene["id"], group_id))
            elif not any(branch.get("advances") for branch in group):
                issues.append("scene %s advancing group %s has no advancing branch" % (scene["id"], group_id))
    return issues


def check_branch_effects(branches, evidence_ids):
    issues = []
    for branch in branches:
        for effect in branch.get("effects") or []:
            kind = effect.get("kind")
            if kind not in EFFECT_KINDS:
                issues.append("branch %s unknown effect kind %s" % (branch["id"], kind))
            if kind == "GrantEvidence" and effect.get("id") not in evidence_ids:
                issues.append("branch %s grant unknown evidence %s" % (branch["id"], effect.get("id")))
            if kind == "SetBranch" and (not effect.get("key") or effect.get("value") in (None, "")):
                issues.append("branch %s SetBranch effect missing key/value" % branch["id"])
    return issues


def check_puzzle_evidence_refs(puzzles, evidence_ids):
    issues = []
    for puzzle in puzzles:
        for evidence_id in puzzle.get("requiredEvidenceIds") or []:
            if evidence_id not in evidence_ids:
                issues.append("puzzle %s requiredEvidenceIds unknown evidence %s" % (puzzle["id"], evidence_id))
    return issues


def check_puzzle_targets(puzzles, testimony_ids):
    issues = []
    for puzzle in puzzles:
        target = puzzle.get("targetStatementId")
        if target and target not in testimony_ids:
            issues.append("puzzle %s targetStatementId unknown statement %s" % (puzzle["id"], target))
        if puzzle.get("type") == "statement_refutation" and not target:
            issues.append("puzzle %s statement_refutation has no targetStatementId" % puzzle["id"])
    return issues


def collect_evidence_ids(sets):
    ids = []
    for evidence_set in sets or []:
        if isinstance(evidence_set, list):
            ids.extend(evidence_set)
        elif isinstance(evidence_set, dict):
            ids.extend(evidence_set.get("requiredIds") or [])
    return ids


def check_puzzle_sufficient_sets(puzzles, evidence_ids):
    issues = []
    for puzzle in puzzles:
        for evidence_id in collect_evidence_ids(puzzle.get("sufficientEvidenceSets")):
            if evidence_id not in evidence_ids:
                issues.append("puzzle %s sufficientEvidenceSets unknown evidence %s" % (puzzle["id"], evidence_id))
        for index, proof in enumerate(puzzle.get("edgeProofRequirements") or []):
            if "sufficientEvidenceSets" not in proof:
                fields = sorted(key for key in proof if key not in ("from", "to", "kind"))
                issues.append(
                    "puzzle %s edgeProofRequirements[%d] uses fields %s instead of DTO field "
                    "'sufficientEvidenceSets' (Unity JsonUtility leaves it null)"
                    % (puzzle["id"], index, fields)
                )
                for evidence_id in collect_evidence_ids(proof.get("evidenceSets")):
                    if evidence_id not in evidence_ids:
                        issues.append(
                            "puzzle %s edgeProofRequirements[%d] unknown evidence %s"
                            % (puzzle["id"], index, evidence_id)
                        )
            else:
                for evidence_id in collect_evidence_ids(proof.get("sufficientEvidenceSets")):
                    if evidence_id not in evidence_ids:
                        issues.append(
                            "puzzle %s edgeProofRequirements[%d] unknown evidence %s"
                            % (puzzle["id"], index, evidence_id)
                        )
        for index, proof in enumerate(puzzle.get("reportProofRequirements") or []):
            if "sufficientEvidenceSets" not in proof:
                issues.append("puzzle %s reportProofRequirements[%d] missing sufficientEvidenceSets" % (puzzle["id"], index))
                continue
            for evidence_id in collect_evidence_ids(proof.get("sufficientEvidenceSets")):
                if evidence_id not in evidence_ids:
                    issues.append(
                        "puzzle %s reportProofRequirements[%d] unknown evidence %s"
                        % (puzzle["id"], index, evidence_id)
                    )
    return issues


def check_puzzle_edges(puzzles, graph_node_ids):
    issues = []
    for puzzle in puzzles:
        for edge in puzzle.get("requiredEdges") or []:
            if edge.get("from") not in graph_node_ids:
                issues.append("puzzle %s required edge unknown node %s" % (puzzle["id"], edge.get("from")))
            if edge.get("to") not in graph_node_ids:
                issues.append("puzzle %s required edge unknown node %s" % (puzzle["id"], edge.get("to")))
            if edge.get("kind") not in ("causal", "signal"):
                issues.append("puzzle %s required edge unknown kind %s" % (puzzle["id"], edge.get("kind")))
    return issues


def check_p20(puzzle_map):
    issues = []
    puzzle = puzzle_map.get("p20")
    if puzzle is None:
        return ["puzzles.json has no p20"]
    if puzzle.get("sceneId") != "s640":
        issues.append("p20 sceneId %s != s640" % puzzle.get("sceneId"))
    proofs = puzzle.get("reportProofRequirements") or []
    items = [proof.get("item") for proof in proofs]
    if len(items) != 8 or set(items) != set(P20_CORRECT_ITEMS) or len(set(items)) != len(items):
        issues.append("p20 reportProofRequirements items %s != the 8 correct items" % items)
    for proof in proofs:
        if proof.get("section") not in P20_SECTIONS:
            issues.append(
                "p20 reportProofRequirements item %s unknown section %s" % (proof.get("item"), proof.get("section"))
            )
    choices = puzzle.get("correctChoiceIds") or []
    expected_choices = set(P20_CORRECT_ITEMS) | {P20_LIMIT_ITEM}
    if set(choices) != expected_choices or len(choices) != len(expected_choices):
        issues.append("p20 correctChoiceIds %s != 8 correct items + limit item" % sorted(choices))
    blob = json.dumps(puzzle, ensure_ascii=False)
    for wrong in P20_WRONG_ITEMS:
        if wrong in blob:
            issues.append("p20 data contains wrong palette item %s" % wrong)
    return issues


def collect_grant_sources(scenes, branches, puzzle_map):
    sources = {}
    for scene in scenes:
        for grant in scene.get("grants") or []:
            source = "scene:%s:%s" % (scene["id"], grant.get("trigger"))
            if grant.get("atDialogueId"):
                source += ":%s" % grant["atDialogueId"]
            sources.setdefault(grant.get("evidenceId"), set()).add(source)
    for branch in branches:
        for effect in branch.get("effects") or []:
            if effect.get("kind") == "GrantEvidence":
                sources.setdefault(effect.get("id"), set()).add("branch:%s" % branch["id"])
    for puzzle in puzzle_map.values():
        for region in puzzle.get("regions") or []:
            if region.get("evidenceId"):
                sources.setdefault(region["evidenceId"], set()).add(
                    "region:%s:%s" % (puzzle["id"], region.get("regionId"))
                )
    return sources


def check_evidence_coverage(scenes, branches, puzzle_map):
    sources = collect_grant_sources(scenes, branches, puzzle_map)
    issues = []
    for evidence_id in EXPECTED_EVIDENCE:
        if evidence_id in ("ev34", "ev35"):
            continue
        if not sources.get(evidence_id):
            issues.append("%s never granted by scene grant, branch effect, or p01 region" % evidence_id)
    ev34_sources = sources.get("ev34", set())
    if ev34_sources != {"scene:s520:dialogue:dlg_s520_007"}:
        issues.append("ev34 grants %s != {scene:s520:dialogue:dlg_s520_007}" % sorted(ev34_sources))
    ev35_sources = sources.get("ev35", set())
    if ev35_sources != {"branch:b_s530_accept"}:
        issues.append("ev35 grants %s != {branch:b_s530_accept}" % sorted(ev35_sources))
    return issues


def check_contracts(scene_map, puzzle_map):
    issues = []
    s150 = scene_map.get("s150")
    if s150 is None:
        issues.append("s150 missing")
    else:
        grants = {
            grant.get("evidenceId"): grant.get("trigger")
            for grant in s150.get("grants") or []
        }
        if grants.get("ev08") != "post_start":
            issues.append("s150 must grant ev08 on post_start, got %s" % grants.get("ev08"))
        if grants.get("ev09") != "post_start":
            issues.append("s150 must grant ev09 on post_start, got %s" % grants.get("ev09"))
    s640 = scene_map.get("s640")
    if s640 is None:
        issues.append("s640 missing")
    else:
        for condition in s640.get("entryConditions") or []:
            if condition.get("kind") == "PuzzleSolved" and condition.get("id") == "p20":
                issues.append("s640 entryConditions must not require p20 (circular lock)")
        if "p20" not in (s640.get("interactionIds") or []):
            issues.append("s640 interactionIds must contain p20")
    p20 = puzzle_map.get("p20")
    if p20 is not None and p20.get("sceneId") != "s640":
        issues.append("p20 must belong to s640")
    return issues


def check_hints(hints, scene_ids, puzzle_ids):
    issues = []
    if len(hints) != 103:
        issues.append("hints total %d != 103" % len(hints))
    puzzle_levels = {}
    scene_hints = {}
    for hint in hints:
        scope_type = hint.get("scopeType")
        scope_id = hint.get("scopeId")
        if not hint.get("text"):
            issues.append("hint %s has empty text" % hint.get("id"))
        if scope_type == "puzzle":
            puzzle_levels.setdefault(scope_id, set()).add(hint.get("level"))
            if scope_id not in puzzle_ids:
                issues.append("hint %s unknown puzzle scope %s" % (hint.get("id"), scope_id))
        elif scope_type == "scene":
            scene_hints.setdefault(scope_id, []).append(hint.get("level"))
            if scope_id not in scene_ids:
                issues.append("hint %s unknown scene scope %s" % (hint.get("id"), scope_id))
        else:
            issues.append("hint %s unknown scopeType %s" % (hint.get("id"), scope_type))
    puzzle_hint_count = sum(len(levels) for levels in puzzle_levels.values())
    if puzzle_hint_count != 60:
        issues.append("puzzle hint levels %d != 60" % puzzle_hint_count)
    for puzzle_id in sorted(puzzle_ids):
        if puzzle_levels.get(puzzle_id) != {1, 2, 3}:
            issues.append("puzzle %s hint levels %s != H1/H2/H3" % (puzzle_id, sorted(puzzle_levels.get(puzzle_id, []))))
    if len(scene_hints) != 43:
        issues.append("scene hints cover %d scenes != 43" % len(scene_hints))
    for scene_id in sorted(scene_ids):
        if scene_id not in scene_hints:
            issues.append("scene %s has no tip" % scene_id)
    return issues


def check_texts(dialogues, evidence, testimonies):
    issues = []
    for line in dialogues:
        if not (line.get("text") or "").strip():
            issues.append("dialogue %s empty text" % line.get("id"))
    for item in evidence:
        for key in ("body", "proves", "limits"):
            if not (item.get(key) or "").strip():
                issues.append("evidence %s empty %s" % (item.get("id"), key))
    for statement in testimonies:
        if not (statement.get("text") or "").strip():
            issues.append("testimony %s empty text" % statement.get("id"))
        for press in statement.get("pressResponse") or []:
            if not (press.get("text") or "").strip():
                issues.append("testimony %s empty press response text" % statement.get("id"))
    return issues


def check_assets(scenes, dialogues, evidence):
    issues = []
    if not MANIFEST_PATH.exists():
        return ["manifest missing: %s" % MANIFEST_PATH]
    with MANIFEST_PATH.open(encoding="utf-8") as handle:
        manifest = json.load(handle)
    assets = manifest.get("assets") or []
    asset_ids = {asset.get("id") for asset in assets}
    if manifest.get("requiredAssetCount") != 54:
        issues.append("manifest requiredAssetCount %s != 54" % manifest.get("requiredAssetCount"))
    if len(asset_ids) != 54:
        issues.append("manifest id set size %d != 54" % len(asset_ids))
    for scene in scenes:
        asset_id = scene.get("backgroundAssetId")
        if not asset_id:
            issues.append("scene %s has no backgroundAssetId" % scene["id"])
        elif asset_id not in asset_ids:
            issues.append("scene %s backgroundAssetId %s not in manifest" % (scene["id"], asset_id))
    for line in dialogues:
        asset_id = line.get("portraitAssetId")
        if asset_id and asset_id not in asset_ids:
            issues.append("dialogue %s portraitAssetId %s not in manifest" % (line["id"], asset_id))
    for item in evidence:
        asset_id = item.get("imageAssetId")
        if asset_id and asset_id not in asset_ids:
            issues.append("evidence %s imageAssetId %s not in manifest" % (item["id"], asset_id))
    return issues


def write_report(report):
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with REPORT_PATH.open("w", encoding="utf-8") as handle:
        json.dump(report, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def main():
    checker = Checker()
    data, parse_issues = load_content()
    checker.add("content.parse", parse_issues)
    if parse_issues:
        report = checker.report()
        write_report(report)
        print("validate_content.py: ok=%s checks=%d failed=%s" % (report["ok"], len(report["checks"]), checker.failed_names()))
        return 1

    episode_file = data["episode.json"]
    scenes = data["scenes.json"]["items"]
    dialogues = data["dialogues.ko.json"]["items"]
    evidence = data["evidence.json"]["items"]
    testimonies = data["testimonies.json"]["items"]
    branches = data["branches.json"]["items"]
    puzzles = data["puzzles.json"]["items"]
    hints = data["hints.ko.json"]["items"]
    ui_strings = data["ui.ko.json"]["items"]
    graph = data["graph.json"]
    matrix = data["matrix.json"]

    scene_map = {scene["id"]: scene for scene in scenes}
    puzzle_map = {puzzle["id"]: puzzle for puzzle in puzzles}
    evidence_ids = {item["id"] for item in evidence}
    testimony_ids = {item["id"] for item in testimonies}
    dialogue_map = {line["id"]: line for line in dialogues}
    scene_ids = {scene["id"] for scene in scenes}
    puzzle_ids = {puzzle["id"] for puzzle in puzzles}
    graph_node_ids = {node["id"] for node in graph.get("nodes") or []}

    episode_issues = []
    if episode_file.get("schemaVersion") != 1:
        episode_issues.append("episode schemaVersion != 1")
    episode = episode_file.get("episode") or {}
    if episode.get("id") != "ep01":
        episode_issues.append("episode.id %s != ep01" % episode.get("id"))
    if episode.get("contentVersion") != "ep01.v1.0.0":
        episode_issues.append("episode.contentVersion %s != ep01.v1.0.0" % episode.get("contentVersion"))
    if episode.get("startSceneId") != "s000":
        episode_issues.append("episode.startSceneId %s != s000" % episode.get("startSceneId"))
    if sorted(episode.get("requiredPuzzleIds") or []) != EXPECTED_PUZZLES:
        episode_issues.append("episode.requiredPuzzleIds != p01..p20")
    if sorted(episode.get("optionalSceneIds") or []) != ["s520", "s530"]:
        episode_issues.append("episode.optionalSceneIds != [s520, s530]")

    checker.add("episode.contract", episode_issues)
    checker.add("ids.scenes", id_set_issues("scene", EXPECTED_SCENES, scenes))
    checker.add("ids.evidence", id_set_issues("evidence", EXPECTED_EVIDENCE, evidence))
    checker.add("ids.puzzles", id_set_issues("puzzle", EXPECTED_PUZZLES, puzzles))

    duplicate_issues_all = []
    duplicate_issues_all += duplicate_issues("scene", scenes)
    duplicate_issues_all += duplicate_issues("dialogue", dialogues)
    duplicate_issues_all += duplicate_issues("evidence", evidence)
    duplicate_issues_all += duplicate_issues("testimony", testimonies)
    duplicate_issues_all += duplicate_issues("branch", branches)
    duplicate_issues_all += duplicate_issues("puzzle", puzzles)
    duplicate_issues_all += duplicate_issues("hint", hints)
    duplicate_issues_all += duplicate_issues("ui", ui_strings)
    duplicate_issues_all += duplicate_issues("graph node", graph.get("nodes") or [])
    duplicate_issues_all += duplicate_issues("graph event", graph.get("eventLog") or [])
    row_indexes = [row.get("index") for row in matrix.get("rows") or []]
    if len(row_indexes) != len(set(row_indexes)):
        duplicate_issues_all.append("matrix duplicate row index")
    checker.add("ids.duplicates", duplicate_issues_all)

    checker.add("scene.dialogue_refs", check_scene_dialogue_refs(scenes, dialogues))
    checker.add("scene.grant_refs", check_scene_grant_refs(scenes, evidence_ids, dialogue_map))
    checker.add("scene.transition_refs", check_scene_conditions(scenes))
    checker.add("scene.interaction_refs", check_interactions(scenes, puzzles))
    checker.add("branch.refs", check_branches(scenes, branches))
    checker.add("branch.effects", check_branch_effects(branches, evidence_ids))
    checker.add("puzzle.evidence_refs", check_puzzle_evidence_refs(puzzles, evidence_ids))
    checker.add("puzzle.target_statements", check_puzzle_targets(puzzles, testimony_ids))
    checker.add("puzzle.sufficient_evidence_sets", check_puzzle_sufficient_sets(puzzles, evidence_ids))
    checker.add("puzzle.required_edges", check_puzzle_edges(puzzles, graph_node_ids))
    checker.add("puzzle.p20_report", check_p20(puzzle_map))
    checker.add("evidence.coverage", check_evidence_coverage(scenes, branches, puzzle_map))
    checker.add("contract.s150_grants", check_contracts(scene_map, puzzle_map))
    checker.add("hints.coverage", check_hints(hints, scene_ids, puzzle_ids))
    checker.add("text.completeness", check_texts(dialogues, evidence, testimonies))
    checker.add("assets.manifest", check_assets(scenes, dialogues, evidence))

    report = checker.report()
    write_report(report)
    failed = checker.failed_names()
    summary = "validate_content.py: ok=%s checks=%d failed=%d errorCount=%d" % (
        report["ok"],
        len(report["checks"]),
        len(failed),
        report["errorCount"],
    )
    if failed:
        summary += " failed_checks=%s" % ",".join(failed)
    print(summary)
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
