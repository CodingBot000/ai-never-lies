using System;
using System.Collections.Generic;
using ANL.Core.Contracts;
using ANL.Core.Puzzles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    internal static class PuzzleUi
    {
        public static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.11f, 0.96f);
        public static readonly Color RowColor = new Color(0.1f, 0.12f, 0.18f, 0.96f);
        public static readonly Color SelectedColor = new Color(0.2f, 0.26f, 0.38f, 1f);
        public static readonly Color AccentColor = new Color(0.28f, 0.45f, 0.7f, 1f);
        public static readonly Color SecondaryColor = new Color(0.5f, 0.35f, 0.6f, 1f);
        public static readonly Color TextColor = new Color(0.94f, 0.96f, 1f, 1f);
        public static readonly Color MutedColor = new Color(0.72f, 0.76f, 0.85f, 1f);
        public static readonly Color RemovedColor = new Color(0.3f, 0.16f, 0.18f, 0.96f);
        public static readonly Color OkColor = new Color(0.55f, 0.92f, 0.62f, 1f);
        public static readonly Color WarnColor = new Color(0.98f, 0.84f, 0.42f, 1f);
        public static readonly Color BadColor = new Color(0.98f, 0.55f, 0.5f, 1f);

        private static readonly Dictionary<string, string> TypeLabels = new Dictionary<string, string>
        {
            { PuzzleTypes.StatementRefutation, "증언 반박" },
            { PuzzleTypes.SourceMapping, "출처 대응" },
            { PuzzleTypes.TraceMapping, "추적 대응" },
            { PuzzleTypes.TimelineOrder, "시간 순서" },
            { PuzzleTypes.Chronology, "연표" },
            { PuzzleTypes.DeploymentChain, "배포 경로" },
            { PuzzleTypes.CrossCamera, "교차 카메라" },
            { PuzzleTypes.ArgumentChoice, "논증 선택" },
            { PuzzleTypes.DiffSelect, "변경 비교" },
            { PuzzleTypes.AssumptionLink, "전제 연결" },
            { PuzzleTypes.MatrixGap, "조합 공백" },
            { PuzzleTypes.Counterfactual, "반사실" },
            { PuzzleTypes.CausalGraph, "인과 그래프" },
            { PuzzleTypes.ResponsibilityMatrix, "책임 보고서" },
            { PuzzleTypes.HotspotSet, "현장 조사" }
        };

        private static readonly Dictionary<string, string> ChoiceLabels = new Dictionary<string, string>
        {
            { "unconditional_approval", "어떤 조건도 없이 승인" },
            { "conditional_approval_unverified", "조건부 승인했지만 이행 확인 미완료" },
            { "never_approved", "끝까지 승인하지 않음" },
            { "none_until_impact", "충돌 때까지 제동이 전혀 없었다" },
            { "network_gap", "1.36초간 통신이 끊겼다" },
            { "weak_then_strong", "약한 제동 뒤 강한 제동이 실행됐다" },
            { "permanent", "영구 정상 판정" },
            { "conditional_active", "제한 승인 유지" },
            { "withdrawn", "승인 철회·운행중지" },
            { "safe_by_count", "46대가 무사했으니 해제는 안전했다" },
            { "all_certainly_unsafe", "47대 모두 같은 사고가 날 상태였다" },
            { "exposure_not_equivalent", "동일 위험 조건에 노출되었다는 근거가 없다" },
            { "scalar_compare_cost_21ms", "부등식 계산 한 번에 21ms" },
            { "snapshot_sync_cost", "상태 동기화 대기·검증 경로에서 21ms" },
            { "faulty_vehicle_not_dispatched", "결함 차량은 배차되지 않는다" },
            { "perfect_ai", "인식 정확도가 100%다" },
            { "never_rains", "비가 오지 않는다" },
            { "human_driver_present", "항상 운전자가 있다" },
            { "all_tests_fake", "시험 전체가 허위" },
            { "validated_all", "사고 조합까지 검증됨" },
            { "unvalidated_combination", "사고 조건 조합은 미검증" },
            { "merely_proposed", "문서로만 제안" },
            { "deployed_with_open_conditions", "실제 배포됐고 조건 미완료" },
            { "unknown_build", "차량 버전을 확인 못 함" }
        };

        private static readonly Dictionary<string, string> ItemLabels = new Dictionary<string, string>
        {
            { "unsafe_brake_limiting", "결함 후방 입력이 긴급제동 제한에 사용됨" },
            { "radar_fault", "없는 객체를 보고한 센서 결함" },
            { "override_after_warning", "위험 보고 확인 후 차량 운행중지 해제" },
            { "health_check_removed", "제동 경계에서 건강정보 확인 경로 제거" },
            { "release_conditions_open", "조건부 승인 사항을 완료하지 않은 배포" },
            { "conflicting_kpi", "안전 운휴와 충돌한 가동률·비용 관리" },
            { "known_risk_continued", "결합 위험과 중지 권고 인지 후 운영 유지" },
            { "misleading_public_summary", "사후 재분석을 당시 판단처럼 제시한 공개" },
            { "no_intent_or_criminal_verdict", "고의·형사 죄명을 확정하지 않고 사실과 증거를 이관" },
            { "no_one_responsible", "시스템 문제이므로 아무의 책임도 없음" },
            { "all_ai_innocent", "모든 AI가 항상 올바르고 안전함" },
            { "intentional_murder_proven", "회사가 피해자를 의도적으로 살해함" },
            { "publicity_caused_collision", "사후 발표 편집이 사고의 물리 원인임" }
        };

        private static readonly Dictionary<string, string> EventLabels = new Dictionary<string, string>
        {
            { "detect", "보행자 판정" },
            { "request", "긴급제동 요청" },
            { "limit", "제동 강도 제한" },
            { "warning", "차내 경고 표시" },
            { "weak_apply", "약한 제동 시작" },
            { "bypass", "독립 충돌임박 경로가 제한 해제" },
            { "strong_apply", "강한 제동 시작" },
            { "impact", "충돌" }
        };

        private static readonly Dictionary<string, string> ModuleLabels = new Dictionary<string, string>
        {
            { "perception", "대상 인식" },
            { "planner", "제동 계획" },
            { "brake_supervisor", "제동 조정기" },
            { "vehicle_controller", "차량 제어기" }
        };

        private static readonly Dictionary<string, string> SlotLabels = new Dictionary<string, string>
        {
            { "live", "사고 당시 판단" },
            { "reanalysis", "사후 재분석" },
            { "provenance", "출처 확인" },
            { "inputRequest", "-8.1 요청" },
            { "modifiedBy", "변경 모듈" },
            { "allowedRequest", "-2.0 허용" },
            { "actualStrong", "-6.2 실제 강한 감속" }
        };

        private static readonly Dictionary<string, string> DimensionLabels = new Dictionary<string, string>
        {
            { "sensor", "센서" },
            { "overrideState", "수동 해제" },
            { "version", "버전" },
            { "condition", "조건" }
        };

        private static readonly Dictionary<string, string> OptionLabels = new Dictionary<string, string>
        {
            { "normal", "정상" },
            { "faulty_sensor", "결함 센서" },
            { "missing_state", "상태 누락" },
            { "no_override", "수동 해제 없음" },
            { "manual_override", "수동 해제" },
            { "supervisor_3_4", "제동 조정기 3.4" },
            { "supervisor_3_5", "제동 조정기 3.5" },
            { "dry_day", "건조/주간" },
            { "rain_night", "강우/야간" }
        };

        private static readonly Dictionary<string, string> CaseLabels = new Dictionary<string, string>
        {
            { "historical", "사고 당시 조건" },
            { "gate_restored", "건강검증 복원" },
            { "dispatch_blocked", "운행중지 유지" }
        };

        private static readonly Dictionary<string, string> MasterLabels = new Dictionary<string, string>
        {
            { "collision", "충돌" },
            { "avoidable_under_assumptions", "보고서 가정에서 회피 가능" },
            { "no_trip", "운행 없음" }
        };

        public static string TypeLabel(string type)
        {
            return !string.IsNullOrEmpty(type) && TypeLabels.TryGetValue(type, out var label) ? label : type;
        }

        public static string ChoiceLabel(string id)
        {
            return !string.IsNullOrEmpty(id) && ChoiceLabels.TryGetValue(id, out var label) ? label : id;
        }

        public static string ItemLabel(string id)
        {
            return !string.IsNullOrEmpty(id) && ItemLabels.TryGetValue(id, out var label) ? label : id;
        }

        public static string EventLabel(string id)
        {
            return !string.IsNullOrEmpty(id) && EventLabels.TryGetValue(id, out var label) ? label : "";
        }

        public static string ModuleLabel(string id)
        {
            return !string.IsNullOrEmpty(id) && ModuleLabels.TryGetValue(id, out var label) ? label : "";
        }

        public static string SlotLabel(string key)
        {
            return !string.IsNullOrEmpty(key) && SlotLabels.TryGetValue(key, out var label) ? label : key;
        }

        public static string DimensionLabel(string key)
        {
            return !string.IsNullOrEmpty(key) && DimensionLabels.TryGetValue(key, out var label) ? label : key;
        }

        public static string OptionLabel(string id)
        {
            return !string.IsNullOrEmpty(id) && OptionLabels.TryGetValue(id, out var label) ? label : id;
        }

        public static string CaseLabel(string id)
        {
            return !string.IsNullOrEmpty(id) && CaseLabels.TryGetValue(id, out var label) ? label : id;
        }

        public static string MasterLabel(string id)
        {
            return !string.IsNullOrEmpty(id) && MasterLabels.TryGetValue(id, out var label) ? label : id;
        }

        public static int StableHash(string value)
        {
            unchecked
            {
                int hash = 5381;
                if (value == null) return hash;
                for (int i = 0; i < value.Length; i++) hash = ((hash << 5) + hash) ^ value[i];
                return hash;
            }
        }

        public static GameObject Transparent(Transform parent, string name)
        {
            var go = UiKit.Panel(parent, name, new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(go);
            var image = go.GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
            return go;
        }
    }

    internal abstract class PuzzleWidget
    {
        protected AppRoot App;
        protected PuzzleDefinitionDto Puzzle;
        protected PuzzleDraftDto Draft;
        protected Transform Parent;
        protected TextMeshProUGUI ResultText;
        public GameObject Root;

        protected PuzzleWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
        {
            App = app;
            Puzzle = puzzle;
            Draft = draft;
            Parent = parent;
        }

        public abstract void Build(Transform parent);

        protected CommandResult Send(string type, string targetId = null, string[] ids = null, string slot = null, string value = null, double numeric = 0)
        {
            var result = App.Exec(type, targetId, ids, slot, value, numeric);
            ShowResult(result);
            if (result != null && result.Changed) App.Refresh();
            return result;
        }

        protected void Rebuild()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
            Build(Parent);
        }

        protected virtual void ShowResult(CommandResult result)
        {
            if (Root == null || result == null) return;
            if (ResultText == null)
            {
                ResultText = UiKit.Label(Root.transform, "", 22f, PuzzleUi.TextColor, TextAlignmentOptions.TopLeft, true);
                UiKit.SetRect(ResultText.gameObject, 48f, 916f, 1100f, 44f);
            }
            ResultText.text = result.message ?? "";
            ResultText.color = result.status == ResultStatus.Solved ? PuzzleUi.OkColor
                : result.status == ResultStatus.Partial ? PuzzleUi.WarnColor
                : PuzzleUi.BadColor;
        }

        protected TextMeshProUGUI MakeTitle(Transform parent, string text)
        {
            var label = UiKit.Label(parent, text, 34f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(label.gameObject, 48f, 24f, 1500f, 46f);
            return label;
        }

        protected string Label(string key, string fallback)
        {
            return UiKit.Ui(App.Content, key, fallback);
        }

        protected void BuildHeader(Transform parent, string instruction)
        {
            MakeTitle(parent, Puzzle.id + " · " + PuzzleUi.TypeLabel(Puzzle.type));
            var line = UiKit.Label(parent, instruction, 24f, PuzzleUi.MutedColor, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(line.gameObject, 48f, 78f, 1700f, 44f);
        }

        protected GameObject MakeBox(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = UiKit.Panel(parent, name, color);
            UiKit.SetRect(go, x, y, w, h);
            return go;
        }

        protected TextMeshProUGUI MakeText(Transform parent, string value, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.TopLeft, bool wrap = true)
        {
            var label = UiKit.Label(parent, value, size, color, align, wrap);
            UiKit.SetRect(label.gameObject, x, y, w, h);
            return label;
        }

        protected Button MakeButton(Transform parent, string label, float x, float y, float w, float h, Action onClick, Color? background = null, float size = 22f)
        {
            var button = UiKit.TextButton(parent, label, size, onClick, background, w, h);
            UiKit.SetRect(button.gameObject, x, y, w, h);
            return button;
        }

        protected Button SubmitButton(Transform parent)
        {
            return MakeButton(parent, Label("ui.submit", "제출"), 1660f, 968f, 220f, 64f,
                () => Send(CommandKinds.SubmitPuzzle), new Color(0.16f, 0.3f, 0.22f, 0.96f), 26f);
        }

        protected string PairValue(string key)
        {
            var pairs = Puzzle.valuePairs;
            if (pairs == null) return "";
            for (int i = 0; i < pairs.Length; i++)
                if (pairs[i] != null && pairs[i].key == key) return pairs[i].value;
            return "";
        }

        protected string PlacedValue(string slot)
        {
            var cards = Draft.placedCards;
            if (cards == null) return "";
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null && cards[i].slot == slot) return cards[i].value;
            return "";
        }

        protected bool HasPlacedValue(string value)
        {
            var cards = Draft.placedCards;
            if (cards == null) return false;
            for (int i = 0; i < cards.Length; i++)
                if (cards[i] != null && cards[i].value == value) return true;
            return false;
        }

        protected bool IsChoice(string id)
        {
            var ids = Draft.choiceIds;
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return true;
            return false;
        }

        protected bool HasRanCase(string id)
        {
            var ids = Draft.ranCaseIds;
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return true;
            return false;
        }

        protected string EvidenceTitle(string id)
        {
            var def = App.Content.EvidenceDef(id);
            return def != null ? def.title : id;
        }

        protected string CardLabel(string id)
        {
            var eventLabel = PuzzleUi.EventLabel(id);
            if (!string.IsNullOrEmpty(eventLabel)) return eventLabel;
            var def = App.Content.EvidenceDef(id);
            if (def != null) return def.title;
            return id;
        }

        protected string DisplayValue(string value)
        {
            var def = App.Content.EvidenceDef(value);
            if (def != null) return def.title;
            var module = PuzzleUi.ModuleLabel(value);
            if (!string.IsNullOrEmpty(module)) return module;
            var eventLabel = PuzzleUi.EventLabel(value);
            if (!string.IsNullOrEmpty(eventLabel)) return eventLabel;
            return value;
        }
    }

    internal sealed class StatementWidget : PuzzleWidget
    {
        public StatementWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "StatementWidget");
            BuildHeader(Root.transform, Label("ui.statement_select", "증언을 선택하십시오."));

            var list = MakeBox(Root.transform, "Testimonies", 48f, 150f, 640f, 740f, PuzzleUi.PanelColor);
            var testimonies = App.Content.SceneTestimonies(Puzzle.sceneId);
            for (int i = 0; i < testimonies.Count; i++)
            {
                var statement = testimonies[i];
                bool selected = Draft.selectedStatementId == statement.id;
                var row = MakeBox(list.transform, "Statement_" + statement.id, 12f, 12f + i * 150f, 616f, 140f,
                    selected ? PuzzleUi.SelectedColor : PuzzleUi.RowColor);
                var id = statement.id;
                UiKit.HitArea(row, () => Send(CommandKinds.SelectStatement, id));
                MakeText(row.transform, (string.IsNullOrEmpty(statement.speaker) ? "" : statement.speaker + "\n") + statement.text,
                    14f, 10f, 588f, 86f, 20f, PuzzleUi.TextColor);
                if (selected)
                    MakeButton(row.transform, Label("ui.statement_press", "추궁"), 436f, 96f, 168f, 38f,
                        () => Send(CommandKinds.PressStatement, id), null, 20f);
            }

            var middle = MakeBox(Root.transform, "Selected", 700f, 150f, 532f, 740f, PuzzleUi.PanelColor);
            var chosen = App.Content.Testimony(Draft.selectedStatementId);
            MakeText(middle.transform, chosen != null ? chosen.speaker + "\n\n" + chosen.text : Label("ui.statement_select", "증언을 선택하십시오."),
                24f, 24f, 484f, 640f, 26f, PuzzleUi.TextColor);

            var right = MakeBox(Root.transform, "Evidence", 1240f, 150f, 632f, 740f, PuzzleUi.PanelColor);
            MakeText(right.transform, Label("ui.evidence", "증거"), 24f, 16f, 584f, 40f, 26f, PuzzleUi.TextColor);
            MakeButton(right.transform, "증거 선택", 24f, 64f, 260f, 56f, OpenEvidencePicker, null, 22f);
            var selectedIds = Draft.selectedEvidenceIds ?? new string[0];
            for (int i = 0; i < selectedIds.Length && i < 3; i++)
                MakeText(right.transform, EvidenceTitle(selectedIds[i]), 24f, 136f + i * 56f, 584f, 50f, 22f, PuzzleUi.MutedColor);

            if (Puzzle.correctChoiceIds != null && Puzzle.correctChoiceIds.Length > 0)
            {
                for (int i = 0; i < Puzzle.correctChoiceIds.Length && i < 3; i++)
                {
                    var choiceId = Puzzle.correctChoiceIds[i];
                    bool selected = IsChoice(choiceId);
                    MakeButton(right.transform, PuzzleUi.ChoiceLabel(choiceId), 24f, 400f + i * 74f, 584f, 64f,
                        () => Send(CommandKinds.SelectChoice, choiceId), selected ? PuzzleUi.AccentColor : (Color?)null, 22f);
                }
            }

            MakeButton(right.transform, Label("ui.statement_present", "증거 제시"), 24f, 640f, 584f, 64f,
                () => Send(CommandKinds.PresentEvidence), new Color(0.16f, 0.3f, 0.22f, 0.96f), 26f);
        }

        private void OpenEvidencePicker()
        {
            var current = new List<string>(Draft.selectedEvidenceIds ?? new string[0]);
            App.Evidence.ShowPicker("제시할 증거", 3, current,
                ids => Send(CommandKinds.SelectEvidence, null, ids.ToArray()));
        }
    }

    internal sealed class SlotAssignWidget : PuzzleWidget
    {
        private string _selectedSlot = "";

        public SlotAssignWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "SlotAssignWidget");
            BuildHeader(Root.transform, "각 항목에 알맞은 값을 배치하십시오.");

            var slots = MakeBox(Root.transform, "Slots", 48f, 150f, 820f, 740f, PuzzleUi.PanelColor);
            var pairs = Puzzle.valuePairs ?? new StringPairDto[0];
            for (int i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];
                bool selected = _selectedSlot == pair.key;
                var row = MakeBox(slots.transform, "Slot_" + pair.key, 12f, 12f + i * 110f, 796f, 96f,
                    selected ? PuzzleUi.SelectedColor : PuzzleUi.RowColor);
                var key = pair.key;
                UiKit.HitArea(row, () => SelectSlot(key));
                MakeText(row.transform, PuzzleUi.SlotLabel(pair.key), 16f, 8f, 380f, 38f, 22f, PuzzleUi.MutedColor);
                var placed = PlacedValue(pair.key);
                MakeText(row.transform, string.IsNullOrEmpty(placed) ? "-" : DisplayValue(placed), 16f, 46f, 760f, 40f, 24f, PuzzleUi.TextColor);
            }

            var candidates = MakeBox(Root.transform, "Candidates", 890f, 150f, 982f, 740f, PuzzleUi.PanelColor);
            MakeText(candidates.transform, "값 선택", 24f, 14f, 934f, 38f, 24f, PuzzleUi.TextColor);
            var values = CandidateValues();
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                bool placed = HasPlacedValue(value);
                MakeButton(candidates.transform, DisplayValue(value), 24f, 60f + i * 76f, 934f, 64f,
                    () => Assign(value), placed ? PuzzleUi.AccentColor : (Color?)null, 22f);
            }
        }

        private void SelectSlot(string key)
        {
            _selectedSlot = key;
            Rebuild();
        }

        private List<string> CandidateValues()
        {
            var values = new List<string>();
            var pairs = Puzzle.valuePairs ?? new StringPairDto[0];
            for (int i = 0; i < pairs.Length; i++)
            {
                var value = pairs[i] != null ? pairs[i].value : null;
                if (!string.IsNullOrEmpty(value) && !values.Contains(value)) values.Add(value);
            }
            if (Puzzle.type == PuzzleTypes.TraceMapping)
            {
                string[] modules = { "perception", "planner", "brake_supervisor", "vehicle_controller" };
                for (int i = 0; i < modules.Length; i++)
                    if (!values.Contains(modules[i])) values.Add(modules[i]);
            }
            return values;
        }

        private void Assign(string value)
        {
            if (string.IsNullOrEmpty(_selectedSlot))
            {
                ShowResult(CommandResult.Reject("no_slot", "슬롯을 먼저 선택하십시오."));
                return;
            }
            Send(CommandKinds.PlaceCard, null, null, _selectedSlot, value);
        }
    }

    internal sealed class OrderingWidget : PuzzleWidget
    {
        public OrderingWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "OrderingWidget");
            BuildHeader(Root.transform, Label("ui.timeline_order_hint", "사건 카드를 시간 순서대로 배치하십시오."));

            var pool = MakeBox(Root.transform, "Pool", 48f, 150f, 700f, 740f, PuzzleUi.PanelColor);
            MakeText(pool.transform, Label("ui.causal_cards", "카드함"), 24f, 14f, 652f, 38f, 24f, PuzzleUi.TextColor);
            var ids = new List<string>(Puzzle.orderedIds ?? new string[0]);
            ids.Sort(CompareStable);
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                MakeButton(pool.transform, CardLabel(id), 24f, 60f + i * 76f, 652f, 64f,
                    () => Send(CommandKinds.PlaceCard, null, null, "order", id), null, 22f);
            }

            var ordered = MakeBox(Root.transform, "Ordered", 770f, 150f, 640f, 740f, PuzzleUi.PanelColor);
            MakeText(ordered.transform, "배치 순서", 24f, 14f, 592f, 38f, 24f, PuzzleUi.TextColor);
            var placed = Draft.placedCards ?? new PlacedCardDto[0];
            int count = 0;
            for (int i = 0; i < placed.Length; i++)
            {
                if (placed[i] == null || placed[i].slot != "order") continue;
                var value = placed[i].value;
                var row = MakeText(ordered.transform, (count + 1) + ". " + CardLabel(value), 24f, 60f + count * 64f, 592f, 56f, 24f, PuzzleUi.TextColor);
                UiKit.HitArea(row.gameObject, () => Send(CommandKinds.PlaceCard, null, null, "order", value));
                count++;
            }
            if (count == 0)
                MakeText(ordered.transform, "카드를 클릭해 순서대로 배치하십시오.", 24f, 60f, 592f, 56f, 22f, PuzzleUi.MutedColor);

            if (Puzzle.correctChoiceIds != null && Puzzle.correctChoiceIds.Length > 0)
            {
                var choices = MakeBox(Root.transform, "Choices", 1430f, 150f, 442f, 740f, PuzzleUi.PanelColor);
                MakeText(choices.transform, ChoiceTitle(), 20f, 14f, 402f, 38f, 24f, PuzzleUi.TextColor);
                for (int i = 0; i < Puzzle.correctChoiceIds.Length && i < 3; i++)
                {
                    var choiceId = Puzzle.correctChoiceIds[i];
                    bool selected = IsChoice(choiceId);
                    MakeButton(choices.transform, PuzzleUi.ChoiceLabel(choiceId), 20f, 60f + i * 76f, 402f, 64f,
                        () => Send(CommandKinds.SelectChoice, choiceId), selected ? PuzzleUi.AccentColor : (Color?)null, 20f);
                }
            }

            SubmitButton(Root.transform);
        }

        private string ChoiceTitle()
        {
            if (Puzzle.type == PuzzleTypes.TimelineOrder) return Label("ui.timeline_interpret", "해석 선택");
            if (Puzzle.type == PuzzleTypes.Chronology) return "최종 상태";
            if (Puzzle.type == PuzzleTypes.DeploymentChain) return "결론";
            return "선택";
        }

        private static int CompareStable(string a, string b)
        {
            int hashA = PuzzleUi.StableHash(a);
            int hashB = PuzzleUi.StableHash(b);
            if (hashA != hashB) return hashA < hashB ? -1 : 1;
            return string.CompareOrdinal(a, b);
        }
    }

    internal sealed class CrossCameraWidget : PuzzleWidget
    {
        public CrossCameraWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "CrossCameraWidget");
            BuildHeader(Root.transform, Label("ui.compare_error", "같은 시간 구간의 자료를 비교하십시오."));

            var windows = MakeBox(Root.transform, "Windows", 48f, 150f, 700f, 740f, PuzzleUi.PanelColor);
            MakeText(windows.transform, Label("ui.select_time", "시점 선택"), 24f, 14f, 652f, 38f, 24f, PuzzleUi.TextColor);
            string[] ids = { "t_minus_2_to_minus_0_3", "before_event", "after_event" };
            string[] labels = { "충돌 2.0초~0.3초 전", "충돌 8~6초 전", "충돌 1~3초 후" };
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                bool selected = Draft.comparisonWindowId == id;
                MakeButton(windows.transform, labels[i], 24f, 70f + i * 90f, 652f, 76f,
                    () => Send(CommandKinds.SelectComparisonWindow, id), selected ? PuzzleUi.AccentColor : (Color?)null, 24f);
            }

            var evidence = MakeBox(Root.transform, "Evidence", 770f, 150f, 1102f, 740f, PuzzleUi.PanelColor);
            MakeText(evidence.transform, Label("ui.compare_window", "비교 구간"), 24f, 14f, 1054f, 38f, 24f, PuzzleUi.TextColor);
            MakeButton(evidence.transform, Label("ui.compare_add", "비교함에 추가"), 24f, 64f, 280f, 56f, OpenPicker, null, 22f);
            var selectedIds = Draft.selectedEvidenceIds ?? new string[0];
            for (int i = 0; i < selectedIds.Length && i < 3; i++)
                MakeText(evidence.transform, EvidenceTitle(selectedIds[i]), 24f, 140f + i * 56f, 1054f, 50f, 22f, PuzzleUi.MutedColor);

            SubmitButton(Root.transform);
        }

        private void OpenPicker()
        {
            var current = new List<string>(Draft.selectedEvidenceIds ?? new string[0]);
            App.Evidence.ShowPicker("비교 자료 선택", 3, current,
                ids => Send(CommandKinds.SelectEvidence, null, ids.ToArray()));
        }
    }

    internal sealed class ChoiceWidget : PuzzleWidget
    {
        private static readonly string[] Options = { "safe_by_count", "all_certainly_unsafe", "exposure_not_equivalent" };

        public ChoiceWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "ChoiceWidget");
            BuildHeader(Root.transform, "가장 타당한 주장을 하나 선택하십시오.");

            var box = MakeBox(Root.transform, "Choices", 460f, 220f, 1000f, 520f, PuzzleUi.PanelColor);
            for (int i = 0; i < Options.Length; i++)
            {
                var id = Options[i];
                bool selected = IsChoice(id);
                MakeButton(box.transform, PuzzleUi.ChoiceLabel(id), 40f, 40f + i * 150f, 920f, 120f,
                    () => Send(CommandKinds.SelectChoice, id), selected ? PuzzleUi.AccentColor : (Color?)null, 26f);
            }

            SubmitButton(Root.transform);
        }
    }

    internal sealed class DiffSelectWidget : PuzzleWidget
    {
        public DiffSelectWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "DiffSelectWidget");
            BuildHeader(Root.transform, Label("ui.diff_select", "삭제된 블록을 선택하십시오."));

            MakeText(Root.transform, "변경 전 v3.4", 460f, 148f, 600f, 42f, 28f, PuzzleUi.TextColor);
            MakeText(Root.transform, "변경 후 v3.5", 1100f, 148f, 600f, 42f, 28f, PuzzleUi.TextColor);

            BuildBlock(460f, 200f, "imminent_override", "if independent_imminent_collision: return emergency_request", false);
            BuildBlock(1100f, 200f, "imminent_override", "if independent_imminent_collision: return emergency_request", false);
            BuildBlock(460f, 380f, "health_snapshot_validation",
                "health = await sensor_health_snapshot / if health is missing or health.valid == false or health.age_ms > 100: return emergency_request", false);
            BuildBlock(1100f, 380f, "health_snapshot_validation",
                "// 제거됨: 운행 진단이 결함차 배차를 막는다는 전제로 대체", true);
            BuildBlock(460f, 560f, "rear_risk_arbitration", "if rear_risk_score > 70: return limited_request(-2.0)", false);
            BuildBlock(1100f, 560f, "rear_risk_arbitration", "if rear_risk_score > 70: return limited_request(-2.0)", false);

            var cost = MakeBox(Root.transform, "Costs", 460f, 730f, 1240f, 170f, PuzzleUi.PanelColor);
            MakeText(cost.transform, Label("ui.diff_cost", "비용 설명"), 20f, 12f, 1200f, 36f, 24f, PuzzleUi.TextColor);
            var costA = "scalar_compare_cost_21ms";
            var costB = "snapshot_sync_cost";
            MakeButton(cost.transform, PuzzleUi.ChoiceLabel(costA), 20f, 56f, 590f, 96f,
                () => Send(CommandKinds.SelectChoice, costA), IsChoice(costA) ? PuzzleUi.AccentColor : (Color?)null, 22f);
            MakeButton(cost.transform, PuzzleUi.ChoiceLabel(costB), 630f, 56f, 590f, 96f,
                () => Send(CommandKinds.SelectChoice, costB), IsChoice(costB) ? PuzzleUi.AccentColor : (Color?)null, 22f);

            SubmitButton(Root.transform);
        }

        private void BuildBlock(float x, float y, string blockId, string text, bool removed)
        {
            bool selected = IsChoice(blockId);
            var color = selected ? PuzzleUi.SelectedColor : (removed ? PuzzleUi.RemovedColor : PuzzleUi.RowColor);
            var block = MakeBox(Root.transform, "Block_" + blockId + "_" + (int)x, x, y, 600f, 150f, color);
            UiKit.HitArea(block, () => Send(CommandKinds.SelectChoice, blockId));
            MakeText(block.transform, text, 16f, 12f, 568f, 126f, 19f, removed ? PuzzleUi.MutedColor : PuzzleUi.TextColor);
        }
    }

    internal sealed class AssumptionWidget : PuzzleWidget
    {
        private static readonly string[] Options =
        {
            "faulty_vehicle_not_dispatched", "perfect_ai", "never_rains", "human_driver_present"
        };

        public AssumptionWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "AssumptionWidget");
            BuildHeader(Root.transform, "설계 전제를 선택하고 반증 자료를 연결하십시오.");

            var box = MakeBox(Root.transform, "Assumptions", 48f, 150f, 860f, 740f, PuzzleUi.PanelColor);
            for (int i = 0; i < Options.Length; i++)
            {
                var id = Options[i];
                bool selected = IsChoice(id);
                MakeButton(box.transform, PuzzleUi.ChoiceLabel(id), 24f, 60f + i * 110f, 812f, 96f,
                    () => Send(CommandKinds.SelectChoice, id), selected ? PuzzleUi.AccentColor : (Color?)null, 24f);
            }

            var evidence = MakeBox(Root.transform, "Evidence", 930f, 150f, 942f, 740f, PuzzleUi.PanelColor);
            MakeText(evidence.transform, "반증 자료", 24f, 14f, 894f, 38f, 24f, PuzzleUi.TextColor);
            MakeButton(evidence.transform, "증거 선택", 24f, 64f, 280f, 56f, OpenPicker, null, 22f);
            var selectedIds = Draft.selectedEvidenceIds ?? new string[0];
            for (int i = 0; i < selectedIds.Length && i < 3; i++)
                MakeText(evidence.transform, EvidenceTitle(selectedIds[i]), 24f, 140f + i * 56f, 894f, 50f, 22f, PuzzleUi.MutedColor);

            SubmitButton(Root.transform);
        }

        private void OpenPicker()
        {
            var current = new List<string>(Draft.selectedEvidenceIds ?? new string[0]);
            App.Evidence.ShowPicker("반증 자료", 3, current,
                ids => Send(CommandKinds.SelectEvidence, null, ids.ToArray()));
        }
    }

    internal sealed class MatrixWidget : PuzzleWidget
    {
        private static readonly string[] Keys = { "sensor", "overrideState", "version", "condition" };

        public MatrixWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "MatrixWidget");
            BuildHeader(Root.transform, Label("ui.matrix_note", "사고 조합에 해당하는 행을 선택하십시오."));

            var dims = MakeBox(Root.transform, "Dimensions", 48f, 150f, 1180f, 300f, PuzzleUi.PanelColor);
            for (int i = 0; i < Keys.Length; i++)
            {
                var key = Keys[i];
                MakeText(dims.transform, PuzzleUi.DimensionLabel(key), 20f, 24f + i * 68f, 180f, 48f, 22f, PuzzleUi.MutedColor);
                var options = DimensionOptions(key);
                for (int j = 0; j < options.Length; j++)
                {
                    var option = options[j];
                    bool selected = PlacedValue(key) == option;
                    MakeButton(dims.transform, PuzzleUi.OptionLabel(option), 210f + j * 240f, 12f + i * 68f, 224f, 56f,
                        () => Send(CommandKinds.PlaceCard, null, null, key, option), selected ? PuzzleUi.AccentColor : (Color?)null, 20f);
                }
            }

            BuildTable();

            var claims = MakeBox(Root.transform, "Claims", 1250f, 150f, 622f, 800f, PuzzleUi.PanelColor);
            MakeText(claims.transform, "시험 범위 판단", 24f, 14f, 574f, 38f, 24f, PuzzleUi.TextColor);
            var runs = PairValue("simulationRuns");
            if (!string.IsNullOrEmpty(runs))
                MakeText(claims.transform, Label("ui.matrix_total", "총 시뮬레이션") + ": " + runs, 24f, 62f, 574f, 38f, 22f, PuzzleUi.MutedColor);
            string[] claimsIds = { "all_tests_fake", "validated_all", "unvalidated_combination" };
            for (int i = 0; i < claimsIds.Length; i++)
            {
                var id = claimsIds[i];
                bool selected = IsChoice(id);
                MakeButton(claims.transform, PuzzleUi.ChoiceLabel(id), 24f, 120f + i * 110f, 574f, 96f,
                    () => Send(CommandKinds.SelectChoice, id), selected ? PuzzleUi.AccentColor : (Color?)null, 22f);
            }

            SubmitButton(Root.transform);
        }

        private void BuildTable()
        {
            var table = MakeBox(Root.transform, "Matrix", 48f, 470f, 1180f, 480f, new Color(0.05f, 0.06f, 0.09f, 0.96f));
            string[] headers = { "센서", "수동 해제", "버전", "조건", "검증" };
            float[] cols = { 14f, 150f, 330f, 540f, 700f };
            float[] widths = { 130f, 172f, 200f, 152f, 440f };
            for (int i = 0; i < headers.Length; i++)
                MakeText(table.transform, headers[i], cols[i], 10f, widths[i], 30f, 20f, PuzzleUi.MutedColor);

            var rows = App.Content.Matrix != null ? App.Content.Matrix.rows : null;
            if (rows == null) return;
            for (int i = 0; i < rows.Length && i < 8; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                bool match = RowMatches(row);
                var rowGo = MakeBox(table.transform, "Row_" + row.index, 8f, 44f + i * 52f, 1164f, 48f,
                    match ? PuzzleUi.SelectedColor : PuzzleUi.RowColor);
                MakeText(rowGo.transform, PuzzleUi.OptionLabel(row.sensor), cols[0], 6f, widths[0], 36f, 18f, PuzzleUi.TextColor);
                MakeText(rowGo.transform, PuzzleUi.OptionLabel(row.overrideState), cols[1], 6f, widths[1], 36f, 18f, PuzzleUi.TextColor);
                MakeText(rowGo.transform, PuzzleUi.OptionLabel(row.version), cols[2], 6f, widths[2], 36f, 18f, PuzzleUi.TextColor);
                MakeText(rowGo.transform, PuzzleUi.OptionLabel(row.condition), cols[3], 6f, widths[3], 36f, 18f, PuzzleUi.TextColor);
                MakeText(rowGo.transform, row.verification + " · " + row.note, cols[4], 6f, widths[4], 36f, 17f, PuzzleUi.MutedColor);
            }
        }

        private bool RowMatches(MatrixRowDto row)
        {
            return row.sensor == PlacedValue("sensor")
                && row.overrideState == PlacedValue("overrideState")
                && row.version == PlacedValue("version")
                && row.condition == PlacedValue("condition");
        }

        private string[] DimensionOptions(string key)
        {
            var matrix = App.Content.Matrix;
            if (matrix == null || matrix.dimensions == null) return new string[0];
            switch (key)
            {
                case "sensor": return matrix.dimensions.sensor ?? new string[0];
                case "overrideState": return matrix.dimensions.overrideState ?? new string[0];
                case "version": return matrix.dimensions.version ?? new string[0];
                case "condition": return matrix.dimensions.condition ?? new string[0];
                default: return new string[0];
            }
        }
    }

    internal sealed class CounterfactualWidget : PuzzleWidget
    {
        public CounterfactualWidget(AppRoot app, PuzzleDefinitionDto puzzle, PuzzleDraftDto draft, Transform parent)
            : base(app, puzzle, draft, parent)
        {
        }

        public override void Build(Transform parent)
        {
            Root = PuzzleUi.Transparent(parent, "CounterfactualWidget");
            BuildHeader(Root.transform, "세 사례를 모두 실행해야 합니다.");

            var cases = MakeBox(Root.transform, "Cases", 48f, 150f, 620f, 740f, PuzzleUi.PanelColor);
            var required = Puzzle.requiredCaseIds ?? new string[0];
            for (int i = 0; i < required.Length; i++)
            {
                var caseId = required[i];
                bool ran = HasRanCase(caseId);
                var caption = PuzzleUi.CaseLabel(caseId) + (ran ? " · " + Label("ui.counterfactual_ran", "실행됨") : "");
                MakeButton(cases.transform, caption, 24f, 60f + i * 110f, 572f, 96f,
                    () => Send(CommandKinds.RunCounterfactualCase, caseId), ran ? PuzzleUi.AccentColor : (Color?)null, 24f);
            }

            var results = MakeBox(Root.transform, "Results", 700f, 150f, 1172f, 810f, PuzzleUi.PanelColor);
            var kinematics = App.Content.Graph != null ? App.Content.Graph.kinematics : null;
            float y = 20f;
            for (int i = 0; i < required.Length && i < 3; i++)
            {
                BuildCaseResult(results.transform, required[i], kinematics, y);
                y += 230f;
            }
            var note = kinematics != null ? kinematics.note : "";
            if (!string.IsNullOrEmpty(note))
                MakeText(results.transform, note, 24f, 710f, 1124f, 70f, 20f, PuzzleUi.MutedColor);

            SubmitButton(Root.transform);
        }

        private void BuildCaseResult(Transform parent, string caseId, KinematicsDto kinematics, float y)
        {
            bool ran = HasRanCase(caseId);
            var panel = MakeBox(parent, "Case_" + caseId, 20f, y, 1132f, 220f,
                ran ? PuzzleUi.RowColor : new Color(0.05f, 0.06f, 0.09f, 0.9f));
            MakeText(panel.transform, PuzzleUi.CaseLabel(caseId), 18f, 10f, 1096f, 36f, 24f, PuzzleUi.TextColor);
            var body = ran ? CaseBody(caseId, kinematics) : Label("ui.counterfactual_run", "실행");
            MakeText(panel.transform, body, 18f, 52f, 1096f, 156f, 22f, ran ? PuzzleUi.TextColor : PuzzleUi.MutedColor);
        }

        private string CaseBody(string caseId, KinematicsDto kinematics)
        {
            string master = PuzzleUi.MasterLabel(PairValue(caseId));
            if (caseId == "dispatch_blocked") return "운행 없음";
            if (kinematics == null) return master;
            if (caseId == "historical")
                return master + "\n충돌까지 거리 " + F(kinematics.historicalDistanceMeters) + "m · 충돌 속도 " + F(kinematics.historicalImpactKmh) + "km/h"
                    + "\n요청 감속 -8.1 / 실제 강한 감속 -" + F(kinematics.strongDeceleration);
            return master + "\n완전 제동 정지거리 " + F(kinematics.fullBrakeStopMeters) + "m · 노면 감속 범위 정지거리 "
                + F(kinematics.stopDistanceMinMeters) + "~" + F(kinematics.stopDistanceMaxMeters) + "m";
        }

        private static string F(double value)
        {
            return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
