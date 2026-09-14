using System;
using System.Collections.Generic;
using ANL.Core.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class ArchiveScreen : AnlScreen
    {
        public Action OnExit;

        private GameObject _background;
        private GameObject _contentRoot;
        private GameObject _lockedRoot;
        private RectTransform _content;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _confirmText;
        private GameObject _confirm;
        private Action _confirmAction;
        private int _tab;

        public ArchiveScreen(AppRoot app) : base(app)
        {
        }

        public override void Build()
        {
            Root = UiKit.Panel(Kit.ContentLayer, "ArchiveScreen", new Color(0.02f, 0.03f, 0.05f, 1f));
            UiKit.Stretch(Root);

            _background = UiKit.Panel(Root.transform, "Background", Color.white);
            UiKit.Stretch(_background);
            _background.GetComponent<Image>().raycastTarget = false;

            var header = UiKit.Panel(Root.transform, "Header", new Color(0.03f, 0.04f, 0.07f, 0.94f));
            UiKit.SetRect(header, 40f, 30f, 1840f, 150f);
            _title = UiKit.Label(header.transform, UiKit.Ui(App.Content, "ui.archive_title", "사건 기록"), 40f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(_title.gameObject, 24f, 16f, 900f, 52f);

            var back = UiKit.TextButton(header.transform, UiKit.Ui(App.Content, "ui.back_to_title", "타이틀로"), 24f, GoTitle, null, 220f, 56f);
            UiKit.SetRect(back.gameObject, 1370f, 18f, 220f, 56f);
            var restart = UiKit.TextButton(header.transform, UiKit.Ui(App.Content, "ui.restart_episode", "새 리플레이 슬롯으로 다시 시작"), 22f, RequestRestart, null, 240f, 56f);
            UiKit.SetRect(restart.gameObject, 1604f, 18f, 240f, 56f);

            _contentRoot = new GameObject("ContentRoot", typeof(RectTransform));
            _contentRoot.transform.SetParent(Root.transform, false);
            UiKit.Stretch(_contentRoot);

            float x = 40f;
            string[] tabs =
            {
                UiKit.Ui(App.Content, "ui.archive_report", "최종 보고"),
                UiKit.Ui(App.Content, "ui.archive_responsibility", "책임 행위표"),
                UiKit.Ui(App.Content, "ui.archive_prevention", "예방 권고"),
                UiKit.Ui(App.Content, "ui.archive_unresolved", "미확정 사항"),
                UiKit.Ui(App.Content, "ui.archive_sources", "자료 출처")
            };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var button = UiKit.TextButton(_contentRoot.transform, tabs[i], 23f, () => SelectTab(index), null, 340f, 56f);
                UiKit.SetRect(button.gameObject, x, 194f, 340f, 56f);
                x += 350f;
            }

            RectTransform content;
            var scroll = UiKit.Scroll(_contentRoot.transform, "Content", out content);
            UiKit.SetRect(scroll.gameObject, 40f, 266f, 1840f, 760f);
            _content = content;

            _lockedRoot = UiKit.Panel(_contentRoot.transform, "Locked", new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UiKit.SetRect(_lockedRoot, 560f, 400f, 800f, 240f);
            var locked = UiKit.Label(_lockedRoot.transform, UiKit.Ui(App.Content, "ui.archive_locked", "사건을 완료하면 열립니다."), 28f, new Color(0.9f, 0.85f, 0.7f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(locked.gameObject, 28f, 30f, 744f, 100f);
            var lockedBack = UiKit.TextButton(_lockedRoot.transform, UiKit.Ui(App.Content, "ui.back_to_title", "타이틀로"), 24f, GoTitle, null, 220f, 56f);
            UiKit.SetRect(lockedBack.gameObject, 552f, 150f, 220f, 56f);
            _lockedRoot.SetActive(false);

            BuildConfirm();
        }

        private void BuildConfirm()
        {
            _confirm = UiKit.Dim(Root.transform, "Confirm");
            var box = UiKit.Panel(_confirm.transform, "Box", new Color(0.05f, 0.06f, 0.09f, 1f));
            UiKit.SetRect(box, 560f, 380f, 800f, 320f);
            _confirmText = UiKit.Label(box.transform, "", 24f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_confirmText.gameObject, 28f, 24f, 744f, 200f);
            var ok = UiKit.TextButton(box.transform, UiKit.Ui(App.Content, "ui.confirm", "확인"), 25f, Confirm, new Color(0.16f, 0.3f, 0.22f, 0.96f), 180f, 56f);
            UiKit.SetRect(ok.gameObject, 430f, 242f, 180f, 56f);
            var cancel = UiKit.TextButton(box.transform, UiKit.Ui(App.Content, "ui.cancel", "취소"), 25f, HideConfirm, null, 180f, 56f);
            UiKit.SetRect(cancel.gameObject, 620f, 242f, 180f, 56f);
            _confirm.SetActive(false);
        }

        private void SelectTab(int tab)
        {
            _tab = tab;
            Refresh();
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            var state = App.Session.State;
            bool unlocked = state.IsSceneCompleted("s710");
            _lockedRoot.SetActive(!unlocked);
            _content.gameObject.SetActive(unlocked);
            UiKit.ClearChildren(_content);
            if (!unlocked) return;

            switch (_tab)
            {
                case 0: BuildReport(state); break;
                case 1: BuildResponsibility(); break;
                case 2: BuildPrevention(); break;
                case 3: BuildUnresolved(); break;
                default: BuildSources(state); break;
            }
        }

        private void BuildReport(ANL.Core.State.GameState state)
        {
            AddHeader(UiKit.Ui(App.Content, "ui.archive_report", "최종 보고"));
            var draft = state.GetDraft("p20", false);
            if (draft != null && draft.reportPlacements != null && draft.reportPlacements.Length > 0)
            {
                for (int i = 0; i < draft.reportPlacements.Length; i++)
                {
                    var placement = draft.reportPlacements[i];
                    AddLine(SectionName(placement.section) + " · " + ItemName(placement.item));
                }
            }
            else
            {
                var puzzle = App.Content.Puzzle("p20");
                if (puzzle != null) AddLine(puzzle.successText);
                var nodes = App.Content.GraphNodes();
                for (int i = 0; i < nodes.Count; i++)
                    AddLine(nodes[i].label);
            }
        }

        private string SectionName(string section)
        {
            switch (section)
            {
                case "direct": return UiKit.Ui(App.Content, "ui.report_direct", "직접 원인");
                case "contributions": return UiKit.Ui(App.Content, "ui.report_contributions", "기여 요인");
                case "organizational": return UiKit.Ui(App.Content, "ui.report_organizational", "조직·관리");
                case "post": return UiKit.Ui(App.Content, "ui.report_post", "사고 이후 행위");
                case "limit": return UiKit.Ui(App.Content, "ui.report_limit", "판단 한계");
                default: return section;
            }
        }

        private static string ItemName(string item)
        {
            switch (item)
            {
                case "unsafe_brake_limiting": return "결함 후방 입력이 긴급제동 제한에 사용됨";
                case "radar_fault": return "없는 객체를 보고한 센서 결함";
                case "override_after_warning": return "위험 보고 확인 후 차량 운행중지 해제";
                case "health_check_removed": return "제동 경계에서 건강정보 확인 경로 제거";
                case "release_conditions_open": return "조건부 승인 사항을 완료하지 않은 배포";
                case "conflicting_kpi": return "안전 운휴와 충돌한 가동률·비용 관리";
                case "known_risk_continued": return "결합 위험과 중지 권고 인지 후 운영 유지";
                case "misleading_public_summary": return "사후 재분석을 당시 판단처럼 제시한 공개";
                case "no_intent_or_criminal_verdict": return "고의·형사 죄명을 확정하지 않고 사실과 증거를 이관";
                default: return item;
            }
        }

        private void BuildResponsibility()
        {
            AddHeader(UiKit.Ui(App.Content, "ui.archive_responsibility", "책임 행위표"));
            var header = UiKit.Panel(_content, "HeaderRow", new Color(0.1f, 0.12f, 0.18f, 1f));
            header.AddComponent<LayoutElement>().preferredHeight = 56f;
            AddCell(header.transform, 0f, "행위자", true);
            AddCell(header.transform, 220f, "행위", true);
            AddCell(header.transform, 620f, "인지", true);
            AddCell(header.transform, 1060f, "권한", true);

            string[,] rows =
            {
                { "서지원", "위험 보고 이후 제한 운영 유지 결정", "결합 위험 보고 확인(ev29)·확인 발언(ev30)", "운행 중지·복귀 결정 권한" },
                { "최민규", "D-1 09:31 수동 운행 해제", "재발 보고 확인 회신(ev19)·본인 진술(ev31)", "차량 운행 상태·배차 권한" },
                { "박도윤", "제동 조정기 3.5 조건부 승인", "최초 반대·조건부 승인(ev23)", "배포 승인 권한" },
                { "이재현", "건강정보 동기화 경로 제거 변경", "성능 측정값(ev24)·조건부 승인(ev23)", "코드 변경·배포 권한" },
                { "윤해린", "우천 재발 보고·운행 금지 요청", "우천 재발 기록(ev17)·경고 메일(ev19)", "정비·검사 권한" },
                { "한서진", "발표 자료 수정·승인", "사후 재분석 표기 삭제(ev36)", "대외 발표 승인 권한" }
            };
            for (int r = 0; r < rows.GetLength(0); r++)
            {
                var row = UiKit.Panel(_content, "Row", new Color(0.06f, 0.07f, 0.11f, 0.96f));
                row.AddComponent<LayoutElement>().preferredHeight = 96f;
                AddCell(row.transform, 0f, rows[r, 0], false);
                AddCell(row.transform, 220f, rows[r, 1], false);
                AddCell(row.transform, 620f, rows[r, 2], false);
                AddCell(row.transform, 1060f, rows[r, 3], false);
            }
        }

        private void AddCell(Transform row, float x, string text, bool header)
        {
            var label = UiKit.Label(row, text, header ? 24f : 21f,
                header ? new Color(0.95f, 0.85f, 0.5f, 1f) : Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(label.gameObject, x + 10f, 8f, 400f, 80f);
        }

        private void BuildPrevention()
        {
            AddHeader(UiKit.Ui(App.Content, "ui.archive_prevention", "예방 권고"));
            AddLine("위험 차량 운행 해제 통제");
            AddLine("수동 해제와 임시 보정의 예외 범위를 제한하고, 해제 이력을 배차 판단에 강제로 반영한다.");
            AddLine("조건부 승인 이행 확인");
            AddLine("승인 조건이 닫히기 전에는 배포가 진행되지 않도록 조건 완료 근거를 필수로 기록한다.");
            AddLine("결함·운영 예외 조합 시험");
            AddLine("결함 센서·수동 해제·신규 제어기·야간 강우 조합을 시험 범위에 포함한다.");
            AddLine("자료 출처 공개");
            AddLine("실시간 기록과 사후 재분석을 구분해 출처와 생성 방식을 함께 공개한다.");
        }

        private void BuildUnresolved()
        {
            AddHeader(UiKit.Ui(App.Content, "ui.archive_unresolved", "미확정 사항"));
            AddLine("고의");
            AddLine("사망을 의도했다는 증거는 확정되지 않았다.");
            AddLine("형사 죄명");
            AddLine("법적 죄명과 형량은 이 조사의 범위가 아니다.");
            AddLine("장기적 모델 안전성");
            AddLine("장기적 모델 안전성은 이번 자료만으로 판단하지 않는다.");
        }

        private void BuildSources(ANL.Core.State.GameState state)
        {
            AddHeader(UiKit.Ui(App.Content, "ui.archive_sources", "자료 출처"));
            for (int i = 0; i < state.OwnedEvidence.Count; i++)
            {
                var evidence = App.Content.EvidenceDef(state.OwnedEvidence[i]);
                if (evidence == null) continue;
                AddLine(evidence.title + " — " + evidence.sourceMetadata);
            }
        }

        private void AddHeader(string text)
        {
            var label = UiKit.Label(_content, text, 32f, new Color(0.95f, 0.85f, 0.5f, 1f), TextAlignmentOptions.TopLeft, true);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        }

        private void AddLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var label = UiKit.Label(_content, text, 23f, Color.white, TextAlignmentOptions.TopLeft, true);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Max(40f, Mathf.CeilToInt(text.Length / 80f) * 34f + 10f);
        }

        private void GoTitle()
        {
            SetVisible(false);
            if (OnExit != null)
            {
                OnExit();
                return;
            }
            App.Session.Dispatcher.EnterScene("s000");
            App.Refresh();
        }

        private void RequestRestart()
        {
            ShowConfirm(UiKit.Ui(App.Content, "ui.new_game_confirm", "새 사건을 시작할까요? 현재 자동 저장은 새 진행으로 대체됩니다."), Restart);
        }

        private void Restart()
        {
            App.Session.NewGame(NextFreeSlot());
            int guard = 0;
            while (App.Session.State.Phase == ANL.Core.State.Phases.PreDialogue && guard++ < 32)
                App.Exec(CommandKinds.AdvanceDialogue);
            App.Exec(CommandKinds.ContinueScene);
            SetVisible(false);
            App.Refresh();
        }

        private string NextFreeSlot()
        {
            string[] slots = { "slot_01", "slot_02", "slot_03" };
            for (int i = 0; i < slots.Length; i++)
                if (!App.Session.Saves.Exists(slots[i])) return slots[i];
            return "slot_03";
        }

        private void ShowConfirm(string message, Action action)
        {
            _confirmAction = action;
            _confirmText.text = message;
            _confirm.SetActive(true);
        }

        private void HideConfirm()
        {
            _confirmAction = null;
            _confirm.SetActive(false);
        }

        private void Confirm()
        {
            _confirm.SetActive(false);
            var action = _confirmAction;
            _confirmAction = null;
            if (action != null) action();
        }
    }
}
