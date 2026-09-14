#!/usr/bin/env python3
"""Simulate Episode 01 story routes; writes reports/route_validation.json."""

from __future__ import annotations

import itertools
import json
import sys
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[1]
CONTENT_DIR = WORKSPACE / "Game/Assets/_Game/Content/ep01"
REPORT_PATH = WORKSPACE / "reports/route_validation.json"

EXPECTED_PUZZLES = ["p%02d" % n for n in range(1, 21)]
REQUIRED_EVIDENCE = ["ev%02d" % n for n in range(1, 34)] + ["ev36"]
EXPECTED_PUZZLE_SET = set(EXPECTED_PUZZLES)
REQUIRED_EVIDENCE_SET = set(REQUIRED_EVIDENCE)

CANONICAL_ROUTE = [
    "s000", "s010", "s020",
    "s100", "s110", "s100", "s120", "s100", "s130", "s100",
    "s140", "s150", "s160", "s170",
    "s200", "s210", "s200", "s220", "s200", "s230", "s200",
    "s240",
    "s300", "s310", "s300", "s320", "s300", "s330", "s300",
    "s340",
    "s400", "s410", "s411", "s400", "s420", "s400", "s430", "s400",
    "s440", "s450", "s460",
    "s500", "s510", "s500", "s540", "s500",
    "s550",
    "s600", "s610", "s620", "s630", "s640", "s650",
    "s700", "s710",
]


def load_file(name):
    with (CONTENT_DIR / name).open(encoding="utf-8") as handle:
        return json.load(handle)


class Content:
    def __init__(self):
        self.scenes = {scene["id"]: scene for scene in load_file("scenes.json")["items"]}
        self.dialogues = {line["id"]: line for line in load_file("dialogues.ko.json")["items"]}
        self.branches = load_file("branches.json")["items"]
        self.puzzles = {puzzle["id"]: puzzle for puzzle in load_file("puzzles.json")["items"]}


class Simulator:
    def __init__(self, content):
        self.content = content
        self.owned = set()
        self.read = set()
        self.visited = []
        self.completed = set()
        self.solved = set()
        self.topics = []
        self.branch_values = {}
        self.pre_done = set()
        self.post_done = set()
        self.applied = []
        self.errors = []

    def to_dict(self):
        return {
            "owned": sorted(self.owned),
            "read": sorted(self.read),
            "visited": list(self.visited),
            "completed": sorted(self.completed),
            "solved": sorted(self.solved),
            "topics": list(self.topics),
            "branchValues": [[key, self.branch_values[key]] for key in sorted(self.branch_values)],
            "preDone": sorted(self.pre_done),
            "postDone": sorted(self.post_done),
            "applied": list(self.applied),
            "errors": list(self.errors),
        }

    @classmethod
    def from_dict(cls, content, data):
        sim = cls(content)
        sim.owned = set(data.get("owned") or [])
        sim.read = set(data.get("read") or [])
        sim.visited = list(data.get("visited") or [])
        sim.completed = set(data.get("completed") or [])
        sim.solved = set(data.get("solved") or [])
        sim.topics = list(data.get("topics") or [])
        sim.branch_values = {key: value for key, value in data.get("branchValues") or []}
        sim.pre_done = set(data.get("preDone") or [])
        sim.post_done = set(data.get("postDone") or [])
        sim.applied = list(data.get("applied") or [])
        sim.errors = list(data.get("errors") or [])
        return sim

    def cond_ok(self, condition):
        kind = condition.get("kind")
        target = condition.get("id")
        if kind == "SceneCompleted":
            return target in self.completed
        if kind == "EvidenceOwned":
            return target in self.owned
        if kind == "EvidenceRead":
            return target in self.read
        if kind == "PuzzleSolved":
            return target in self.solved
        if kind in ("TopicVisited", "BranchEquals"):
            return target in self.topics
        if kind == "PostDialogueComplete":
            return target in self.post_done or target in self.completed
        if kind == "AllRequiredChildrenComplete":
            scene = self.content.scenes.get(target)
            if scene is None or not scene.get("childSceneIds"):
                return False
            return all(child in self.completed for child in scene["childSceneIds"])
        return False

    def conditions_ok(self, conditions):
        return all(self.cond_ok(condition) for condition in conditions or [])

    def auto_read(self, conditions):
        for condition in conditions or []:
            if condition.get("kind") == "EvidenceRead" and condition.get("id") in self.owned:
                self.read.add(condition["id"])

    def apply_grants(self, scene, trigger):
        if trigger == "dialogue":
            return
        for grant in scene.get("grants") or []:
            if grant.get("trigger") != trigger:
                continue
            effect_id = "grant:%s:%s" % (scene["id"], grant.get("evidenceId"))
            if effect_id not in self.applied:
                self.applied.append(effect_id)
                self.owned.add(grant["evidenceId"])

    def apply_line_grants(self, scene, line_id):
        for grant in scene.get("grants") or []:
            if grant.get("trigger") != "dialogue" or grant.get("atDialogueId") != line_id:
                continue
            effect_id = "grant:%s:%s" % (scene["id"], grant.get("evidenceId"))
            if effect_id not in self.applied:
                self.applied.append(effect_id)
                self.owned.add(grant["evidenceId"])

    def choose_branch(self, scene, group_id, forced_branch_id):
        group = [
            branch
            for branch in self.content.branches
            if branch.get("sceneId") == scene["id"] and branch.get("groupId") == group_id
        ]
        if not group:
            self.errors.append("scene %s branch group %s has no branches" % (scene["id"], group_id))
            return None
        if forced_branch_id:
            for branch in group:
                if branch["id"] == forced_branch_id:
                    return branch
            self.errors.append(
                "scene %s branch group %s has no forced branch %s" % (scene["id"], group_id, forced_branch_id)
            )
        advancing = [branch for branch in group if branch.get("advances")]
        pool = advancing or group
        correct = [branch for branch in pool if branch.get("correct")]
        return (correct or pool)[0]

    def process_scene(self, scene_id, forced=None, shallow=False, mid=None):
        scene = self.content.scenes[scene_id]
        if scene_id not in self.visited:
            self.visited.append(scene_id)
        if shallow:
            return self
        self.apply_grants(scene, "scene_start")
        for line_id in scene.get("preDialogueIds") or []:
            self.apply_line_grants(scene, line_id)
        self.pre_done.add(scene_id)
        self.apply_grants(scene, "interaction_start")
        if mid is not None:
            replacement = mid(self)
            if replacement is not None:
                self = replacement
        forced = forced or {}
        for group_id in scene.get("branchGroupIds") or []:
            branch = self.choose_branch(scene, group_id, forced.get(group_id))
            if branch is None:
                continue
            if branch["id"] not in self.topics:
                self.topics.append(branch["id"])
            for effect in branch.get("effects") or []:
                if effect.get("kind") == "SetBranch":
                    self.branch_values[effect.get("key")] = effect.get("value")
                elif effect.get("kind") == "GrantEvidence":
                    self.owned.add(effect.get("id"))
        for puzzle_id in scene.get("interactionIds") or []:
            puzzle = self.content.puzzles.get(puzzle_id)
            if puzzle is None:
                self.errors.append("scene %s interaction unknown puzzle %s" % (scene_id, puzzle_id))
                continue
            if puzzle.get("type") == "hotspot_set":
                for region in puzzle.get("regions") or []:
                    if region.get("evidenceId"):
                        self.owned.add(region["evidenceId"])
            missing = [ev for ev in puzzle.get("requiredEvidenceIds") or [] if ev not in self.owned]
            if missing:
                self.errors.append(
                    "scene %s puzzle %s missing required evidence: %s" % (scene_id, puzzle_id, ",".join(missing))
                )
            self.solved.add(puzzle_id)
        for evidence_id in scene.get("requiredReadEvidenceIds") or []:
            if evidence_id in self.owned:
                self.read.add(evidence_id)
            else:
                self.errors.append("scene %s required read %s not owned" % (scene_id, evidence_id))
        if scene.get("postDialogueIds"):
            self.apply_grants(scene, "post_start")
            for line_id in scene["postDialogueIds"]:
                self.apply_line_grants(scene, line_id)
        self.post_done.add(scene_id)
        self.completed.add(scene_id)
        return self

    def can_move(self, previous_id, target_id):
        previous = self.content.scenes[previous_id]
        target = self.content.scenes[target_id]
        for condition in target.get("entryConditions") or []:
            if not self.cond_ok(condition):
                return False, "entry condition %s:%s not met" % (condition.get("kind"), condition.get("id"))
        children = (previous.get("childSceneIds") or []) + (previous.get("optionalChildSceneIds") or [])
        if target_id in children:
            return True, ""
        parent_children = (target.get("childSceneIds") or []) + (target.get("optionalChildSceneIds") or [])
        if previous_id in parent_children:
            return True, ""
        for transition in previous.get("transitions") or []:
            if transition.get("target") != target_id:
                continue
            self.auto_read(transition.get("conditions"))
            if self.conditions_ok(transition.get("conditions")):
                return True, ""
        return False, "no enabled transition"


def build_route(hub, children, prefix, suffix):
    route = list(prefix)
    for child in children:
        route += [hub, child]
    route += [hub]
    route += list(suffix)
    return route


def build_routes():
    routes = []
    s100_prefix = CANONICAL_ROUTE[:3]
    s100_suffix = CANONICAL_ROUTE[10:]
    for perm in itertools.permutations(["s110", "s120", "s130"]):
        routes.append(
            (
                "s100_order_" + "_".join(perm),
                build_route("s100", perm, s100_prefix, s100_suffix),
                {},
                set(),
                "base",
                set(),
            )
        )
    s200_prefix = CANONICAL_ROUTE[:14]
    s200_suffix = CANONICAL_ROUTE[21:]
    for perm in itertools.permutations(["s210", "s220", "s230"]):
        routes.append(
            (
                "s200_order_" + "_".join(perm),
                build_route("s200", perm, s200_prefix, s200_suffix),
                {},
                set(),
                "base",
                set(),
            )
        )
    s400_prefix = CANONICAL_ROUTE[:30]
    s400_suffix = CANONICAL_ROUTE[38:]
    for perm in itertools.permutations(["s410", "s411", "s420", "s430"]):
        if perm.index("s411") < perm.index("s410"):
            continue
        routes.append(
            (
                "s400_order_" + "_".join(perm),
                build_route("s400", perm, s400_prefix, s400_suffix),
                {},
                set(),
                "base",
                set(),
            )
        )
    s500_prefix = CANONICAL_ROUTE[:41]
    s500_suffix = CANONICAL_ROUTE[46:]
    s500_required_orders = [("s510", "s540"), ("s540", "s510")]
    s500_optional_patterns = [(), ("s520",), ("s530",), ("s520", "s530")]
    for required_order in s500_required_orders:
        for pattern in s500_optional_patterns:
            optional_children = [child for child in pattern]
            route = list(s500_prefix) + ["s500"]
            for child in optional_children + list(required_order):
                route += [child, "s500"]
            route += list(s500_suffix)
            name = "s500_req_%s_opt_%s" % (
                "-".join(required_order),
                "-".join(optional_children) if optional_children else "none",
            )
            forced = {"s530": {"cg_s530": "b_s530_decline"}} if "s530" in optional_children else {}
            shallow = {"s520"} if "s520" in optional_children else set()
            routes.append((name, route, forced, shallow, "base", set()))
    optional_specs = [
        ("s500_optional_s520_full", ("s510", "s540"), ("s520",), {}, set(), {"ev34"}),
        (
            "s500_optional_s530_accept",
            ("s510", "s540"),
            ("s530",),
            {"s530": {"cg_s530": "b_s530_accept"}},
            set(),
            {"ev35"},
        ),
        (
            "s500_optional_both",
            ("s510", "s540"),
            ("s520", "s530"),
            {"s530": {"cg_s530": "b_s530_accept"}},
            set(),
            {"ev34", "ev35"},
        ),
    ]
    for name, required_order, optional_children, forced, shallow, delta in optional_specs:
        route = list(s500_prefix) + ["s500"]
        for child in list(optional_children) + list(required_order):
            route += [child, "s500"]
        route += list(s500_suffix)
        routes.append((name, route, forced, shallow, "optional:" + ",".join(sorted(delta)), set()))
    routes.append(("canonical_13_1", list(CANONICAL_ROUTE), {}, set(), "base", set()))
    routes.append(
        (
            "canonical_save_load_3pt",
            list(CANONICAL_ROUTE),
            {},
            set(),
            "save_load",
            {"s150", "s310", "s610"},
        )
    )
    return routes


def run_route(content, spec):
    name, sequence, forced, shallow, kind, save_points = spec
    sim = Simulator(content)
    for index, scene_id in enumerate(sequence):
        if index > 0:
            ok, reason = sim.can_move(sequence[index - 1], scene_id)
            if not ok:
                sim.errors.append("%s: move %s->%s rejected: %s" % (name, sequence[index - 1], scene_id, reason))
        mid = None
        if save_points and scene_id in save_points:
            def mid(current, _name=name, _scene=scene_id):
                payload = json.loads(json.dumps(current.to_dict(), ensure_ascii=False))
                restored = Simulator.from_dict(content, payload)
                if restored.to_dict() != current.to_dict():
                    restored.errors.append("%s: save/load round-trip mismatch at %s" % (_name, _scene))
                return restored
        sim = sim.process_scene(scene_id, forced.get(scene_id), scene_id in shallow, mid)
    return sim


def route_issues(name, sim, expected_delta=None):
    issues = []
    missing_puzzles = sorted(EXPECTED_PUZZLE_SET - sim.solved)
    if missing_puzzles:
        issues.append("%s: unsolved puzzles: %s" % (name, ",".join(missing_puzzles)))
    missing_evidence = sorted(REQUIRED_EVIDENCE_SET - sim.owned)
    if missing_evidence:
        issues.append("%s: missing required evidence: %s" % (name, ",".join(missing_evidence)))
    if "s710" not in sim.completed:
        issues.append("%s: s710 not completed" % name)
    if expected_delta is not None:
        for evidence_id in expected_delta:
            if evidence_id not in sim.owned:
                issues.append("%s: optional evidence %s not owned" % (name, evidence_id))
    issues.extend(sim.errors)
    return issues


def main():
    content = Content()
    specs = build_routes()
    results = []
    errors = []
    for spec in specs:
        sim = run_route(content, spec)
        expected_delta = None
        if spec[4].startswith("optional:"):
            expected_delta = set(filter(None, spec[4].split(":")[1].split(",")))
        issues = route_issues(spec[0], sim, expected_delta)
        results.append({"name": spec[0], "sim": sim, "kind": spec[4], "issues": issues, "expected_delta": expected_delta})

    canonical = next(result for result in results if result["name"] == "canonical_13_1")
    canonical_full = set(canonical["sim"].owned)
    canonical_required = canonical_full & REQUIRED_EVIDENCE_SET
    canonical_solved = set(canonical["sim"].solved)

    for result in results:
        name = result["name"]
        sim = result["sim"]
        required = sim.owned & REQUIRED_EVIDENCE_SET
        if required != canonical_required:
            errors.append(
                "%s: required evidence differs: +%s -%s"
                % (
                    name,
                    ",".join(sorted(required - canonical_required)) or "none",
                    ",".join(sorted(canonical_required - required)) or "none",
                )
            )
        if set(sim.solved) != canonical_solved:
            errors.append(
                "%s: solved puzzles differ: +%s -%s"
                % (
                    name,
                    ",".join(sorted(set(sim.solved) - canonical_solved)) or "none",
                    ",".join(sorted(canonical_solved - set(sim.solved))) or "none",
                )
            )
        if result["kind"] == "base":
            if sim.owned != canonical_full:
                errors.append(
                    "%s: full evidence set differs: +%s -%s"
                    % (
                        name,
                        ",".join(sorted(sim.owned - canonical_full)) or "none",
                        ",".join(sorted(canonical_full - sim.owned)) or "none",
                    )
                )
        elif result["kind"].startswith("optional:"):
            delta = sim.owned - canonical_full
            missing = canonical_full - sim.owned
            expected = result["expected_delta"]
            if delta != expected or missing:
                errors.append(
                    "%s: optional delta %s != expected %s (missing %s)"
                    % (
                        name,
                        sorted(delta),
                        sorted(expected),
                        sorted(missing),
                    )
                )
        errors.extend(result["issues"])

    save_load = next(result for result in results if result["kind"] == "save_load")
    if save_load["sim"].to_dict() != canonical["sim"].to_dict():
        errors.append("canonical_save_load_3pt: final state differs from no-save canonical route")

    routes_report = [
        {
            "name": result["name"],
            "ok": not result["issues"] and not any(error.startswith(result["name"] + ":") for error in errors),
            "finalEvidenceCount": len(result["sim"].owned),
            "finalPuzzleCount": len(result["sim"].solved),
        }
        for result in results
    ]
    report = {
        "ok": not errors and all(route["ok"] for route in routes_report),
        "routeCount": len(routes_report),
        "routes": routes_report,
        "errors": errors,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with REPORT_PATH.open("w", encoding="utf-8") as handle:
        json.dump(report, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    print(
        "check_story_routes.py: ok=%s routes=%d errors=%d -> reports/route_validation.json"
        % (report["ok"], report["routeCount"], len(errors))
    )
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
