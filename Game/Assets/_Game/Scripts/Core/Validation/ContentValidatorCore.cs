using System.Collections.Generic;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.Conditions;
using ANL.Core.Puzzles;

namespace ANL.Core.Validation
{
    public static class FrozenIds
    {
        public static readonly string[] Scenes =
        {
            "s000","s010","s020","s100","s110","s120","s130","s140","s150","s160","s170",
            "s200","s210","s220","s230","s240","s300","s310","s320","s330","s340",
            "s400","s410","s411","s420","s430","s440","s450","s460",
            "s500","s510","s520","s530","s540","s550","s600","s610","s620","s630","s640","s650",
            "s700","s710"
        };

        public static readonly string[] Evidence =
        {
            "ev01","ev02","ev03","ev04","ev05","ev06","ev07","ev08","ev09","ev10","ev11","ev12",
            "ev13","ev14","ev15","ev16","ev17","ev18","ev19","ev20","ev21","ev22","ev23","ev24",
            "ev25","ev26","ev27","ev28","ev29","ev30","ev31","ev32","ev33","ev34","ev35","ev36"
        };

        public static readonly string[] Puzzles =
        {
            "p01","p02","p03","p04","p05","p06","p07","p08","p09","p10",
            "p11","p12","p13","p14","p15","p16","p17","p18","p19","p20"
        };
    }

    public static class ContentValidatorCore
    {
        public static List<string> Validate(IContentIndex index, string[] assetIds = null)
        {
            var errors = new List<string>();
            if (index == null)
            {
                errors.Add("content index is null");
                return errors;
            }
            var evidenceSet = new HashSet<string>(FrozenIds.Evidence);
            if (assetIds != null)
            {
                var assetSet = new HashSet<string>(assetIds);
                foreach (var pair in index.Scenes)
                {
                    var bg = pair.Value.backgroundAssetId;
                    if (!string.IsNullOrEmpty(bg) && !assetSet.Contains(bg))
                        errors.Add($"scene {pair.Key}: unknown background asset {bg}");
                }
                foreach (var pair in index.Dialogues)
                {
                    var portrait = pair.Value.portraitAssetId;
                    if (!string.IsNullOrEmpty(portrait) && !assetSet.Contains(portrait))
                        errors.Add($"dialogue {pair.Key}: unknown portrait asset {portrait}");
                }
                foreach (var pair in index.Evidence)
                {
                    var img = pair.Value.imageAssetId;
                    if (!string.IsNullOrEmpty(img) && !assetSet.Contains(img))
                        errors.Add($"evidence {pair.Key}: unknown image asset {img}");
                }
            }

            if (index.Episode == null) errors.Add("episode definition missing");
            else
            {
                if (index.Episode.id != "ep01") errors.Add($"episode id must be ep01, got {index.Episode.id}");
                if (string.IsNullOrEmpty(index.Episode.startSceneId) || !index.HasScene(index.Episode.startSceneId))
                    errors.Add("episode startSceneId is missing or unknown");
                if (index.Episode.requiredPuzzleIds == null || index.Episode.requiredPuzzleIds.Length != 20)
                    errors.Add("episode requiredPuzzleIds must contain 20 puzzles");
                else
                    foreach (var id in FrozenIds.Puzzles)
                        if (System.Array.IndexOf(index.Episode.requiredPuzzleIds, id) < 0)
                            errors.Add($"episode requiredPuzzleIds missing {id}");
            }

            CheckFrozenSet("scene", FrozenIds.Scenes, new List<string>(index.Scenes.Keys), errors);
            CheckFrozenSet("evidence", FrozenIds.Evidence, new List<string>(index.Evidence.Keys), errors);
            CheckFrozenSet("puzzle", FrozenIds.Puzzles, new List<string>(index.Puzzles.Keys), errors);

            // scene references
            foreach (var pair in index.Scenes)
            {
                var scene = pair.Value;
                CheckDialogueRefs(index, scene.id, scene.preDialogueIds, errors);
                CheckDialogueRefs(index, scene.id, scene.postDialogueIds, errors);
                if (scene.grants != null)
                {
                    foreach (var grant in scene.grants)
                    {
                        if (!evidenceSet.Contains(grant.evidenceId))
                            errors.Add($"scene {scene.id}: grant references unknown evidence {grant.evidenceId}");
                        if (grant.trigger == "dialogue" && !index.Dialogues.ContainsKey(grant.atDialogueId ?? ""))
                            errors.Add($"scene {scene.id}: dialogue grant references unknown line {grant.atDialogueId}");
                    }
                }
                if (scene.requiredReadEvidenceIds != null)
                    foreach (var id in scene.requiredReadEvidenceIds)
                        if (!evidenceSet.Contains(id))
                            errors.Add($"scene {scene.id}: requiredRead references unknown evidence {id}");
                if (scene.interactionIds != null)
                    foreach (var id in scene.interactionIds)
                        if (!index.Puzzles.ContainsKey(id))
                            errors.Add($"scene {scene.id}: unknown puzzle {id}");
                if (scene.transitions != null)
                {
                    foreach (var transition in scene.transitions)
                    {
                        if (!index.HasScene(transition.target))
                            errors.Add($"scene {scene.id}: transition targets unknown scene {transition.target}");
                        if (transition.conditions != null)
                            foreach (var condition in transition.conditions)
                                if (!ConditionKind.IsKnown(condition.kind))
                                    errors.Add($"scene {scene.id}: unknown condition kind {condition.kind}");
                    }
                }
                if (scene.branchGroupIds != null)
                {
                    foreach (var groupId in scene.branchGroupIds)
                    {
                        var group = index.BranchGroup(groupId);
                        if (group.Count == 0)
                            errors.Add($"scene {scene.id}: branch group {groupId} is empty");
                        foreach (var branch in group)
                            if (branch.sceneId != scene.id)
                                errors.Add($"branch {branch.id}: sceneId {branch.sceneId} does not match group scene {scene.id}");
                    }
                }
                if (scene.advancingGroupIds != null)
                {
                    foreach (var groupId in scene.advancingGroupIds)
                    {
                        var group = index.BranchGroup(groupId);
                        bool hasAdvancing = false;
                        foreach (var branch in group)
                            if (branch.advances) hasAdvancing = true;
                        if (!hasAdvancing)
                            errors.Add($"scene {scene.id}: advancing group {groupId} has no advancing branch");
                    }
                }
                if (scene.childSceneIds != null)
                    foreach (var child in scene.childSceneIds)
                        if (!index.HasScene(child))
                            errors.Add($"scene {scene.id}: unknown child scene {child}");
            }

            // branch references
            foreach (var pair in index.Branches)
            {
                var branch = pair.Value;
                if (!index.HasScene(branch.sceneId))
                    errors.Add($"branch {branch.id}: unknown scene {branch.sceneId}");
                if (branch.effects != null)
                {
                    foreach (var effect in branch.effects)
                    {
                        if (effect.kind == "GrantEvidence" && !evidenceSet.Contains(effect.id))
                            errors.Add($"branch {branch.id}: grant references unknown evidence {effect.id}");
                        if (effect.kind != "GrantEvidence" && effect.kind != "SetBranch" && effect.kind != "Feedback")
                            errors.Add($"branch {branch.id}: unknown effect kind {effect.kind}");
                    }
                }
            }

            // puzzle references and structure
            foreach (var pair in index.Puzzles)
            {
                var puzzle = pair.Value;
                if (!index.HasScene(puzzle.sceneId))
                    errors.Add($"puzzle {puzzle.id}: unknown scene {puzzle.sceneId}");
                if (!PuzzleEvaluator.IsKnownType(puzzle.type))
                    errors.Add($"puzzle {puzzle.id}: unknown type {puzzle.type}");
                if (puzzle.requiredEvidenceIds != null)
                    foreach (var id in puzzle.requiredEvidenceIds)
                        if (!evidenceSet.Contains(id))
                            errors.Add($"puzzle {puzzle.id}: unknown required evidence {id}");
                if (!string.IsNullOrEmpty(puzzle.targetStatementId) && !index.Testimonies.ContainsKey(puzzle.targetStatementId))
                    errors.Add($"puzzle {puzzle.id}: unknown target statement {puzzle.targetStatementId}");
                if (puzzle.type == PuzzleTypes.StatementRefutation && string.IsNullOrEmpty(puzzle.targetStatementId))
                    errors.Add($"puzzle {puzzle.id}: statement_refutation requires a target statement");
                if (puzzle.sufficientEvidenceSets != null)
                    foreach (var set in puzzle.sufficientEvidenceSets)
                        if (set?.requiredIds != null)
                            foreach (var id in set.requiredIds)
                                if (!evidenceSet.Contains(id))
                                    errors.Add($"puzzle {puzzle.id}: sufficient set references unknown evidence {id}");
                if (puzzle.requiredEdges != null)
                    foreach (var edge in puzzle.requiredEdges)
                    {
                        if (!HasNode(index, edge.from) || !HasNode(index, edge.to))
                            errors.Add($"puzzle {puzzle.id}: edge references unknown node {edge.from}->{edge.to}");
                        if (edge.kind != "causal" && edge.kind != "signal")
                            errors.Add($"puzzle {puzzle.id}: edge {edge.from}->{edge.to} has unknown kind {edge.kind}");
                    }
                if (string.IsNullOrEmpty(puzzle.successText))
                    errors.Add($"puzzle {puzzle.id}: success text is empty");
                var scene = index.Scene(puzzle.sceneId);
                if (scene != null && (scene.interactionIds == null || System.Array.IndexOf(scene.interactionIds, puzzle.id) < 0))
                    errors.Add($"puzzle {puzzle.id}: scene {puzzle.sceneId} does not list it as interaction");
            }

            // testimony references
            foreach (var pair in index.Testimonies)
            {
                if (!index.HasScene(pair.Value.sceneId))
                    errors.Add($"testimony {pair.Key}: unknown scene {pair.Value.sceneId}");
                if (string.IsNullOrEmpty(pair.Value.text)) errors.Add($"testimony {pair.Key}: empty text");
            }

            // hints coverage
            int sceneHints = 0, puzzleHintLevels = 0;
            var puzzleHintIds = new Dictionary<string, HashSet<int>>();
            foreach (var pair in index.Hints)
            {
                var hint = pair.Value;
                if (hint.scopeType == "scene")
                {
                    sceneHints++;
                    if (!index.HasScene(hint.scopeId)) errors.Add($"hint {hint.id}: unknown scene {hint.scopeId}");
                }
                else if (hint.scopeType == "puzzle")
                {
                    puzzleHintLevels++;
                    if (!index.Puzzles.ContainsKey(hint.scopeId)) errors.Add($"hint {hint.id}: unknown puzzle {hint.scopeId}");
                    if (!puzzleHintIds.TryGetValue(hint.scopeId, out var levels))
                    {
                        levels = new HashSet<int>();
                        puzzleHintIds[hint.scopeId] = levels;
                    }
                    levels.Add(hint.level);
                }
                if (string.IsNullOrEmpty(hint.text)) errors.Add($"hint {hint.id}: empty text");
            }
            if (sceneHints < 43) errors.Add($"scene hints must cover 43 scenes, got {sceneHints}");
            foreach (var id in FrozenIds.Puzzles)
                if (!puzzleHintIds.TryGetValue(id, out var levels) || levels.Count < 3)
                    errors.Add($"puzzle {id}: missing H1/H2/H3 hints");
            if (puzzleHintLevels < 60) errors.Add($"puzzle hints must be at least 60, got {puzzleHintLevels}");

            // evidence acquisition coverage
            var granted = new HashSet<string>();
            foreach (var pair in index.Scenes)
            {
                var scene = pair.Value;
                if (scene.grants != null)
                    foreach (var grant in scene.grants)
                        granted.Add(grant.evidenceId);
                if (scene.interactionIds != null)
                    foreach (var puzzleId in scene.interactionIds)
                    {
                        var puzzle = index.Puzzle(puzzleId);
                        if (puzzle?.regions != null)
                            foreach (var region in puzzle.regions)
                                if (!string.IsNullOrEmpty(region.evidenceId)) granted.Add(region.evidenceId);
                    }
            }
            foreach (var pair in index.Branches)
                if (pair.Value.effects != null)
                    foreach (var effect in pair.Value.effects)
                        if (effect.kind == "GrantEvidence") granted.Add(effect.id);
            foreach (var id in FrozenIds.Evidence)
                if (!granted.Contains(id))
                    errors.Add($"evidence {id}: never granted by any scene, branch, or region");

            // specific contract checks
            var s150 = index.Scene("s150");
            if (s150 != null)
            {
                bool ev08 = false, ev09 = false;
                if (s150.grants != null)
                    foreach (var grant in s150.grants)
                    {
                        if (grant.evidenceId == "ev08" && grant.trigger == "post_start") ev08 = true;
                        if (grant.evidenceId == "ev09" && grant.trigger == "post_start") ev09 = true;
                    }
                if (!ev08 || !ev09) errors.Add("s150 must grant ev08/ev09 on post_start");
            }
            var s640 = index.Scene("s640");
            if (s640 != null && s640.entryConditions != null)
                foreach (var condition in s640.entryConditions)
                    if (condition.kind == "PuzzleSolved" && condition.id == "p20")
                        errors.Add("s640 entry must not require p20 (circular lock)");
            bool acceptBranch = false;
            foreach (var pair in index.Branches)
            {
                if (pair.Key == "b_s530_accept")
                {
                    if (pair.Value.effects != null)
                        foreach (var effect in pair.Value.effects)
                            if (effect.kind == "GrantEvidence" && effect.id == "ev35") acceptBranch = true;
                }
            }
            if (!acceptBranch) errors.Add("b_s530_accept must grant ev35");

            // dialogue text completeness
            foreach (var pair in index.Dialogues)
                if (string.IsNullOrEmpty(pair.Value.text))
                    errors.Add($"dialogue {pair.Key}: empty text");

            // evidence body completeness
            foreach (var pair in index.Evidence)
            {
                if (string.IsNullOrEmpty(pair.Value.body)) errors.Add($"evidence {pair.Key}: empty body");
                if (string.IsNullOrEmpty(pair.Value.proves)) errors.Add($"evidence {pair.Key}: empty proves");
                if (string.IsNullOrEmpty(pair.Value.limits)) errors.Add($"evidence {pair.Key}: empty limits");
            }

            return errors;
        }

        private static bool HasNode(IContentIndex index, string nodeId)
        {
            var nodes = index.GraphNodes();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].id == nodeId) return true;
            return false;
        }

        private static void CheckDialogueRefs(IContentIndex index, string sceneId, string[] ids, List<string> errors)
        {
            if (ids == null) return;
            foreach (var id in ids)
                if (!index.Dialogues.ContainsKey(id))
                    errors.Add($"scene {sceneId}: unknown dialogue {id}");
        }

        private static void CheckFrozenSet(string label, string[] expected, List<string> actual, List<string> errors)
        {
            var expectedSet = new HashSet<string>(expected);
            var actualSet = new HashSet<string>(actual);
            foreach (var id in expected)
                if (!actualSet.Contains(id)) errors.Add($"{label} missing: {id}");
            foreach (var id in actualSet)
                if (!expectedSet.Contains(id)) errors.Add($"{label} unexpected: {id}");
            if (actual.Count != actualSet.Count) errors.Add($"{label} contains duplicate ids");
        }
    }
}
