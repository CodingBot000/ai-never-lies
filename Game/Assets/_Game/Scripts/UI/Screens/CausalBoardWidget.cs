using System.Collections.Generic;
using ANL.Core.Contracts;
using TMPro;
using UnityEngine;

namespace ANL.UI.Screens
{
    internal sealed class CausalBoardWidget : PuzzleWidget
    {
        private static readonly string[] Zones = { "operational", "control", "outcome" };
        private static readonly string[] ZoneTitles = { "운영선", "제어선", "결과" };
        private static readonly float[] ZoneX = { 40f, 420f, 800f };

        private string _source = "";
        private string _target = "";
        private TextMeshProUGUI _localResult;

        public CausalBoardWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "CausalBoardWidget");
            BuildHeader(Root.transform, "노드를 차례로 선택한 뒤 인과선 또는 신호선을 연결하십시오.");

            var nodes = App.Content.GraphNodes();
            for (int i = 0; i < Zones.Length; i++)
                BuildZone(Root.transform, ZoneTitles[i], Zones[i], ZoneX[i], nodes);

            MakeButton(Root.transform, "인과선 연결", 40f, 748f, 240f, 64f,
                () => Connect("causal"), new Color(0.16f, 0.3f, 0.22f, 0.96f), 24f);
            MakeButton(Root.transform, "신호선 연결", 300f, 748f, 240f, 64f,
                () => Connect("signal"), null, 24f);

            BuildEdges();
            BuildProofs();

            if (ShouldShowAndTip())
                MakeText(Root.transform, Label("ui.causal_and_partial", "AND 결합 입력이 하나만 연결되었습니다."),
                    40f, 824f, 1100f, 44f, 22f, PuzzleUi.WarnColor);

            SubmitButton(Root.transform);
        }

        protected override void ShowResult(CommandResult result)
        {
            if (Root == null || result == null) return;
            if (_localResult == null)
            {
                _localResult = UiKit.Label(Root.transform, "", 22f, PuzzleUi.BadColor, TextAlignmentOptions.TopLeft, true);
                UiKit.SetRect(_localResult.gameObject, 40f, 878f, 1100f, 44f);
            }
            _localResult.text = result.message ?? "";
            _localResult.color = result.status == ResultStatus.Solved ? PuzzleUi.OkColor
                : result.status == ResultStatus.Partial ? PuzzleUi.WarnColor
                : PuzzleUi.BadColor;
        }

        private void BuildZone(Transform parent, string title, string zone, float x, List<GraphNodeDto> nodes)
        {
            var box = MakeBox(parent, "Zone_" + zone, x, 150f, 360f, 560f, new Color(0.05f, 0.06f, 0.1f, 0.9f));
            MakeText(box.transform, title, 16f, 10f, 328f, 34f, 24f, PuzzleUi.TextColor);
            int index = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null || !InZone(node.kind, zone)) continue;
                bool isSource = node.id == _source;
                bool isTarget = node.id == _target;
                var color = isSource ? PuzzleUi.AccentColor : (isTarget ? PuzzleUi.SecondaryColor : PuzzleUi.RowColor);
                var card = MakeBox(box.transform, "Node_" + node.id, 10f, 50f + index * 84f, 340f, 76f, color);
                var id = node.id;
                UiKit.HitArea(card, () => SelectNode(id));
                MakeText(card.transform, node.label, 12f, 8f, 316f, 60f, 20f, PuzzleUi.TextColor);
                index++;
            }
        }

        private static bool InZone(string kind, string zone)
        {
            if (zone == "operational") return kind == "operational";
            if (zone == "outcome") return kind == "outcome" || kind == "signal";
            return kind == "physical" || kind == "software" || kind == "and";
        }

        private void SelectNode(string nodeId)
        {
            if (_source == nodeId)
            {
                _source = "";
                _target = "";
            }
            else if (string.IsNullOrEmpty(_source))
            {
                _source = nodeId;
            }
            else if (string.IsNullOrEmpty(_target))
            {
                _target = nodeId;
            }
            else
            {
                _source = nodeId;
                _target = "";
            }
            Rebuild();
        }

        private void Connect(string kind)
        {
            if (string.IsNullOrEmpty(_source) || string.IsNullOrEmpty(_target))
            {
                SetLocalMessage("연결할 두 노드를 선택하십시오.", PuzzleUi.WarnColor);
                return;
            }
            Send(CommandKinds.ConnectNodes, kind, new[] { _source, _target });
        }

        private void SetLocalMessage(string message, Color color)
        {
            if (Root == null) return;
            if (_localResult == null)
            {
                _localResult = UiKit.Label(Root.transform, "", 22f, color, TextAlignmentOptions.TopLeft, true);
                UiKit.SetRect(_localResult.gameObject, 40f, 878f, 1100f, 44f);
            }
            _localResult.text = message;
            _localResult.color = color;
        }

        private void BuildEdges()
        {
            var panel = MakeBox(Root.transform, "Edges", 1180f, 150f, 700f, 560f, PuzzleUi.PanelColor);
            MakeText(panel.transform, Label("ui.causal_connect", "연결"), 20f, 12f, 660f, 36f, 24f, PuzzleUi.TextColor);
            var edges = Draft.edges ?? new EdgeDto[0];
            int count = 0;
            for (int i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                if (edge == null) continue;
                var row = MakeBox(panel.transform, "Edge_" + i, 12f, 56f + count * 58f, 676f, 52f, PuzzleUi.RowColor);
                MakeText(row.transform, NodeLabel(edge.from) + " → " + NodeLabel(edge.to) + " (" + KindLabel(edge.kind) + ")",
                    12f, 10f, 480f, 34f, 20f, PuzzleUi.TextColor);
                var from = edge.from;
                var to = edge.to;
                MakeButton(row.transform, Label("ui.causal_remove", "삭제"), 540f, 8f, 120f, 36f,
                    () => Send(CommandKinds.RemoveEdge, null, new[] { from, to }), new Color(0.4f, 0.16f, 0.18f, 0.96f), 20f);
                count++;
            }
            if (count == 0)
                MakeText(panel.transform, "연결이 없습니다.", 20f, 60f, 660f, 36f, 22f, PuzzleUi.MutedColor);
        }

        private void BuildProofs()
        {
            var proofs = MakeBox(Root.transform, "Proofs", 1180f, 726f, 700f, 242f, PuzzleUi.PanelColor);
            MakeText(proofs.transform, Label("ui.causal_proof", "간선 근거"), 16f, 8f, 668f, 30f, 22f, PuzzleUi.TextColor);
            var requirements = Puzzle.edgeProofRequirements ?? new EdgeProofDto[0];
            for (int i = 0; i < requirements.Length && i < 8; i++)
            {
                var requirement = requirements[i];
                if (requirement == null) continue;
                float y = 42f + i * 24f;
                MakeText(proofs.transform, requirement.from + "→" + requirement.to + " (" + ProofKindLabel(requirement.kind) + ")",
                    12f, y, 380f, 22f, 15f, PuzzleUi.MutedColor, TextAlignmentOptions.Left, false);
                var from = requirement.from;
                var to = requirement.to;
                var kind = requirement.kind;
                MakeButton(proofs.transform, Label("ui.causal_proof", "근거") + " " + ProofSelection(from, to, kind).Length,
                    400f, y - 1f, 120f, 24f,
                    () => OpenProofPicker(from, to, kind), null, 14f);
            }
        }

        private void OpenProofPicker(string from, string to, string kind)
        {
            var current = new List<string>(ProofSelection(from, to, kind));
            App.Evidence.ShowPicker("간선 근거", 3, current,
                ids => Send(CommandKinds.SelectEvidence, from + "|" + to + "|" + kind, ids.ToArray()));
        }

        private string[] ProofSelection(string from, string to, string kind)
        {
            var proofs = Draft.edgeProofs;
            if (proofs == null) return new string[0];
            for (int i = 0; i < proofs.Length; i++)
            {
                var proof = proofs[i];
                if (proof != null && proof.from == from && proof.to == to && proof.kind == kind)
                    return proof.evidenceIds ?? new string[0];
            }
            return new string[0];
        }

        private bool ShouldShowAndTip()
        {
            var nodeIds = Puzzle.andNodeIds;
            var edges = Puzzle.requiredEdges;
            if (nodeIds == null || edges == null) return false;
            for (int i = 0; i < nodeIds.Length; i++)
            {
                int required = 0;
                int present = 0;
                for (int j = 0; j < edges.Length; j++)
                {
                    if (edges[j] == null || edges[j].to != nodeIds[i] || edges[j].kind != "causal") continue;
                    required++;
                    if (HasEdge(edges[j].from, edges[j].to, edges[j].kind)) present++;
                }
                if (required > 1 && present < required) return true;
            }
            return false;
        }

        private bool HasEdge(string from, string to, string kind)
        {
            var edges = Draft.edges;
            if (edges == null) return false;
            for (int i = 0; i < edges.Length; i++)
                if (edges[i] != null && edges[i].from == from && edges[i].to == to && edges[i].kind == kind) return true;
            return false;
        }

        private string NodeLabel(string nodeId)
        {
            var nodes = App.Content.GraphNodes();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].id == nodeId) return nodes[i].label;
            return nodeId;
        }

        private static string KindLabel(string kind)
        {
            return kind == "signal" ? "신호" : "원인";
        }

        private static string ProofKindLabel(string kind)
        {
            return kind == "signal" ? "신호" : "인과";
        }
    }
}
