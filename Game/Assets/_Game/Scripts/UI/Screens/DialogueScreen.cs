using System.Collections;
using System.Collections.Generic;
using ANL.Core.Conditions;
using ANL.Core.Contracts;
using ANL.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class DialogueScreen : AnlScreen
    {
        private GameObject _background;
        private GameObject _portrait;
        private Image _portraitImage;
        private TextMeshProUGUI _objective;
        private GameObject _dialogueGroup;
        private GameObject _box;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _body;
        private GameObject _optionsGroup;
        private RectTransform _optionsList;
        private Button _branchContinue;
        private GameObject _readGroup;
        private RectTransform _readList;
        private Button _readContinue;
        private GameObject _summaryGroup;
        private TextMeshProUGUI _summaryBadge;
        private TextMeshProUGUI _summaryTitle;
        private TextMeshProUGUI _summaryReplayText;
        private Button _summaryContinue;
        private Button _summaryHub;
        private Button _summaryReplay;
        private Coroutine _revealRoutine;
        private Coroutine _autoRoutine;
        private bool _revealing;
        private string _lineKey = "";
        private int _replayIndex = -1;

        public DialogueScreen(AppRoot app) : base(app)
        {
        }

        public override void Build()
        {
            Root = UiKit.Panel(Kit.DialogueLayer, "DialogueScreen", new Color(0.02f, 0.03f, 0.05f, 1f));
            UiKit.Stretch(Root);

            _background = UiKit.Panel(Root.transform, "Background", Color.white);
            UiKit.Stretch(_background);
            _background.GetComponent<Image>().raycastTarget = false;

            _portrait = UiKit.Panel(Root.transform, "Portrait", new Color(1f, 1f, 1f, 0f));
            _portraitImage = _portrait.GetComponent<Image>();
            _portraitImage.raycastTarget = false;

            _objective = UiKit.Label(Root.transform, "", 22f, new Color(0.8f, 0.84f, 0.92f, 1f), TextAlignmentOptions.TopLeft);
            UiKit.SetRect(_objective.gameObject, 40f, 28f, 900f, 40f);

            BuildDialogueBox();
            BuildOptions();
            BuildReadCheck();
            BuildSummary();
        }

        private void BuildDialogueBox()
        {
            _dialogueGroup = new GameObject("DialogueGroup", typeof(RectTransform));
            _dialogueGroup.transform.SetParent(Root.transform, false);
            UiKit.Stretch(_dialogueGroup);

            _box = UiKit.Panel(_dialogueGroup.transform, "DialogueBox", new Color(0.02f, 0.03f, 0.05f, 0.9f));
            UiKit.SetRect(_box, 40f, 760f, 1840f, 280f);
            UiKit.HitArea(_box, OnNextClick);

            _speaker = UiKit.Label(_box.transform, "", 30f, new Color(0.95f, 0.8f, 0.45f, 1f), TextAlignmentOptions.TopLeft);
            UiKit.SetRect(_speaker.gameObject, 24f, 12f, 900f, 44f);

            _body = UiKit.Label(_box.transform, "", 30f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_body.gameObject, 24f, 62f, 1560f, 168f);

            var next = UiKit.TextButton(_box.transform, UiKit.Ui(App.Content, "ui.next", "다음"), 22f, OnNextClick, null, 130f, 50f);
            UiKit.SetRect(next.gameObject, 1680f, 214f, 130f, 50f);

            var evidence = UiKit.TextButton(_box.transform, UiKit.Ui(App.Content, "ui.evidence", "증거"), 22f,
                () => App.Evidence.ShowBook(), null, 130f, 50f);
            UiKit.SetRect(evidence.gameObject, 1680f, 12f, 130f, 50f);
        }

        private void BuildOptions()
        {
            _optionsGroup = UiKit.Panel(Root.transform, "OptionsGroup", new Color(0f, 0f, 0f, 0f));
            UiKit.SetRect(_optionsGroup, 360f, 320f, 1200f, 480f);
            var title = UiKit.Label(_optionsGroup.transform, UiKit.Ui(App.Content, "ui.statement_select", "증언을 선택하십시오."), 24f, new Color(0.9f, 0.92f, 1f, 1f));
            UiKit.SetRect(title.gameObject, 0f, 0f, 900f, 40f);

            RectTransform content;
            var scroll = UiKit.Scroll(_optionsGroup.transform, "Options", out content);
            UiKit.SetRect(scroll.gameObject, 0f, 50f, 1200f, 330f);
            _optionsList = content;

            var evidence = UiKit.TextButton(_optionsGroup.transform, UiKit.Ui(App.Content, "ui.evidence", "증거"), 24f,
                () => App.Evidence.ShowBook(), null, 200f, 56f);
            UiKit.SetRect(evidence.gameObject, 0f, 396f, 200f, 56f);

            _branchContinue = UiKit.TextButton(_optionsGroup.transform, UiKit.Ui(App.Content, "ui.continue_scene", "계속"), 24f,
                () => App.Exec(CommandKinds.ContinueScene), new Color(0.16f, 0.3f, 0.22f, 0.96f), 200f, 56f);
            UiKit.SetRect(_branchContinue.gameObject, 1000f, 396f, 200f, 56f);
            _branchContinue.gameObject.SetActive(false);
        }

        private void BuildReadCheck()
        {
            _readGroup = UiKit.Panel(Root.transform, "ReadGroup", new Color(0.03f, 0.04f, 0.06f, 0.96f));
            UiKit.SetRect(_readGroup, 360f, 260f, 1200f, 560f);
            var title = UiKit.Label(_readGroup.transform, UiKit.Ui(App.Content, "ui.notebook", "수첩"), 30f, Color.white);
            UiKit.SetRect(title.gameObject, 24f, 20f, 600f, 44f);

            RectTransform content;
            var scroll = UiKit.Scroll(_readGroup.transform, "ReadList", out content);
            UiKit.SetRect(scroll.gameObject, 24f, 80f, 1152f, 390f);
            _readList = content;

            _readContinue = UiKit.TextButton(_readGroup.transform, UiKit.Ui(App.Content, "ui.continue_scene", "계속"), 26f,
                OnReadContinue, new Color(0.16f, 0.3f, 0.22f, 0.96f), 220f, 60f);
            UiKit.SetRect(_readContinue.gameObject, 956f, 482f, 220f, 60f);
        }

        private void BuildSummary()
        {
            _summaryGroup = UiKit.Panel(Root.transform, "SummaryGroup", new Color(0.03f, 0.04f, 0.06f, 0.97f));
            UiKit.SetRect(_summaryGroup, 560f, 260f, 800f, 540f);
            _summaryBadge = UiKit.Label(_summaryGroup.transform, "", 28f, new Color(0.5f, 0.95f, 0.6f, 1f));
            UiKit.SetRect(_summaryBadge.gameObject, 28f, 24f, 300f, 44f);
            _summaryTitle = UiKit.Label(_summaryGroup.transform, "", 26f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_summaryTitle.gameObject, 28f, 80f, 744f, 240f);
            _summaryReplayText = UiKit.Label(_summaryGroup.transform, "", 23f, new Color(0.85f, 0.88f, 0.95f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_summaryReplayText.gameObject, 28f, 300f, 744f, 120f);

            _summaryContinue = UiKit.TextButton(_summaryGroup.transform, UiKit.Ui(App.Content, "ui.continue_scene", "계속"), 25f,
                OnSummaryContinue, new Color(0.16f, 0.3f, 0.22f, 0.96f), 200f, 58f);
            UiKit.SetRect(_summaryContinue.gameObject, 28f, 452f, 200f, 58f);

            _summaryHub = UiKit.TextButton(_summaryGroup.transform, UiKit.Ui(App.Content, "ui.return_to_hub", "조사 허브로"), 25f,
                () => App.Exec(CommandKinds.ReturnToHub), null, 220f, 58f);
            UiKit.SetRect(_summaryHub.gameObject, 244f, 452f, 220f, 58f);

            _summaryReplay = UiKit.TextButton(_summaryGroup.transform, UiKit.Ui(App.Content, "ui.stop_to_think", "기록을 다시 본다"), 25f,
                ReplayLines, null, 260f, 58f);
            UiKit.SetRect(_summaryReplay.gameObject, 480f, 452f, 260f, 58f);
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            var state = App.Session.State;
            var scene = App.Content.Scene(state.CurrentSceneId);
            if (scene == null) return;

            var image = _background.GetComponent<Image>();
            image.sprite = Kit.Sprite(scene.backgroundAssetId);
            image.color = image.sprite != null ? Color.white : new Color(0.06f, 0.07f, 0.11f, 1f);
            UiKit.Cover(_background, 1920f, 1080f);
            _objective.text = "Act " + scene.act + " · " + scene.id;

            bool dialogue = false;
            bool options = false;
            bool read = false;
            bool summary = false;
            switch (state.Phase)
            {
                case Phases.PreDialogue:
                    dialogue = ShowDialogue(scene, "pre", state);
                    if (!dialogue) { ShowSummary(scene, state); summary = true; }
                    break;
                case Phases.PostDialogue:
                    dialogue = ShowDialogue(scene, "post", state);
                    if (!dialogue) { ShowSummary(scene, state); summary = true; }
                    break;
                case Phases.BranchChoice:
                    ShowOptions(scene, state);
                    options = true;
                    break;
                case Phases.ReadCheck:
                    ShowRead(scene, state);
                    read = true;
                    break;
                default:
                    ShowSummary(scene, state);
                    summary = true;
                    break;
            }
            _dialogueGroup.SetActive(dialogue);
            _optionsGroup.SetActive(options);
            _readGroup.SetActive(read);
            _summaryGroup.SetActive(summary);
            _portrait.SetActive(dialogue || options || read);
            ApplyTextScale();
            if (!dialogue && _revealRoutine != null) CompleteReveal();
        }

        private void ApplyTextScale()
        {
            float scale = 1f;
            var settings = App.Session != null ? App.Session.Settings : null;
            if (settings != null && settings.textScale > 0f) scale = settings.textScale;
            if (_body != null) _body.fontSize = 30f * scale;
            if (_speaker != null) _speaker.fontSize = 30f * scale;
        }

        private bool ShowDialogue(SceneDefinitionDto scene, string phase, GameState state)
        {
            var lines = App.Content.SceneDialogue(scene.id, phase);
            if (lines.Count == 0) return false;
            int index = Mathf.Clamp(state.DialogueCursor, 0, lines.Count - 1);
            ShowLine(lines[index], scene.id, phase, state.DialogueCursor);
            return true;
        }

        private void ShowLine(DialogueLineDto line, string sceneId, string phase, int cursor)
        {
            string key = sceneId + "|" + phase + "|" + cursor;
            if (key == _lineKey) return;
            _lineKey = key;

            var text = !string.IsNullOrEmpty(line.text) ? line.text : App.Content.TextOf(line.textId);
            _speaker.text = line.speaker;

            UpdatePortrait(line.portraitAssetId);

            var settings = App.Session.Settings;
            bool instant = settings != null && (settings.instantText || settings.reduceMotion);
            if (_revealRoutine != null)
            {
                UiKit.CoroutineHost.StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }
            if (_autoRoutine != null)
            {
                UiKit.CoroutineHost.StopCoroutine(_autoRoutine);
                _autoRoutine = null;
            }
            _body.text = text;
            _body.maxVisibleCharacters = int.MaxValue;
            _revealing = false;
            if (!instant && !string.IsNullOrEmpty(text))
            {
                _body.ForceMeshUpdate();
                _body.maxVisibleCharacters = 0;
                _revealRoutine = UiKit.CoroutineHost.StartCoroutine(RevealRoutine());
            }
            if (settings != null && settings.autoAdvance)
                _autoRoutine = UiKit.CoroutineHost.StartCoroutine(AutoAdvanceRoutine(key));
        }

        private IEnumerator AutoAdvanceRoutine(string key)
        {
            while (_revealing && _lineKey == key) yield return null;
            yield return new WaitForSecondsRealtime(1.8f);
            _autoRoutine = null;
            if (_lineKey != key || !Visible) yield break;
            var settings = App.Session != null ? App.Session.Settings : null;
            if (settings == null || !settings.autoAdvance) yield break;
            App.Exec(CommandKinds.AdvanceDialogue);
        }

        private IEnumerator RevealRoutine()
        {
            _revealing = true;
            int total = _body.textInfo.characterCount;
            float shown = 0f;
            while (shown < total && _revealing)
            {
                shown += UiKit.CharsPerSecond * Time.unscaledDeltaTime;
                _body.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            _body.maxVisibleCharacters = int.MaxValue;
            _revealing = false;
            _revealRoutine = null;
        }

        private void OnNextClick()
        {
            if (_revealing)
            {
                CompleteReveal();
                return;
            }
            App.Exec(CommandKinds.AdvanceDialogue);
        }

        private void CompleteReveal()
        {
            if (_revealRoutine != null)
            {
                UiKit.CoroutineHost.StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }
            _revealing = false;
            if (_body != null) _body.maxVisibleCharacters = int.MaxValue;
        }

        private void UpdatePortrait(string assetId)
        {
            var sprite = string.IsNullOrEmpty(assetId) ? null : Kit.Sprite(assetId);
            var entry = string.IsNullOrEmpty(assetId) ? null : Kit.Entry(assetId);
            _portraitImage.sprite = sprite;
            _portraitImage.enabled = sprite != null;
            if (sprite == null) return;

            var displayed = InvestigationScreen.ComputeContainRect(new Vector2(900f, 820f), sprite.rect.size);
            var rect = _portrait.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            float anchorX = entry != null ? Mathf.Clamp01(entry.anchorX) : 0.5f;
            float anchorY = entry != null ? Mathf.Clamp01(entry.anchorY) : 1f;
            rect.pivot = new Vector2(anchorX, 1f - anchorY);
            rect.sizeDelta = new Vector2(displayed.width, displayed.height);
            rect.anchoredPosition = new Vector2(380f, 56f);
        }

        private void ShowOptions(SceneDefinitionDto scene, GameState state)
        {
            UiKit.ClearChildren(_optionsList);
            if (scene.branchGroupIds != null)
            {
                for (int g = 0; g < scene.branchGroupIds.Length; g++)
                {
                    var group = App.Content.BranchGroup(scene.branchGroupIds[g]);
                    for (int i = 0; i < group.Count; i++)
                    {
                        var branch = group[i];
                        if (state.IsTopicVisited(branch.id)) continue;
                        if (!ConditionEvaluator.AllConditionsMet(branch.condition, state, App.Content)) continue;
                        var id = branch.id;
                        var button = UiKit.TextButton(_optionsList, branch.label, 26f,
                            () => App.Exec(CommandKinds.ChooseBranch, id), new Color(0.12f, 0.15f, 0.22f, 0.98f), 1100f, 62f);
                        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
                    }
                }
            }
            if (_branchContinue != null)
                _branchContinue.gameObject.SetActive(CanLeaveBranch(scene, state));
        }

        private bool CanLeaveBranch(SceneDefinitionDto scene, GameState state)
        {
            if (scene.advancingGroupIds != null && scene.advancingGroupIds.Length > 0)
            {
                for (int g = 0; g < scene.advancingGroupIds.Length; g++)
                {
                    var group = App.Content.BranchGroup(scene.advancingGroupIds[g]);
                    bool resolved = false;
                    for (int i = 0; i < group.Count; i++)
                    {
                        if (state.IsTopicVisited(group[i].id)) { resolved = true; break; }
                    }
                    if (!resolved) return false;
                }
                return true;
            }
            if (scene.branchGroupIds != null)
            {
                for (int g = 0; g < scene.branchGroupIds.Length; g++)
                {
                    var group = App.Content.BranchGroup(scene.branchGroupIds[g]);
                    for (int i = 0; i < group.Count; i++)
                    {
                        if (!ConditionEvaluator.AllConditionsMet(group[i].condition, state, App.Content)) continue;
                        if (!state.IsTopicVisited(group[i].id)) return false;
                    }
                }
            }
            return true;
        }

        private void ShowRead(SceneDefinitionDto scene, GameState state)
        {
            UiKit.ClearChildren(_readList);
            var ids = scene.requiredReadEvidenceIds;
            if (ids != null)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    var evidence = App.Content.EvidenceDef(ids[i]);
                    if (evidence == null) continue;
                    bool read = state.HasRead(ids[i]);


                    var row = UiKit.Panel(_readList, "Evidence", new Color(0.1f, 0.12f, 0.18f, 0.96f));
                    row.AddComponent<LayoutElement>().preferredHeight = 84f;
                    var title = UiKit.Label(row.transform, evidence.title, 25f, Color.white);
                    UiKit.SetRect(title.gameObject, 16f, 10f, 700f, 36f);
                    var status = UiKit.Label(row.transform,
                        read ? UiKit.Ui(App.Content, "ui.evidence_read", "열람 완료") : UiKit.Ui(App.Content, "ui.evidence_unread", "미열람"),
                        21f, read ? new Color(0.55f, 0.9f, 0.6f, 1f) : new Color(1f, 0.75f, 0.5f, 1f));
                    UiKit.SetRect(status.gameObject, 16f, 48f, 300f, 30f);
                    var id = ids[i];
                    var open = UiKit.TextButton(row.transform, UiKit.Ui(App.Content, "ui.evidence", "증거"), 22f,
                        () => App.Exec(CommandKinds.OpenEvidence, id), null, 160f, 54f);
                    UiKit.SetRect(open.gameObject, 960f, 15f, 160f, 54f);
                }
            }
            _readContinue.interactable = true;
        }

        private void OnReadContinue()
        {
            var scene = App.Content.Scene(App.Session.State.CurrentSceneId);
            var ids = scene != null ? scene.requiredReadEvidenceIds : null;
            if (ids != null)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    if (App.Session.State.HasRead(ids[i])) continue;
                    var evidence = App.Content.EvidenceDef(ids[i]);
                    App.Toast(UiKit.Ui(App.Content, "ui.evidence_unread", "미열람") + ": " + (evidence != null ? evidence.title : ids[i]));
                    return;
                }
            }
            App.Exec(CommandKinds.ContinueScene);
        }

        private string FindParentHub(string sceneId)
        {
            foreach (var pair in App.Content.Scenes)
            {
                var children = pair.Value.childSceneIds;
                if (children == null) continue;
                for (int i = 0; i < children.Length; i++)
                    if (children[i] == sceneId) return pair.Key;
            }
            return "";
        }

        private void ShowSummary(SceneDefinitionDto scene, GameState state)
        {
            bool completed = state.IsSceneCompleted(scene.id);
            _summaryBadge.text = completed ? UiKit.Ui(App.Content, "ui.hub_done", "완료") : "";
            _summaryTitle.text = scene.id + " · Act " + scene.act + "\n\n" + scene.tipText;
            _summaryReplayText.text = "";
            _replayIndex = -1;

            string hub = FindParentHub(scene.id);
            _summaryHub.gameObject.SetActive(!string.IsNullOrEmpty(hub));

            bool canReplay = completed && App.Content.SceneDialogue(scene.id, "pre").Count > 0;
            _summaryReplay.gameObject.SetActive(canReplay);
        }

        private void ReplayLines()
        {
            if (App.Session == null || App.Content == null) return;
            var scene = App.Content.Scene(App.Session.State.CurrentSceneId);
            if (scene == null) return;
            var lines = App.Content.SceneDialogue(scene.id, "pre");
            if (lines.Count == 0) return;
            _replayIndex = (_replayIndex + 1) % lines.Count;
            var line = lines[_replayIndex];
            _summaryReplayText.text = line.speaker + ": " + (!string.IsNullOrEmpty(line.text) ? line.text : App.Content.TextOf(line.textId));
        }

        private void OnSummaryContinue()
        {
            if (App.Session == null || App.Content == null) return;
            var scene = App.Content.Scene(App.Session.State.CurrentSceneId);
            if (scene != null && scene.screenType == "hub" && App.Session.State.Phase == Phases.ReadyToExit)
            {
                App.Session.Dispatcher.EnterScene(scene.id);
                App.Refresh();
                return;
            }
            App.Exec(CommandKinds.ContinueScene);
        }
    }
}
