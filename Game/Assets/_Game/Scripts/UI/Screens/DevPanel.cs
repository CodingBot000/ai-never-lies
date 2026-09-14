using System;
using System.Collections.Generic;
using ANL.Core.Commands;
using ANL.Core.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class DevPanel : AnlScreen
    {
        [Serializable]
        private sealed class FixtureFile
        {
            public FixtureItem[] items = new FixtureItem[0];
        }

        [Serializable]
        private sealed class FixtureItem
        {
            public string id = "";
            public string description = "";
            public string currentSceneId = "";
            public string phase = "";
            public string[] ownedEvidenceIds = new string[0];
            public string[] readEvidenceIds = new string[0];
            public string[] visitedSceneIds = new string[0];
            public string[] completedSceneIds = new string[0];
            public string[] solvedPuzzleIds = new string[0];
            public string[] visitedTopicIds = new string[0];
            public StringPairDto[] branchValues = new StringPairDto[0];
            public HintUsageDto[] hintUsage = new HintUsageDto[0];
            public string[] appliedEffectIds = new string[0];
            public string[] preDialogueDoneSceneIds = new string[0];
            public string[] postDialogueDoneSceneIds = new string[0];
        }

        private TextMeshProUGUI _body;
        private TextMeshProUGUI _blocker;
        private CommandResult _last;
        private readonly List<FixtureItem> _fixtures = new List<FixtureItem>();

        public DevPanel(AppRoot app) : base(app)
        {
            IsModal = true;
        }

        public override void Build()
        {
            Root = UiKit.Panel(Kit.DevLayer, "DevPanel", new Color(0.02f, 0.03f, 0.05f, 0.97f));
            UiKit.SetRect(Root, 20f, 20f, 760f, 700f);
            var title = UiKit.Label(Root.transform, UiKit.Ui(App.Content, "ui.dev_panel", "개발 패널"), 28f, new Color(0.6f, 1f, 0.7f, 1f));
            UiKit.SetRect(title.gameObject, 18f, 12f, 400f, 40f);
            var close = UiKit.TextButton(Root.transform, UiKit.Ui(App.Content, "ui.close", "닫기"), 20f,
                () => App.CloseModal(this), null, 110f, 44f);
            UiKit.SetRect(close.gameObject, 630f, 12f, 110f, 44f);

            _body = UiKit.Label(Root.transform, "", 20f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_body.gameObject, 18f, 66f, 724f, 240f);

            var assist = UiKit.TextButton(Root.transform, UiKit.Ui(App.Content, "ui.test_assist", "정답 행동 실행"), 22f,
                RunAssist, new Color(0.16f, 0.3f, 0.22f, 0.96f), 320f, 60f);
            UiKit.SetRect(assist.gameObject, 18f, 318f, 320f, 60f);
            _blocker = UiKit.Label(Root.transform, "", 20f, new Color(1f, 0.75f, 0.6f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_blocker.gameObject, 350f, 318f, 392f, 72f);

            var fixtureTitle = UiKit.Label(Root.transform, UiKit.Ui(App.Content, "ui.dev_fixture_load", "픽스처 불러오기"), 22f, new Color(0.8f, 0.85f, 1f, 1f));
            UiKit.SetRect(fixtureTitle.gameObject, 18f, 398f, 400f, 36f);

            LoadFixtures();
            RectTransform content;
            var scroll = UiKit.Scroll(Root.transform, "Fixtures", out content);
            UiKit.SetRect(scroll.gameObject, 18f, 440f, 724f, 240f);
            BuildFixtures(content);

            if (App.Session != null) App.Session.Executed += OnExecuted;
        }

        public override void Refresh()
        {
            if (_body == null || App.Session == null || App.Content == null) return;
            var state = App.Session.State;
            var scene = App.Content.Scene(state.CurrentSceneId);
            string lastText = "-";
            if (_last != null)
                lastText = _last.status + " " + _last.reasonCode + (string.IsNullOrEmpty(_last.message) ? "" : " · " + _last.message);
            _body.text =
                UiKit.Ui(App.Content, "ui.dev_scene", "씬") + ": " + (scene != null ? scene.id : "-") + "\n" +
                UiKit.Ui(App.Content, "ui.dev_phase", "단계") + ": " + state.Phase + "\n" +
                UiKit.Ui(App.Content, "ui.dev_puzzle", "퍼즐") + ": " + (string.IsNullOrEmpty(state.ActivePuzzleId) ? "-" : state.ActivePuzzleId) + "\n" +
                "증거 " + state.OwnedEvidence.Count + " / 열람 " + state.ReadEvidence.Count + " / 완료 " + state.CompletedScenes.Count + "\n" +
                UiKit.Ui(App.Content, "ui.dev_last_result", "최근 명령") + ": " + lastText + "\n" +
                UiKit.Ui(App.Content, "ui.dev_blocker", "블로커") + ": " + (string.IsNullOrEmpty(App.Session.BlockingError) ? "-" : App.Session.BlockingError);
        }

        private void OnExecuted(CommandResult result)
        {
            _last = result;
            if (Visible) Refresh();
        }

        private void RunAssist()
        {
            if (App.Content == null || App.Session == null) return;
            var plan = TestAssistPlanner.Plan(App.Content, App.Session.State);
            if (!plan.CanRun)
            {
                if (_blocker != null) _blocker.text = plan.Blocker ?? "";
                Refresh();
                return;
            }
            if (_blocker != null) _blocker.text = "";
            for (int i = 0; i < plan.Commands.Count; i++)
            {
                var command = plan.Commands[i];
                var result = App.Session.ExecuteOrigin(Origins.TestAssist, command.type, command.targetId, command.ids, command.slot, command.value, command.numeric);
                _last = result;
                App.Refresh();
            }
        }

        private void LoadFixtures()
        {
            try
            {
                var asset = Resources.Load<TextAsset>("Content/fixtures");
                if (asset == null) return;
                var file = JsonUtility.FromJson<FixtureFile>(asset.text);
                if (file != null && file.items != null) _fixtures.AddRange(file.items);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ANL] fixture load failed: " + ex.Message);
            }
        }

        private void BuildFixtures(RectTransform content)
        {
            for (int i = 0; i < _fixtures.Count; i++)
            {
                var fixture = _fixtures[i];
                if (fixture == null) continue;
                var label = string.IsNullOrEmpty(fixture.id) ? "fixture" : fixture.id;
                if (!string.IsNullOrEmpty(fixture.description)) label += " · " + fixture.description;
                var button = UiKit.TextButton(content, label, 18f, () => ApplyFixture(fixture), null, 700f, 52f);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            }
        }

        private void ApplyFixture(FixtureItem fixture)
        {
            if (fixture == null || App.Session == null || App.Content == null) return;
            var state = App.Session.State;
            Fill(state.OwnedEvidence, fixture.ownedEvidenceIds);
            Fill(state.ReadEvidence, fixture.readEvidenceIds);
            Fill(state.VisitedScenes, fixture.visitedSceneIds);
            Fill(state.CompletedScenes, fixture.completedSceneIds);
            Fill(state.SolvedPuzzles, fixture.solvedPuzzleIds);
            Fill(state.VisitedTopics, fixture.visitedTopicIds);
            FillPairs(state.BranchValues, fixture.branchValues);
            FillUsage(state.HintUsage, fixture.hintUsage);
            Fill(state.AppliedEffectIds, fixture.appliedEffectIds);
            Fill(state.PreDialogueDone, fixture.preDialogueDoneSceneIds);
            Fill(state.PostDialogueDone, fixture.postDialogueDoneSceneIds);
            if (!string.IsNullOrEmpty(fixture.currentSceneId))
                App.Session.Dispatcher.EnterScene(fixture.currentSceneId);
            if (!string.IsNullOrEmpty(fixture.phase)) state.Phase = fixture.phase;
            App.Toast(App.Content.Ui("ui.dev_fixture_warning"));
            App.Refresh();
        }

        private static void Fill(List<string> target, string[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (!string.IsNullOrEmpty(source[i]) && !target.Contains(source[i])) target.Add(source[i]);
        }

        private static void FillPairs(List<StringPairDto> target, StringPairDto[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null && !string.IsNullOrEmpty(source[i].key)) target.Add(source[i]);
        }

        private static void FillUsage(List<HintUsageDto> target, HintUsageDto[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null && !string.IsNullOrEmpty(source[i].scopeId)) target.Add(source[i]);
        }
    }
}
