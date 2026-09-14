#if ANL_TEST_TOOLS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ANL.Core.Contracts;
using ANL.Core.State;
using UnityEngine;

namespace ANL.UI
{
    public sealed class TourRunner : MonoBehaviour
    {
        private AppRoot _app;
        private string _dir;

        [Serializable]
        private sealed class FixtureFile
        {
            public int schemaVersion;
            public FixtureItem[] items;
        }

        [Serializable]
        private sealed class FixtureItem
        {
            public string id;
            public string description;
            public string currentSceneId;
            public string phase;
            public string[] ownedEvidenceIds;
            public string[] readEvidenceIds;
            public string[] visitedSceneIds;
            public string[] completedSceneIds;
            public string[] solvedPuzzleIds;
            public string[] visitedTopicIds;
            public StringPairDto[] branchValues;
            public string[] appliedEffectIds;
            public string[] preDialogueDoneSceneIds;
            public string[] postDialogueDoneSceneIds;
        }

        public static bool Requested
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "-anlTour") return true;
                return false;
            }
        }

        public void Init(AppRoot app)
        {
            _app = app;
            StartCoroutine(Run());
        }

        private static string ArgValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return "";
        }

        private IEnumerator Run()
        {
            var dirArg = ArgValue("-anlTourDir");
            _dir = string.IsNullOrEmpty(dirArg) ? Path.Combine(Application.persistentDataPath, "tour") : dirArg;
            Directory.CreateDirectory(_dir);
            yield return new WaitForSecondsRealtime(2.5f);

            yield return Shot("s000_title");

            _app.Session.NewGame("tour");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("s000_warning");

            if (!LoadFixture("f_s150_pre")) yield break;
            yield return Shot("s150_hearing_pre");

            _app.Exec(CommandKinds.AdvanceDialogue);
            _app.Exec(CommandKinds.AdvanceDialogue);
            yield return Shot("s150_p04_widget");

            _app.Exec(CommandKinds.SelectStatement, "st150_02");
            _app.Exec(CommandKinds.SelectEvidence, null, new[] { "ev06" });
            _app.Exec(CommandKinds.PresentEvidence);
            yield return Shot("s150_p04_partial");

            _app.Exec(CommandKinds.SelectEvidence, null, new[] { "ev06", "ev07" });
            _app.Exec(CommandKinds.PresentEvidence);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("s150_post_dialogue");

            _app.Evidence.ShowBook();
            yield return Shot("s150_evidence_book");
            _app.CloseModal(_app.Evidence);

            if (!LoadFixture("f_s310_pre")) yield break;
            _app.Exec(CommandKinds.AdvanceDialogue);
            _app.Exec(CommandKinds.AdvanceDialogue);
            _app.Exec(CommandKinds.AdvanceDialogue);
            yield return Shot("s310_p09_widget");
            _app.Exec(CommandKinds.SelectStatement, "st310_02");
            _app.Exec(CommandKinds.SelectEvidence, null, new[] { "ev19" });
            _app.Exec(CommandKinds.PresentEvidence);
            yield return Shot("s310_p09_partial");
            _app.Exec(CommandKinds.SelectEvidence, null, new[] { "ev19", "ev18" });
            _app.Exec(CommandKinds.PresentEvidence);
            yield return new WaitForSecondsRealtime(0.8f);

            if (!LoadFixture("f_s440_pre")) yield break;
            _app.Exec(CommandKinds.AdvanceDialogue);
            yield return Shot("s440_p15_deployment");

            if (!LoadFixture("f_s610_pre")) yield break;
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("s610_causal_board");

            var plan = ANL.Core.Commands.TestAssistPlanner.Plan(_app.Content, _app.Session.State);
            if (plan.CanRun)
            {
                for (int i = 0; i < plan.Commands.Count; i++)
                {
                    var command = plan.Commands[i];
                    _app.Session.ExecuteOrigin(Origins.TestAssist, command.type, command.targetId, command.ids, command.slot, command.value, command.numeric);
                    _app.Refresh();
                    if (i % 6 == 0) yield return null;
                }
            }
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("s610_causal_solved");

            if (!LoadFixture("f_s640_pre")) yield break;
            _app.Exec(CommandKinds.AdvanceDialogue);
            yield return Shot("s640_report_draft");

            var reportPlan = ANL.Core.Commands.TestAssistPlanner.Plan(_app.Content, _app.Session.State);
            if (reportPlan.CanRun)
            {
                for (int i = 0; i < reportPlan.Commands.Count; i++)
                {
                    var command = reportPlan.Commands[i];
                    _app.Session.ExecuteOrigin(Origins.TestAssist, command.type, command.targetId, command.ids, command.slot, command.value, command.numeric);
                    _app.Refresh();
                    if (i % 6 == 0) yield return null;
                }
            }
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("s640_report_adopted");

            _app.OpenHints();
            yield return Shot("hints_panel");
            if (_app.HintsScreen != null) _app.CloseModal(_app.HintsScreen);

            var settings = _app.Session.Settings;
            settings.textScale = 1.5f;
            _app.Session.ApplySettings(settings);
            _app.OpenSettings();
            yield return Shot("settings_150");
            if (_app.SettingsScreen != null) _app.CloseModal(_app.SettingsScreen);

            if (!LoadFixture("f_s700_pre")) yield break;
            yield return Shot("s700_ending");

            _app.Session.State.CompletedScenes.Add("s710");
            _app.Session.Dispatcher.EnterScene("s710");
            _app.Refresh();
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("s710_archive");

            Application.Quit(0);
        }

        private bool LoadFixture(string fixtureId)
        {
            var asset = Resources.Load<TextAsset>("Content/fixtures");
            if (asset == null)
            {
                Debug.LogError("[ANL] fixtures.json not found for tour");
                return false;
            }
            var file = JsonUtility.FromJson<FixtureFile>(asset.text);
            FixtureItem item = null;
            for (int i = 0; i < file.items.Length; i++)
                if (file.items[i].id == fixtureId) item = file.items[i];
            if (item == null)
            {
                Debug.LogError("[ANL] fixture not found: " + fixtureId);
                return false;
            }
            var state = _app.Session.State;
            Fill(state.OwnedEvidence, item.ownedEvidenceIds);
            Fill(state.ReadEvidence, item.readEvidenceIds);
            Fill(state.VisitedScenes, item.visitedSceneIds);
            Fill(state.CompletedScenes, item.completedSceneIds);
            Fill(state.SolvedPuzzles, item.solvedPuzzleIds);
            Fill(state.VisitedTopics, item.visitedTopicIds);
            Fill(state.AppliedEffectIds, item.appliedEffectIds);
            Fill(state.PreDialogueDone, item.preDialogueDoneSceneIds);
            Fill(state.PostDialogueDone, item.postDialogueDoneSceneIds);
            state.BranchValues.Clear();
            if (item.branchValues != null)
                for (int i = 0; i < item.branchValues.Length; i++) state.BranchValues.Add(item.branchValues[i]);
            _app.Session.Dispatcher.EnterScene(item.currentSceneId);
            _app.Refresh();
            return true;
        }

        private static void Fill(List<string> target, string[] source)
        {
            target.Clear();
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (!string.IsNullOrEmpty(source[i])) target.Add(source[i]);
        }

        private IEnumerator Shot(string name)
        {
            var path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 4f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log("[ANL] tour screenshot: " + path + " exists=" + File.Exists(path));
            yield return new WaitForSecondsRealtime(0.6f);
        }
    }
}
#endif
