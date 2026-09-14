#!/usr/bin/env python3
"""Generate Episode 01 scenario fixtures for the dev panel."""

from __future__ import annotations

import json
import sys
from pathlib import Path

WORKSPACE = Path(__file__).resolve().parents[1]
CONTENT_DIR = WORKSPACE / "content/ep01"
FIXTURE_PATH = WORKSPACE / "content/dev/fixtures.json"

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

    def process_scene(self, scene_id, forced=None, shallow=False):
        scene = self.content.scenes[scene_id]
        if scene_id not in self.visited:
            self.visited.append(scene_id)
        if shallow:
            return
        self.apply_grants(scene, "scene_start")
        for line_id in scene.get("preDialogueIds") or []:
            self.apply_line_grants(scene, line_id)
        self.pre_done.add(scene_id)
        self.apply_grants(scene, "interaction_start")
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


def with_optionals(visits):
    prefix = CANONICAL_ROUTE[:41]
    suffix = CANONICAL_ROUTE[46:]
    route = list(prefix) + ["s500"]
    for child in visits:
        route += [child, "s500"]
    route += list(suffix)
    return route


FIXTURE_ROUTE_WITH_OPTIONALS = with_optionals(["s510", "s520", "s530", "s540"])
FIXTURE_FORCED = {"s530": {"cg_s530": "b_s530_decline"}}
FIXTURE_SHALLOW = {"s520"}


def capture(content, sequence, target, forced=None, shallow=None):
    forced = forced or {}
    shallow = shallow or set()
    sim = Simulator(content)
    for index, scene_id in enumerate(sequence):
        if scene_id == target:
            return sim
        if index > 0:
            ok, reason = sim.can_move(sequence[index - 1], scene_id)
            if not ok:
                sim.errors.append("move %s->%s rejected: %s" % (sequence[index - 1], scene_id, reason))
        sim.process_scene(scene_id, forced.get(scene_id), scene_id in shallow)
    raise ValueError("target scene %s not found in route" % target)


def make_fixture(fixture_id, description, sim, current_scene, read_override=None):
    visited = list(sim.visited)
    if current_scene not in visited:
        visited.append(current_scene)
    owned = sorted(sim.owned)
    read = sorted(read_override) if read_override is not None else list(owned)
    return {
        "id": fixture_id,
        "description": description,
        "currentSceneId": current_scene,
        "phase": "pre_dialogue",
        "ownedEvidenceIds": owned,
        "readEvidenceIds": read,
        "visitedSceneIds": visited,
        "completedSceneIds": sorted(sim.completed),
        "solvedPuzzleIds": sorted(sim.solved),
        "visitedTopicIds": list(sim.topics),
        "branchValues": [{"key": key, "value": sim.branch_values[key]} for key in sim.branch_values],
        "hintUsage": [],
        "appliedEffectIds": list(sim.applied),
        "preDialogueDoneSceneIds": sorted(sim.pre_done),
        "postDialogueDoneSceneIds": sorted(sim.post_done),
    }


def expected_evidence(first, last=None):
    if last is None:
        return {"ev%02d" % n for n in range(1, first + 1)}
    return {"ev%02d" % n for n in range(first, last + 1)}


def expected_puzzles(last):
    return {"p%02d" % n for n in range(1, last + 1)}


def main():
    content = Content()
    specs = [
        (
            "f_s150_pre",
            "s150 hearing start: pre_dialogue, p01-p03 solved, ev01-ev07 owned; ev06/ev07 unread",
            CANONICAL_ROUTE,
            "s150",
            expected_evidence(7),
            expected_puzzles(3),
            None,
            {"ev06", "ev07"},
        ),
        (
            "f_s310_pre",
            "s310 hearing start: pre_dialogue, p01-p08 solved, evidence through s230 owned",
            CANONICAL_ROUTE,
            "s310",
            expected_evidence(17),
            expected_puzzles(8),
            None,
            None,
        ),
        (
            "f_s440_pre",
            "s440 deployment start: pre_dialogue, p01-p14 solved, evidence through s430 owned",
            CANONICAL_ROUTE,
            "s440",
            expected_evidence(26) | {"ev31"},
            expected_puzzles(14),
            None,
            None,
        ),
        (
            "f_s610_pre",
            "s610 causal board start: pre_dialogue, p01-p16 solved, evidence through s550 owned (ev34/ev35 excluded), s520/s530 not visited",
            CANONICAL_ROUTE,
            "s610",
            expected_evidence(33) | {"ev36"},
            expected_puzzles(16),
            {"s520", "s530"},
            None,
        ),
        (
            "f_s640_pre",
            "s640 report start: pre_dialogue, p01-p19 solved, ev01-ev33+ev36 owned, s520/s530 visited with s530 decline (ev35 not owned)",
            FIXTURE_ROUTE_WITH_OPTIONALS,
            "s640",
            expected_evidence(33) | {"ev36"},
            expected_puzzles(19),
            {"s520", "s530"},
            None,
        ),
        (
            "f_s700_pre",
            "s700 dialogue choice start: pre_dialogue, p01-p20 solved, all evidence except ev34/ev35 owned, s710 not completed",
            FIXTURE_ROUTE_WITH_OPTIONALS,
            "s700",
            expected_evidence(33) | {"ev36"},
            expected_puzzles(20),
            {"s520", "s530"},
            None,
        ),
    ]

    fixtures = []
    for fixture_id, description, sequence, target, evidence_set, puzzle_set, optional_visited, unread in specs:
        shallow = set()
        forced = {}
        if fixture_id in ("f_s640_pre", "f_s700_pre"):
            shallow = set(FIXTURE_SHALLOW)
            forced = FIXTURE_FORCED
        sim = capture(content, sequence, target, forced, shallow)
        if sim.errors:
            raise SystemExit("fixture %s: simulator errors: %s" % (fixture_id, sim.errors))
        read_override = (sim.owned - unread) if unread else None
        fixture = make_fixture(fixture_id, description, sim, target, read_override)
        if set(fixture["ownedEvidenceIds"]) != evidence_set:
            raise SystemExit(
                "fixture %s: owned evidence %s != expected %s"
                % (fixture_id, sorted(set(fixture["ownedEvidenceIds"]) - evidence_set), sorted(evidence_set - set(fixture["ownedEvidenceIds"])))
            )
        if set(fixture["solvedPuzzleIds"]) != puzzle_set:
            raise SystemExit(
                "fixture %s: solved puzzles %s != expected %s"
                % (fixture_id, sorted(set(fixture["solvedPuzzleIds"]) - puzzle_set), sorted(puzzle_set - set(fixture["solvedPuzzleIds"])))
            )
        if fixture_id in ("f_s640_pre", "f_s700_pre"):
            for scene_id in ("s520", "s530"):
                if scene_id not in fixture["visitedSceneIds"]:
                    raise SystemExit("fixture %s: expected %s in visitedSceneIds" % (fixture_id, scene_id))
        if fixture_id == "f_s610_pre":
            for scene_id in ("s520", "s530"):
                if scene_id in fixture["visitedSceneIds"]:
                    raise SystemExit("fixture %s: %s must not be visited" % (fixture_id, scene_id))
        if fixture_id == "f_s640_pre":
            branch_values = {pair["key"]: pair["value"] for pair in fixture["branchValues"]}
            if branch_values.get("opt_family_visit") != "true" or branch_values.get("opt_family_material") != "false":
                raise SystemExit("fixture f_s640_pre: expected decline branch values, got %s" % branch_values)
            if "ev35" in fixture["ownedEvidenceIds"]:
                raise SystemExit("fixture f_s640_pre: ev35 must not be owned after decline")
        if fixture_id == "f_s700_pre" and "s710" in fixture["completedSceneIds"]:
            raise SystemExit("fixture f_s700_pre: s710 must not be completed")
        fixtures.append(fixture)

    document = {"schemaVersion": 1, "items": fixtures}
    FIXTURE_PATH.parent.mkdir(parents=True, exist_ok=True)
    with FIXTURE_PATH.open("w", encoding="utf-8") as handle:
        json.dump(document, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    with FIXTURE_PATH.open(encoding="utf-8") as handle:
        parsed = json.load(handle)
    if parsed.get("schemaVersion") != 1 or len(parsed.get("items") or []) != len(fixtures):
        raise SystemExit("generated fixtures.json failed parse validation")

    print("make_fixtures.py: wrote %s" % FIXTURE_PATH.relative_to(WORKSPACE))
    for fixture in fixtures:
        print(
            "  %s: evidence=%d puzzles=%d scene=%s"
            % (
                fixture["id"],
                len(fixture["ownedEvidenceIds"]),
                len(fixture["solvedPuzzleIds"]),
                fixture["currentSceneId"],
            )
        )
    return 0


if __name__ == "__main__":
    sys.exit(main())
