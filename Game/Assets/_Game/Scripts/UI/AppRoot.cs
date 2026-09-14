using System;
using System.Collections;
using System.Collections.Generic;
using ANL.Core.Conditions;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.State;
using ANL.Runtime.Assets;
using ANL.Runtime.Content;
using ANL.Runtime.Diagnostics;
using ANL.Runtime.Persistence;
using ANL.Runtime.Presentation;
using ANL.UI.Screens;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ANL.UI
{
    public sealed class AppRoot : MonoBehaviour
    {
        public UiKit Kit { get; private set; }
        public GameSession Session { get; private set; }
        public IContentIndex Content { get { return Session != null ? Session.Content : null; } }
        public bool TestToolsEnabled { get; private set; }
        public EvidencePanel Evidence { get; private set; }
        public FatalScreen Fatal { get; private set; }
#if ANL_TEST_TOOLS
        public AnlScreen HintsScreen { get { return _hints; } }
        public AnlScreen SettingsScreen { get { return _settings; } }
#endif

        private readonly List<AnlScreen> _screens = new List<AnlScreen>();
        private readonly List<AnlScreen> _modals = new List<AnlScreen>();

        private TitleScreen _title;
        private DialogueScreen _dialogue;
        private HubScreen _hub;
        private InvestigationScreen _investigation;
        private AnlScreen _puzzleHost;
        private ArchiveScreen _archive;
        private HintPanel _hints;
        private SettingsPanel _settings;
        private SaveLoadPanel _saveLoad;
        private MenuPanel _menu;
        private BranchLinesOverlay _branchLines;
        private AnlScreen _dev;

        private GameObject _hud;
        private GameObject _objectiveBox;
        private TextMeshProUGUI _objectiveText;
        private GameObject _toastRoot;
        private TextMeshProUGUI _toastText;
        private Coroutine _toastRoutine;
        private bool _ready;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            TestToolsEnabled = IsTestToolsBuild();
            BuildCanvas();
            LoadEverything();
        }

        private static bool IsTestToolsBuild()
        {
#if ANL_TEST_TOOLS || UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("RootCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var background = CreateLayer(canvasGo.transform, "Background");
            var portrait = CreateLayer(canvasGo.transform, "Portraits");
            var content = CreateLayer(canvasGo.transform, "Content");
            var dialogue = CreateLayer(canvasGo.transform, "Dialogue");
            var hud = CreateLayer(canvasGo.transform, "Hud");
            var modal = CreateLayer(canvasGo.transform, "Modal");
            var dev = CreateLayer(canvasGo.transform, "DevOverlay");

            EnsureEventSystem();
            Kit = new UiKit(background, portrait, content, dialogue, hud, modal, dev, LoadFont());
            UiKit.CoroutineHost = this;
        }

        private static Transform CreateLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            UiKit.Stretch(go);
            return go.transform;
        }

        private static void EnsureEventSystem()
        {
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            EventSystem eventSystem = systems != null && systems.Length > 0 ? systems[0] : null;
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                eventSystem = go.GetComponent<EventSystem>();
            }
            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone != null) UnityEngine.Object.Destroy(standalone);
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private static TMP_FontAsset LoadFont()
        {
            var font = Resources.Load<TMP_FontAsset>("Fonts/NanumGothic SDF");
            if (font != null) return font;
            var legacy = Resources.Load<Font>("Fonts/NanumGothic-Regular");
            if (legacy == null) return null;
            try
            {
                return TMP_FontAsset.CreateFontAsset(legacy);
            }
            catch (Exception exception)
            {
                Diag.Warn("TMP font asset creation failed (headless graphics?): " + exception.Message);
                return null;
            }
        }

        private static string LoadArtManifestSha256()
        {
            try
            {
                var contentCatalog = Resources.Load<ContentCatalog>("ContentCatalog");
                if (contentCatalog != null && !string.IsNullOrEmpty(contentCatalog.artManifestSha256))
                    return contentCatalog.artManifestSha256;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ANL] ContentCatalog lookup failed: " + ex.Message);
            }
            return "";
        }

        private void LoadEverything()
        {
            ContentLoadResult load = null;
            try
            {
                load = ContentRepository.Load();
            }
            catch (Exception ex)
            {
                Diag.Error("[UI] ContentRepository.Load failed: " + ex);
            }
            if (load == null || !load.Ok)
            {
                var errors = load != null && load.Errors != null
                    ? load.Errors
                    : new List<string> { "ContentRepository.Load returned no result" };
                ShowFatal(errors);
                return;
            }

            AssetCatalog catalog = null;
            try
            {
                catalog = Resources.Load<AssetCatalog>("AssetCatalog");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ANL] AssetCatalog load failed: " + ex.Message);
            }
            if (catalog != null)
            {
                try
                {
                    Kit.Assets = new CatalogAssetProvider(catalog);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[ANL] CatalogAssetProvider creation failed: " + ex.Message);
                }
            }

            string contentVersion = load.Index != null && load.Index.Episode != null ? load.Index.Episode.contentVersion : "";
            string artHash = LoadArtManifestSha256();
            var saves = new SaveService(contentVersion, artHash);
            Session = new GameSession(load.Index, saves, contentVersion, artHash);

            var settings = Session.Settings;
            if (settings == null) settings = new SettingsData();
            settings.testToolsEnabled = TestToolsEnabled;
            Session.ApplySettings(settings);

            BuildScreens();
            _ready = true;
            Refresh();
#if ANL_TEST_TOOLS
            if (TourRunner.Requested)
            {
                var tour = gameObject.AddComponent<TourRunner>();
                tour.Init(this);
            }
#endif
        }

        private void BuildScreens()
        {
            _title = new TitleScreen(this);
            _dialogue = new DialogueScreen(this);
            _hub = new HubScreen(this);
            _investigation = new InvestigationScreen(this);
            _puzzleHost = CreateScreen("ANL.UI.Screens.PuzzleHostScreen", () => new FallbackPuzzleHost(this));
            _archive = new ArchiveScreen(this);

            Evidence = new EvidencePanel(this);
            _hints = new HintPanel(this);
            _settings = new SettingsPanel(this);
            _saveLoad = new SaveLoadPanel(this);
            _menu = new MenuPanel(this);
            _branchLines = new BranchLinesOverlay(this);
            _dev = TestToolsEnabled ? CreateScreen("ANL.UI.Screens.DevPanel", () => new FallbackDevPanel(this)) : null;
            Fatal = new FatalScreen(this);
            Fatal.Configure(new List<string>(), RetryContent, UiKit.QuitGame);

            var primary = new AnlScreen[] { _title, _dialogue, _hub, _investigation, _puzzleHost, _archive };
            var modals = new AnlScreen[] { Evidence, _hints, _settings, _saveLoad, _menu, _branchLines, _dev, Fatal };
            for (int i = 0; i < primary.Length; i++)
            {
                if (primary[i].Root == null) primary[i].Build();
                primary[i].SetVisible(false);
                _screens.Add(primary[i]);
            }
            for (int i = 0; i < modals.Length; i++)
            {
                if (modals[i] == null) continue;
                if (modals[i].Root == null) modals[i].Build();
                modals[i].SetVisible(false);
            }
            if (_menu != null) _menu.Saves = _saveLoad;
            if (Fatal != null && !_screens.Contains(Fatal)) _screens.Add(Fatal);

            BuildHud();
            BuildToast();
        }

        public void Refresh()
        {
            if (!_ready || Session == null) return;
            AnlScreen target = ChooseScreen();
            SetScreen(target);
            UpdateHud(target);
        }

        private AnlScreen ChooseScreen()
        {
            if (!string.IsNullOrEmpty(Session.BlockingError)) return Fatal;
            var state = Session.State;
            var scene = Content.Scene(state.CurrentSceneId);
            if (scene == null) return _title;
            if (state.Phase == Phases.PreDialogue || state.Phase == Phases.PostDialogue) return _dialogue;
            if (scene.screenType == "title") return _title;
            if (scene.screenType == "case_archive") return _archive;
            if (state.Phase == Phases.Interaction)
            {
                var puzzle = Content.Puzzle(state.ActivePuzzleId);
                if (puzzle != null && puzzle.type == "hotspot_set") return _investigation;
                return _puzzleHost;
            }
            if (state.Phase == Phases.HubActive) return _hub;
            return _dialogue;
        }

        public void SetScreen(AnlScreen screen)
        {
            if (screen == null) return;
            for (int i = 0; i < _screens.Count; i++)
                if (_screens[i] != screen) _screens[i].SetVisible(false);
            if (screen.Root == null) screen.Build();
            screen.SetVisible(true);
        }

        public void OpenModal(AnlScreen modal)
        {
            if (modal == null) return;
            if (modal.Root == null) modal.Build();
            if (!_modals.Contains(modal)) _modals.Add(modal);
            if (modal.Root != null) modal.Root.transform.SetAsLastSibling();
            modal.SetVisible(true);
        }

        public void CloseModal(AnlScreen modal)
        {
            if (modal == null) return;
            modal.SetVisible(false);
            _modals.Remove(modal);
        }

        public CommandResult Exec(string type, string targetId = null, string[] ids = null, string slot = null, string value = null, double numeric = 0)
        {
            if (Session == null) return CommandResult.Reject("no_session");
            var result = Session.Execute(type, targetId, ids, slot, value, numeric);
            HandleResult(result);
            return result;
        }

        private void HandleResult(CommandResult result)
        {
            if (result == null) return;
            if (result.events != null)
            {
                for (int i = 0; i < result.events.Count; i++)
                {
                    var ev = result.events[i];
                    if (ev == null) continue;
                    switch (ev.kind)
                    {
                        case "branch_dialogue":
                            ShowBranchLines(ev.lines);
                            break;
                        case "scene_entered":
                            break;
                        case "evidence_granted":
                            Toast(UiKit.Ui(Content, "ui.evidence_new", "새 자료") + ": " + EvidenceTitle(ev.id));
                            break;
                        case "puzzle_solved":
                            Toast(string.IsNullOrEmpty(ev.message) ? UiKit.Ui(Content, "ui.solved", "확인됨") : ev.message);
                            break;
                        case "feedback":
                            if (!string.IsNullOrEmpty(ev.message)) Toast(ev.message);
                            break;
                    }
                }
            }
            if (result.status == ResultStatus.Rejected && !string.IsNullOrEmpty(result.message))
                Toast(result.message);
            Refresh();
        }

        private string EvidenceTitle(string evidenceId)
        {
            var def = Content != null ? Content.EvidenceDef(evidenceId) : null;
            return def != null ? def.title : evidenceId;
        }

        public void ShowBranchLines(PressLineDto[] lines, Action onDone = null)
        {
            if (_branchLines == null) return;
            _branchLines.Show(lines, onDone);
            OpenModal(_branchLines);
        }

        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message) || _toastText == null || _toastRoot == null) return;
            _toastText.text = message;
            _toastText.color = Color.white;
            _toastRoot.SetActive(true);
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            float elapsed = 0f;
            const float total = 2.5f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = elapsed > total - 0.6f ? Mathf.Clamp01((total - elapsed) / 0.6f) : 1f;
                if (_toastText != null) _toastText.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            if (_toastRoot != null) _toastRoot.SetActive(false);
            _toastRoutine = null;
        }

        public void OpenHints()
        {
            if (_hints != null) OpenModal(_hints);
        }

        public void OpenMenu()
        {
            if (_menu != null) OpenModal(_menu);
        }

        public void OpenSettings()
        {
            if (_settings != null) OpenModal(_settings);
        }

        public void EnterChildScene(string sceneId)
        {
            if (Session == null || Content == null || string.IsNullOrEmpty(sceneId)) return;
            var hub = Content.Scene(Session.State.CurrentSceneId);
            if (hub == null) return;
            bool listed = ContainsId(hub.childSceneIds, sceneId) || ContainsId(hub.optionalChildSceneIds, sceneId);
            if (!listed) return;
            var child = Content.Scene(sceneId);
            if (child == null) return;
            if (!ConditionEvaluator.AllConditionsMet(child.entryConditions, Session.State, Content)) return;
            Session.Dispatcher.EnterScene(sceneId);
            Refresh();
        }

        private static bool ContainsId(string[] ids, string id)
        {
            if (ids == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == id) return true;
            return false;
        }

        private void BuildHud()
        {
            _hud = new GameObject("Hud", typeof(RectTransform));
            _hud.transform.SetParent(Kit.HudLayer, false);
            UiKit.Stretch(_hud);

            float x = 1890f;
            x = AddHudButton(x, UiKit.Ui(Content, "ui.menu", "메뉴"), OpenMenu);
            x = AddHudButton(x, UiKit.Ui(Content, "ui.hints", "팁"), OpenHints);
            x = AddHudButton(x, UiKit.Ui(Content, "ui.notebook", "수첩"), OpenEvidenceBook);
            x = AddHudButton(x, UiKit.Ui(Content, "ui.evidence", "증거"), OpenEvidenceBook);
            x = AddHudButton(x, UiKit.Ui(Content, "ui.current_objective", "현재 목표"), ToggleObjective);

            _objectiveBox = UiKit.Panel(_hud.transform, "ObjectiveBox", new Color(0.05f, 0.06f, 0.1f, 0.94f));
            UiKit.SetRect(_objectiveBox, 1330f, 86f, 560f, 170f);
            _objectiveText = UiKit.Label(_objectiveBox.transform, "", 24f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_objectiveText.gameObject, 16f, 12f, 528f, 146f);
            _objectiveBox.SetActive(false);
        }

        private float AddHudButton(float right, string label, Action onClick)
        {
            const float width = 152f;
            const float height = 56f;
            const float gap = 10f;
            var button = UiKit.TextButton(_hud.transform, label, 24f, onClick, new Color(0.1f, 0.12f, 0.18f, 0.94f), width, height);
            UiKit.SetRect(button.gameObject, right - width, 18f, width, height);
            return right - width - gap;
        }

        private void OpenEvidenceBook()
        {
            if (Evidence != null) Evidence.ShowBook();
        }

        private void ToggleObjective()
        {
            if (_objectiveBox == null) return;
            _objectiveBox.SetActive(!_objectiveBox.activeSelf);
            if (_objectiveBox.activeSelf) UpdateObjective();
        }

        private void UpdateHud(AnlScreen target)
        {
            if (_hud == null) return;
            bool visible = Session != null && target != null && target != _title && target != Fatal
                && !string.IsNullOrEmpty(Session.State.CurrentSceneId);
            _hud.SetActive(visible);
            if (visible) UpdateObjective();
        }

        private void UpdateObjective()
        {
            if (_objectiveText == null || Content == null || Session == null) return;
            var scene = Content.Scene(Session.State.CurrentSceneId);
            _objectiveText.text = scene != null ? scene.tipText : "";
        }

        private void BuildToast()
        {
            _toastRoot = UiKit.Panel(Kit.DevLayer, "Toast", new Color(0f, 0f, 0f, 0.84f));
            UiKit.SetRect(_toastRoot, 460f, 966f, 1000f, 72f);
            _toastText = UiKit.Label(_toastRoot.transform, "", 26f, Color.white, TextAlignmentOptions.Center, true);
            UiKit.Stretch(_toastText.gameObject);
            _toastText.rectTransform.offsetMin = new Vector2(18f, 8f);
            _toastText.rectTransform.offsetMax = new Vector2(-18f, -8f);
            _toastRoot.SetActive(false);
        }

        private void ShowFatal(List<string> errors)
        {
            if (Fatal == null)
            {
                Fatal = new FatalScreen(this);
                Fatal.Configure(errors, RetryContent, UiKit.QuitGame);
                Fatal.Build();
                if (!_screens.Contains(Fatal)) _screens.Add(Fatal);
            }
            else
            {
                Fatal.Configure(errors, RetryContent, UiKit.QuitGame);
            }
            SetScreen(Fatal);
            var log = "";
            if (errors != null)
                for (int i = 0; i < errors.Count; i++) log += (i > 0 ? " | " : "") + errors[i];
            Diag.Error("[UI] content unavailable: " + log);
        }

        private void RetryContent()
        {
            if (Fatal != null)
            {
                Fatal.SetVisible(false);
                if (Fatal.Root != null) UnityEngine.Object.Destroy(Fatal.Root);
                _screens.Remove(Fatal);
                Fatal = null;
            }
            if (Session != null)
            {
                Refresh();
                return;
            }
            LoadEverything();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Session == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame && _dialogue != null && _dialogue.Visible && _modals.Count == 0)
            {
                var phase = Session.State.Phase;
                if (phase == Phases.PreDialogue || phase == Phases.PostDialogue)
                    Exec(CommandKinds.AdvanceDialogue);
            }

            if (keyboard.escapeKey.wasPressedThisFrame && _modals.Count > 0)
                CloseModal(_modals[_modals.Count - 1]);

            if (keyboard.f1Key.wasPressedThisFrame || keyboard.backquoteKey.wasPressedThisFrame)
                OpenHints();

            if (_hints != null && _hints.Visible)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) _hints.RevealLevel(1);
                if (keyboard.digit2Key.wasPressedThisFrame) _hints.RevealLevel(2);
                if (keyboard.digit3Key.wasPressedThisFrame) _hints.RevealLevel(3);
            }

            if (TestToolsEnabled && _dev != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) && keyboard.dKey.wasPressedThisFrame)
            {
                if (_dev.Visible) CloseModal(_dev);
                else OpenModal(_dev);
            }
        }

        private AnlScreen CreateScreen(string typeName, Func<AnlScreen> fallback)
        {
            var type = typeof(AppRoot).Assembly.GetType(typeName);
            if (type != null)
            {
                var ctor = type.GetConstructor(new[] { typeof(AppRoot) });
                if (ctor != null)
                {
                    var created = ctor.Invoke(new object[] { this }) as AnlScreen;
                    if (created != null) return created;
                }
            }
            if (fallback != null) return fallback();
            return null;
        }

        private sealed class FallbackPuzzleHost : AnlScreen
        {
            private GameObject _background;
            private TextMeshProUGUI _title;
            private TextMeshProUGUI _info;
            private Button _advance;

            public FallbackPuzzleHost(AppRoot app) : base(app)
            {
            }

            public override void Build()
            {
                Root = UiKit.Panel(Kit.ContentLayer, "PuzzleHostScreen", new Color(0.02f, 0.03f, 0.05f, 1f));
                UiKit.Stretch(Root);
                _background = UiKit.Panel(Root.transform, "Background", Color.white);
                UiKit.Stretch(_background);
                _background.GetComponent<Image>().raycastTarget = false;
                var header = UiKit.Panel(Root.transform, "Header", new Color(0.04f, 0.05f, 0.08f, 0.94f));
                UiKit.SetRect(header, 40f, 40f, 1840f, 150f);
                _title = UiKit.Label(header.transform, "", 36f, Color.white, TextAlignmentOptions.TopLeft);
                UiKit.SetRect(_title.gameObject, 24f, 16f, 1200f, 50f);
                _info = UiKit.Label(header.transform, "", 24f, new Color(0.82f, 0.86f, 0.94f, 1f), TextAlignmentOptions.TopLeft, true);
                UiKit.SetRect(_info.gameObject, 24f, 70f, 1780f, 64f);
                _advance = UiKit.TextButton(Root.transform, UiKit.Ui(App.Content, "ui.continue_scene", "계속"), 26f,
                    () => App.Exec(CommandKinds.ContinueScene), new Color(0.16f, 0.3f, 0.22f, 0.96f), 220f, 60f);
                UiKit.SetRect(_advance.gameObject, 1660f, 960f, 220f, 60f);
                _advance.gameObject.SetActive(false);
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
                var puzzle = App.Content.Puzzle(state.ActivePuzzleId);
                _title.text = scene.id + (puzzle != null ? " · " + puzzle.id : "");
                _info.text = puzzle != null
                    ? puzzle.type + "  ·  " + UiKit.Ui(App.Content, "ui.interaction_hint", "필요한 조작을 완료하면 진행됩니다.") + "\n" + scene.tipText
                    : UiKit.Ui(App.Content, "ui.no_active_puzzle", "진행 중인 문제가 없습니다.");
                bool canAdvance = state.Phase == Phases.ReadyToExit || state.Phase == Phases.Completed;
                _advance.gameObject.SetActive(canAdvance);
                if (canAdvance) _advance.interactable = App.Session.Dispatcher.TransitionAvailable(out _, out _);
            }
        }

        private sealed class FallbackDevPanel : AnlScreen
        {
            private TextMeshProUGUI _body;
            private CommandResult _last;

            public FallbackDevPanel(AppRoot app) : base(app)
            {
                IsModal = true;
            }

            public override void Build()
            {
                Root = UiKit.Panel(Kit.DevLayer, "DevPanel", new Color(0.02f, 0.03f, 0.05f, 0.96f));
                UiKit.SetRect(Root, 20f, 20f, 620f, 420f);
                var title = UiKit.Label(Root.transform, UiKit.Ui(App.Content, "ui.dev_panel", "개발 패널"), 28f, new Color(0.6f, 1f, 0.7f, 1f));
                UiKit.SetRect(title.gameObject, 18f, 12f, 400f, 40f);
                var close = UiKit.TextButton(Root.transform, UiKit.Ui(App.Content, "ui.close", "닫기"), 20f, () => App.CloseModal(this), null, 110f, 44f);
                UiKit.SetRect(close.gameObject, 490f, 12f, 110f, 44f);
                _body = UiKit.Label(Root.transform, "", 20f, Color.white, TextAlignmentOptions.TopLeft, true);
                UiKit.SetRect(_body.gameObject, 18f, 66f, 584f, 340f);
                if (App.Session != null) App.Session.Executed += OnExecuted;
            }

            private void OnExecuted(CommandResult result)
            {
                _last = result;
                if (Visible) Refresh();
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
                    UiKit.Ui(App.Content, "ui.dev_last_result", "최근 명령") + ": " + lastText + "\n" +
                    UiKit.Ui(App.Content, "ui.dev_blocker", "블로커") + ": " + (string.IsNullOrEmpty(App.Session.BlockingError) ? "-" : App.Session.BlockingError);
            }
        }
    }
}
