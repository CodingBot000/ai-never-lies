using System.Collections.Generic;
using ANL.Core.Contracts;
using TMPro;
using UnityEngine;

namespace ANL.UI.Screens
{
    internal sealed class ReportWidget : PuzzleWidget
    {
        private static readonly string[] Sections = { "direct", "contributions", "organizational", "postIncident", "limit" };
        private static readonly string[] SectionKeys = { "ui.report_direct", "ui.report_contributions", "ui.report_organizational", "ui.report_post", "ui.report_limit" };
        private static readonly string[] SectionFallbacks = { "직접 원인", "기여 요인", "조직·관리", "사고 이후 행위", "판단 한계" };
        private static readonly string[] Items =
        {
            "unsafe_brake_limiting", "radar_fault", "override_after_warning", "health_check_removed",
            "release_conditions_open", "conflicting_kpi", "known_risk_continued", "misleading_public_summary",
            "no_intent_or_criminal_verdict",
            "no_one_responsible", "all_ai_innocent", "intentional_murder_proven", "publicity_caused_collision"
        };

        private string _selectedItem = "";

        public ReportWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "ReportWidget");
            BuildHeader(Root.transform, "항목을 구역에 배치하고 근거를 연결하십시오.");

            var palette = MakeBox(Root.transform, "Palette", 40f, 170f, 400f, 760f, PuzzleUi.PanelColor);
            MakeText(palette.transform, Label("ui.report_palette", "항목 팔레트"), 16f, 8f, 368f, 36f, 24f, PuzzleUi.TextColor);
            for (int i = 0; i < Items.Length; i++)
            {
                var itemId = Items[i];
                bool selected = _selectedItem == itemId;
                var row = MakeBox(palette.transform, "Item_" + itemId, 10f, 50f + i * 54f, 380f, 48f,
                    selected ? PuzzleUi.SelectedColor : PuzzleUi.RowColor);
                UiKit.HitArea(row, () => SelectItem(itemId));
                MakeText(row.transform, PuzzleUi.ItemLabel(itemId), 8f, 4f, 364f, 40f, 18f, PuzzleUi.TextColor);
            }

            for (int i = 0; i < Sections.Length; i++)
            {
                float x = 460f + i * 286f;
                var section = MakeBox(Root.transform, "Section_" + Sections[i], x, 170f, 278f, 760f, new Color(0.05f, 0.06f, 0.1f, 0.94f));
                var key = Sections[i];
                var header = MakeBox(section.transform, "Header", 0f, 0f, 278f, 54f, new Color(0.12f, 0.15f, 0.22f, 1f));
                UiKit.HitArea(header, () => PlaceInSection(key));
                MakeText(header.transform, Label(SectionKeys[i], SectionFallbacks[i]), 10f, 8f, 258f, 38f, 22f, PuzzleUi.TextColor);
                BuildSectionItems(section.transform, key);
            }

            MakeButton(Root.transform, Label("ui.save", "초안 저장"), 1420f, 968f, 240f, 64f, SaveDraft, null, 24f);
            MakeButton(Root.transform, Label("ui.report_submit", "보고서 채택"), 1680f, 968f, 200f, 64f,
                () => Send(CommandKinds.SubmitPuzzle), new Color(0.16f, 0.3f, 0.22f, 0.96f), 24f);
        }

        private void BuildSectionItems(Transform section, string sectionKey)
        {
            var placements = Draft.reportPlacements ?? new ReportPlacementDto[0];
            int index = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                var placement = placements[i];
                if (placement == null || placement.section != sectionKey) continue;
                var itemId = placement.item;
                var row = MakeBox(section, "Assigned_" + itemId, 8f, 62f + index * 88f, 262f, 82f, PuzzleUi.RowColor);
                UiKit.HitArea(row, () => Send(CommandKinds.PlaceCard, null, null, sectionKey, itemId));
                MakeText(row.transform, PuzzleUi.ItemLabel(itemId), 8f, 4f, 246f, 46f, 17f, PuzzleUi.TextColor);
                var id = itemId;
                MakeButton(row.transform, Label("ui.report_attach", "근거") + " " + ProofSelection(id).Length, 8f, 48f, 140f, 30f,
                    () => OpenProofPicker(id), null, 16f);
                index++;
            }
            if (index == 0)
                MakeText(section, "항목 없음", 10f, 68f, 258f, 30f, 18f, PuzzleUi.MutedColor);
        }

        private void SelectItem(string itemId)
        {
            _selectedItem = _selectedItem == itemId ? "" : itemId;
            Rebuild();
        }

        private void PlaceInSection(string sectionKey)
        {
            if (string.IsNullOrEmpty(_selectedItem))
            {
                ShowResult(CommandResult.Reject("no_item", "항목을 먼저 선택하십시오."));
                return;
            }
            Send(CommandKinds.PlaceCard, null, null, sectionKey, _selectedItem);
        }

        private void OpenProofPicker(string itemId)
        {
            var current = new List<string>(ProofSelection(itemId));
            App.Evidence.ShowPicker("근거 연결", 3, current,
                ids => Send(CommandKinds.SelectEvidence, itemId, ids.ToArray()));
        }

        private string[] ProofSelection(string itemId)
        {
            var proofs = Draft.reportEvidence;
            if (proofs == null) return new string[0];
            for (int i = 0; i < proofs.Length; i++)
            {
                var proof = proofs[i];
                if (proof != null && proof.item == itemId) return proof.evidenceIds ?? new string[0];
            }
            return new string[0];
        }

        private void SaveDraft()
        {
            App.Toast(Label("ui.report_draft_saved", "초안이 저장되었습니다"));
            App.Session.AutoSave();
        }
    }
}
