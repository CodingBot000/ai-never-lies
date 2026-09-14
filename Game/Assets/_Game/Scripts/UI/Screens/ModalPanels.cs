using System;
using System.Collections.Generic;
using ANL.Core.Commands;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.Hints;
using ANL.Core.State;
using ANL.Runtime.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class EvidencePanel : AnlScreen
    {
        private GameObject _bookView;
        private GameObject _pickerView;
        private RectTransform _bookList;
        private RectTransform _pickerList;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private TextMeshProUGUI _detailMeta;
        private TextMeshProUGUI _emptyDetail;
        private GameObject _imageHolder;
        private Image _detailImage;
        private TextMeshProUGUI _pickerCounter;
        private Button _pickerConfirm;
        private TextMeshProUGUI _pickerTitle;
        private string _selectedId = "";
        private string _filterKind = "";
        private bool _recentFirst;
        private int _tab;
        private readonly List<string> _pickerSelected = new List<string>();
        private int _pickerMax = 3;
        private Action<List<string>> _pickerOnConfirm;
        private Func<string, bool> _pickerFilter;
        private bool _pickerMode;

        public EvidencePanel(AppRoot app) : base(app)
        {
            IsModal = true;
        }

        public override void Build()
        {
            Root = UiKit.Dim(Kit.ModalLayer, "EvidencePanel");
            var window = UiKit.Panel(Root.transform, "Window", new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UiKit.SetRect(window, 160f, 80f, 1600f, 920f);

            var header = UiKit.Panel(window.transform, "Header", new Color(0.08f, 0.1f, 0.14f, 1f));
            UiKit.SetRect(header, 0f, 0f, 1600f, 84f);
            var title = UiKit.Label(header.transform, UiKit.Ui(App.Content, "ui.evidence_book", "증거책"), 34f, Color.white, TextAlignmentOptions.Left);
            UiKit.SetRect(title.gameObject, 24f, 18f, 900f, 50f);
            var close = UiKit.TextButton(header.transform, UiKit.Ui(App.Content, "ui.close", "닫기"), 24f, () => App.CloseModal(this), null, 140f, 52f);
            UiKit.SetRect(close.gameObject, 1436f, 16f, 140f, 52f);

            BuildBook(window.transform);
            BuildPicker(window.transform);
        }

        private void BuildBook(Transform parent)
        {
            _bookView = new GameObject("BookView", typeof(RectTransform));
            _bookView.transform.SetParent(parent, false);
            UiKit.Stretch(_bookView);

            var filters = UiKit.Panel(_bookView.transform, "Filters", new Color(0f, 0f, 0f, 0f));
            UiKit.SetRect(filters, 24f, 100f, 560f, 48f);
            float x = 0f;
            x = FilterButton(filters.transform, x, UiKit.Ui(App.Content, "ui.filter_all", "전체"), "");
            x = FilterButton(filters.transform, x, UiKit.Ui(App.Content, "ui.evidence_kind_document", "문서"), "document");
            x = FilterButton(filters.transform, x, UiKit.Ui(App.Content, "ui.evidence_kind_video", "영상 기록"), "video");
            x = FilterButton(filters.transform, x, UiKit.Ui(App.Content, "ui.evidence_kind_log", "로그"), "log");
            x = FilterButton(filters.transform, x, UiKit.Ui(App.Content, "ui.evidence_kind_personal", "개인 자료"), "personal");

            var recent = UiKit.TextButton(_bookView.transform, "최근 획득", 22f, ToggleRecent, new Color(0.12f, 0.14f, 0.2f, 0.96f), 150f, 44f);
            UiKit.SetRect(recent.gameObject, 434f, 158f, 150f, 44f);

            RectTransform content;
            var scroll = UiKit.Scroll(_bookView.transform, "BookList", out content);
            UiKit.SetRect(scroll.gameObject, 24f, 216f, 560f, 680f);
            _bookList = content;

            var detail = UiKit.Panel(_bookView.transform, "Detail", new Color(0.07f, 0.08f, 0.12f, 1f));
            UiKit.SetRect(detail, 608f, 100f, 968f, 796f);
            _detailTitle = UiKit.Label(detail.transform, "", 32f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_detailTitle.gameObject, 24f, 16f, 920f, 52f);

            var tabSummary = UiKit.TextButton(detail.transform, UiKit.Ui(App.Content, "ui.evidence_summary", "요약"), 22f, () => SetTab(0), new Color(0.14f, 0.16f, 0.22f, 0.96f), 140f, 44f);
            UiKit.SetRect(tabSummary.gameObject, 24f, 74f, 140f, 44f);
            var tabOriginal = UiKit.TextButton(detail.transform, UiKit.Ui(App.Content, "ui.evidence_original", "원문"), 22f, () => SetTab(1), new Color(0.14f, 0.16f, 0.22f, 0.96f), 140f, 44f);
            UiKit.SetRect(tabOriginal.gameObject, 176f, 74f, 140f, 44f);

            _detailBody = UiKit.Label(detail.transform, "", 24f, new Color(0.92f, 0.94f, 1f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_detailBody.gameObject, 24f, 132f, 920f, 280f);

            _imageHolder = UiKit.Panel(detail.transform, "ImageHolder", new Color(0.03f, 0.04f, 0.06f, 1f));
            UiKit.SetRect(_imageHolder, 24f, 424f, 920f, 240f);
            var imageGo = UiKit.Panel(_imageHolder.transform, "Image", Color.white);
            _detailImage = imageGo.GetComponent<Image>();
            _detailImage.raycastTarget = false;

            _detailMeta = UiKit.Label(detail.transform, "", 22f, new Color(0.85f, 0.88f, 0.95f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_detailMeta.gameObject, 24f, 676f, 920f, 108f);

            _emptyDetail = UiKit.Label(detail.transform, UiKit.Ui(App.Content, "ui.evidence_owned_only", "보유한 자료만 표시됩니다."), 24f, new Color(0.7f, 0.72f, 0.78f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_emptyDetail.gameObject, 24f, 132f, 920f, 200f);
        }

        private float FilterButton(Transform parent, float x, string label, string kind)
        {
            var button = UiKit.TextButton(parent, label, 20f, () => SetFilter(kind), new Color(0.12f, 0.14f, 0.2f, 0.96f), 104f, 44f);
            UiKit.SetRect(button.gameObject, x, 0f, 104f, 44f);
            return x + 110f;
        }

        private void BuildPicker(Transform parent)
        {
            _pickerView = new GameObject("PickerView", typeof(RectTransform));
            _pickerView.transform.SetParent(parent, false);
            UiKit.Stretch(_pickerView);
            _pickerView.SetActive(false);

            _pickerTitle = UiKit.Label(_pickerView.transform, "", 30f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_pickerTitle.gameObject, 24f, 104f, 1200f, 50f);
            _pickerCounter = UiKit.Label(_pickerView.transform, "", 24f, new Color(0.9f, 0.85f, 0.5f, 1f), TextAlignmentOptions.TopRight);
            UiKit.SetRect(_pickerCounter.gameObject, 1200f, 108f, 340f, 44f);

            RectTransform content;
            var scroll = UiKit.Scroll(_pickerView.transform, "PickerList", out content);
            UiKit.SetRect(scroll.gameObject, 24f, 166f, 1516f, 610f);
            _pickerList = content;

            _pickerConfirm = UiKit.TextButton(_pickerView.transform, UiKit.Ui(App.Content, "ui.confirm", "확인"), 26f, ConfirmPicker, new Color(0.16f, 0.3f, 0.22f, 0.96f), 200f, 60f);
            UiKit.SetRect(_pickerConfirm.gameObject, 1100f, 800f, 200f, 60f);
            var cancel = UiKit.TextButton(_pickerView.transform, UiKit.Ui(App.Content, "ui.cancel", "취소"), 26f, CancelPicker, null, 200f, 60f);
            UiKit.SetRect(cancel.gameObject, 1320f, 800f, 200f, 60f);
        }

        public void ShowBook()
        {
            _pickerMode = false;
            _bookView.SetActive(true);
            _pickerView.SetActive(false);
            App.OpenModal(this);
        }

        public void ShowPicker(string title, int maxSelect, List<string> preselected,
            Action<List<string>> onConfirm, Func<string, bool> filter = null)
        {
            _pickerMode = true;
            _pickerMax = Mathf.Max(1, maxSelect);
            _pickerOnConfirm = onConfirm;
            _pickerFilter = filter;
            _pickerSelected.Clear();
            if (preselected != null)
            {
                for (int i = 0; i < preselected.Count && i < _pickerMax; i++)
                    if (!string.IsNullOrEmpty(preselected[i]) && !_pickerSelected.Contains(preselected[i]))
                        _pickerSelected.Add(preselected[i]);
            }
            if (_pickerTitle != null) _pickerTitle.text = title;
            _bookView.SetActive(false);
            _pickerView.SetActive(true);
            App.OpenModal(this);
        }

        private void SetTab(int tab)
        {
            _tab = tab;
            RefreshDetail();
        }

        private void SetFilter(string kind)
        {
            _filterKind = kind;
            Refresh();
        }

        private void ToggleRecent()
        {
            _recentFirst = !_recentFirst;
            Refresh();
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            if (_pickerMode) RefreshPicker();
            else RefreshBook();
        }

        private void RefreshBook()
        {
            var state = App.Session.State;
            UiKit.ClearChildren(_bookList);
            var ids = FilteredOwned();
            if (ids.Count == 0)
            {
                var empty = UiKit.Label(_bookList, UiKit.Ui(App.Content, "ui.evidence_none", "아직 보유한 증거가 없습니다."), 24f, new Color(0.75f, 0.78f, 0.85f, 1f));
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;
            }
            else
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    var def = App.Content.EvidenceDef(ids[i]);
                    if (def == null) continue;
                    BuildBookRow(def, state.HasRead(def.id), def.id == _selectedId);
                }
            }
            if (string.IsNullOrEmpty(_selectedId) || !Contains(ids, _selectedId))
                _selectedId = ids.Count > 0 ? ids[0] : "";
            RefreshDetail();
        }

        private List<string> FilteredOwned()
        {
            var owned = App.Session.State.OwnedEvidence;
            var result = new List<string>();
            if (_recentFirst)
            {
                for (int i = owned.Count - 1; i >= 0; i--) AddFiltered(result, owned[i]);
            }
            else
            {
                for (int i = 0; i < owned.Count; i++) AddFiltered(result, owned[i]);
            }
            return result;
        }

        private void AddFiltered(List<string> result, string id)
        {
            if (string.IsNullOrEmpty(_filterKind)) { result.Add(id); return; }
            var def = App.Content.EvidenceDef(id);
            if (def != null && def.kind == _filterKind) result.Add(id);
        }

        private static bool Contains(List<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++) if (ids[i] == id) return true;
            return false;
        }

        private void BuildBookRow(EvidenceDefinitionDto def, bool read, bool selected)
        {
            var row = UiKit.Panel(_bookList, "Evidence", selected ? new Color(0.2f, 0.26f, 0.38f, 1f) : new Color(0.1f, 0.12f, 0.18f, 0.96f));
            row.AddComponent<LayoutElement>().preferredHeight = 84f;
            var id = def.id;
            UiKit.HitArea(row, () => SelectEvidence(id));

            var title = UiKit.Label(row.transform, def.title, 25f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(title.gameObject, 16f, 8f, 400f, 34f);
            var kind = UiKit.Label(row.transform, KindLabel(def.kind), 20f, new Color(0.68f, 0.72f, 0.82f, 1f));
            UiKit.SetRect(kind.gameObject, 16f, 46f, 240f, 28f);
            if (!read)
            {
                var badge = UiKit.Label(row.transform, UiKit.Ui(App.Content, "ui.evidence_new", "새 자료"), 20f, new Color(1f, 0.85f, 0.35f, 1f));
                UiKit.SetRect(badge.gameObject, 420f, 10f, 120f, 28f);
            }
        }

        private string KindLabel(string kind)
        {
            switch (kind)
            {
                case "document": return UiKit.Ui(App.Content, "ui.evidence_kind_document", "문서");
                case "video": return UiKit.Ui(App.Content, "ui.evidence_kind_video", "영상 기록");
                case "log": return UiKit.Ui(App.Content, "ui.evidence_kind_log", "로그");
                case "personal": return UiKit.Ui(App.Content, "ui.evidence_kind_personal", "개인 자료");
                default: return kind;
            }
        }

        private void SelectEvidence(string id)
        {
            _selectedId = id;
            App.Exec(CommandKinds.OpenEvidence, id);
            Refresh();
        }

        private void RefreshDetail()
        {
            var def = App.Content != null ? App.Content.EvidenceDef(_selectedId) : null;
            bool has = def != null && App.Session.State.Owns(def.id);
            _detailTitle.gameObject.SetActive(has);
            _detailBody.gameObject.SetActive(has);
            _detailMeta.gameObject.SetActive(has);
            _imageHolder.SetActive(has);
            _emptyDetail.gameObject.SetActive(!has);
            if (!has) return;

            _detailTitle.text = def.title;
            _detailBody.text = _tab == 0 ? def.body : (def.sourceMetadata + "\n" + def.source + "\n\n" + def.body);
            _detailMeta.text =
                UiKit.Ui(App.Content, "ui.evidence_source", "출처") + ": " + def.source + "\n" +
                UiKit.Ui(App.Content, "ui.evidence_proves", "확인할 수 있는 것") + ": " + def.proves + "\n" +
                UiKit.Ui(App.Content, "ui.evidence_limits", "아직 알 수 없는 것") + ": " + def.limits;

            var sprite = string.IsNullOrEmpty(def.imageAssetId) ? null : Kit.Sprite(def.imageAssetId);
            _detailImage.sprite = sprite;
            _detailImage.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            if (sprite != null)
            {
                _detailImage.gameObject.SetActive(true);
                UiKit.Contain(_detailImage.gameObject, 880f, 220f);
                _detailImage.rectTransform.anchoredPosition = Vector2.zero;
            }
        }

        private void RefreshPicker()
        {
            UiKit.ClearChildren(_pickerList);
            var owned = App.Session.State.OwnedEvidence;
            for (int i = 0; i < owned.Count; i++)
            {
                var id = owned[i];
                var def = App.Content.EvidenceDef(id);
                if (def == null) continue;
                if (_pickerFilter != null && !_pickerFilter(id)) continue;
                BuildPickerRow(def, _pickerSelected.Contains(id));
            }
            if (_pickerCounter != null)
                _pickerCounter.text = _pickerSelected.Count + " / " + _pickerMax;
            if (_pickerConfirm != null)
                _pickerConfirm.interactable = _pickerSelected.Count > 0;
        }

        private void BuildPickerRow(EvidenceDefinitionDto def, bool selected)
        {
            var row = UiKit.Panel(_pickerList, "Evidence", selected ? new Color(0.2f, 0.26f, 0.38f, 1f) : new Color(0.1f, 0.12f, 0.18f, 0.96f));
            row.AddComponent<LayoutElement>().preferredHeight = 76f;
            var id = def.id;
            UiKit.HitArea(row, () => TogglePicker(id));
            var title = UiKit.Label(row.transform, def.title, 25f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(title.gameObject, 16f, 8f, 700f, 34f);
            var kind = UiKit.Label(row.transform, KindLabel(def.kind), 20f, new Color(0.68f, 0.72f, 0.82f, 1f));
            UiKit.SetRect(kind.gameObject, 16f, 44f, 240f, 26f);
            if (selected)
            {
                var check = UiKit.Label(row.transform, "선택됨", 22f, new Color(0.55f, 0.9f, 0.6f, 1f), TextAlignmentOptions.Left);
                UiKit.SetRect(check.gameObject, 1360f, 22f, 120f, 36f);
            }
        }

        private void TogglePicker(string id)
        {
            if (_pickerSelected.Contains(id)) _pickerSelected.Remove(id);
            else if (_pickerSelected.Count >= _pickerMax)
            {
                App.Toast(UiKit.Ui(App.Content, "ui.select_up_to_3", "최대 3개까지 선택할 수 있습니다."));
                return;
            }
            else _pickerSelected.Add(id);
            Refresh();
        }

        private void ConfirmPicker()
        {
            var callback = _pickerOnConfirm;
            var selected = new List<string>(_pickerSelected);
            App.CloseModal(this);
            if (callback != null) callback(selected);
        }

        private void CancelPicker()
        {
            _pickerOnConfirm = null;
            App.CloseModal(this);
        }
    }

    public sealed class HintPanel : AnlScreen
    {
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _sceneTip;
        private TextMeshProUGUI _nextAction;
        private readonly TextMeshProUGUI[] _hintTexts = new TextMeshProUGUI[3];
        private readonly Button[] _hintButtons = new Button[3];
        private TextMeshProUGUI _blocker;
        private Button _testAssist;
        private HintContext _context;

        public HintPanel(AppRoot app) : base(app)
        {
            IsModal = true;
        }

        public override void Build()
        {
            Root = UiKit.Dim(Kit.ModalLayer, "HintPanel");
            var window = UiKit.Panel(Root.transform, "Window", new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UiKit.SetRect(window, 350f, 140f, 1220f, 800f);

            _title = UiKit.Label(window.transform, UiKit.Ui(App.Content, "ui.hints", "팁"), 34f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(_title.gameObject, 28f, 20f, 900f, 50f);
            var close = UiKit.TextButton(window.transform, UiKit.Ui(App.Content, "ui.close", "닫기"), 24f, () => App.CloseModal(this), null, 140f, 52f);
            UiKit.SetRect(close.gameObject, 1052f, 18f, 140f, 52f);

            _sceneTip = UiKit.Label(window.transform, "", 24f, new Color(0.85f, 0.88f, 0.95f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_sceneTip.gameObject, 28f, 84f, 1164f, 90f);

            _nextAction = UiKit.Label(window.transform, "", 26f, new Color(1f, 0.88f, 0.55f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_nextAction.gameObject, 28f, 182f, 1164f, 70f);

            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;
                _hintButtons[i] = UiKit.TextButton(window.transform, UiKit.Ui(App.Content, "ui.hint_h" + level, "힌트 " + level), 24f,
                    () => RevealLevel(level), new Color(0.12f, 0.15f, 0.22f, 0.96f), 240f, 56f);
                UiKit.SetRect(_hintButtons[i].gameObject, 28f + i * 260f, 264f, 240f, 56f);
                _hintTexts[i] = UiKit.Label(window.transform, "", 23f, Color.white, TextAlignmentOptions.TopLeft, true);
                UiKit.SetRect(_hintTexts[i].gameObject, 28f, 332f + i * 108f, 1164f, 100f);
            }

            _blocker = UiKit.Label(window.transform, "", 22f, new Color(1f, 0.72f, 0.55f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_blocker.gameObject, 28f, 664f, 1164f, 56f);

            _testAssist = UiKit.TextButton(window.transform, UiKit.Ui(App.Content, "ui.test_assist", "정답 행동 실행"), 24f,
                RunTestAssist, new Color(0.4f, 0.2f, 0.2f, 0.96f), 280f, 56f);
            UiKit.SetRect(_testAssist.gameObject, 908f, 712f, 280f, 56f);
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            _context = App.Session.Hints.Resolve(App.Session.State);
            for (int i = 0; i < 3; i++)
            {
                if (_hintTexts[i] != null) _hintTexts[i].text = "";
                if (_hintButtons[i] != null)
                {
                    bool has = FindLevel(i + 1) != null;
                    _hintButtons[i].gameObject.SetActive(has);
                    _hintButtons[i].interactable = has;
                }
            }
            if (_title != null) _title.text = UiKit.Ui(App.Content, "ui.hints", "팁") + " · " + _context.title;
            if (_sceneTip != null) _sceneTip.text = _context.sceneTip;
            if (_nextAction != null)
                _nextAction.text = UiKit.Ui(App.Content, "ui.hint_scene", "다음 행동") + ": " + _context.nextAction;
            if (_blocker != null) _blocker.text = "";
            bool showAssist = App.TestToolsEnabled
                && App.Session.State.Phase == Phases.Interaction
                && !string.IsNullOrEmpty(App.Session.State.ActivePuzzleId);
            if (_testAssist != null) _testAssist.gameObject.SetActive(showAssist);
        }

        private HintDefinitionDto FindLevel(int level)
        {
            if (_context == null) return null;
            for (int i = 0; i < _context.levels.Count; i++)
                if (_context.levels[i].level == level) return _context.levels[i];
            return null;
        }

        public void RevealLevel(int level)
        {
            if (level < 1 || level > 3 || _context == null) return;
            var hint = FindLevel(level);
            if (hint == null) return;
            var text = !string.IsNullOrEmpty(hint.text) ? hint.text : App.Content.TextOf(hint.textId);
            _hintTexts[level - 1].text = text;
            App.Exec(CommandKinds.OpenHint, _context.scopeId, null, null, null, level);
        }

        private void RunTestAssist()
        {
            if (App.Session == null || App.Content == null) return;
            var plan = TestAssistPlanner.Plan(App.Content, App.Session.State);
            if (!plan.CanRun)
            {
                _blocker.text = plan.Blocker == "missing_required_evidence"
                    ? UiKit.Ui(App.Content, "ui.test_assist_missing", "필요한 자료가 아직 없습니다. 획득 위치를 확인하세요.")
                    : plan.Blocker;
                return;
            }
            _blocker.text = "";
            for (int i = 0; i < plan.Commands.Count; i++)
            {
                var command = plan.Commands[i];
                var result = App.Exec(command.type, command.targetId, command.ids, command.slot, command.value, command.numeric);
                if (result == null || result.status == ResultStatus.Rejected)
                {
                    _blocker.text = result != null && !string.IsNullOrEmpty(result.message) ? result.message : "blocked at " + command.type;
                    return;
                }
                if (App.Session.State.Phase != Phases.Interaction) return;
            }
        }
    }

    public sealed class SettingsPanel : AnlScreen
    {
        private readonly Button[] _scaleButtons = new Button[3];
        private readonly Button[] _speedButtons = new Button[3];
        private readonly Button[] _toggleButtons = new Button[5];
        private readonly Func<SettingsData, bool>[] _getters = new Func<SettingsData, bool>[5];
        private readonly Action<SettingsData, bool>[] _setters = new Action<SettingsData, bool>[5];
        private readonly float[] _scales = { 1f, 1.25f, 1.5f };
        private TextMeshProUGUI _status;

        public SettingsPanel(AppRoot app) : base(app)
        {
            IsModal = true;
        }

        public override void Build()
        {
            Root = UiKit.Dim(Kit.ModalLayer, "SettingsPanel");
            var window = UiKit.Panel(Root.transform, "Window", new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UiKit.SetRect(window, 460f, 120f, 1000f, 840f);

            var title = UiKit.Label(window.transform, UiKit.Ui(App.Content, "ui.settings", "설정"), 34f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(title.gameObject, 28f, 20f, 700f, 50f);
            var close = UiKit.TextButton(window.transform, UiKit.Ui(App.Content, "ui.close", "닫기"), 24f, () => App.CloseModal(this), null, 140f, 52f);
            UiKit.SetRect(close.gameObject, 832f, 18f, 140f, 52f);

            float y = 96f;
            var scaleLabel = UiKit.Label(window.transform, UiKit.Ui(App.Content, "ui.settings_text_size", "글자 크기"), 26f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(scaleLabel.gameObject, 28f, y, 500f, 44f);
            string[] scaleLabels = { "100%", "125%", "150%" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _scaleButtons[i] = UiKit.TextButton(window.transform, scaleLabels[i], 22f, () => ApplyScale(_scales[index]), null, 150f, 52f);
                UiKit.SetRect(_scaleButtons[i].gameObject, 560f, y + i * 64f, 150f, 52f);
            }
            y += 208f;

            var speedLabel = UiKit.Label(window.transform, UiKit.Ui(App.Content, "ui.settings_text_speed", "텍스트 출력 속도"), 26f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(speedLabel.gameObject, 28f, y, 500f, 44f);
            string[] speedLabels =
            {
                UiKit.Ui(App.Content, "ui.settings_slow", "느림"),
                UiKit.Ui(App.Content, "ui.settings_normal", "보통"),
                UiKit.Ui(App.Content, "ui.settings_fast", "빠름")
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                _speedButtons[i] = UiKit.TextButton(window.transform, speedLabels[i], 22f, () => ApplySpeed(index), null, 150f, 52f);
                UiKit.SetRect(_speedButtons[i].gameObject, 560f, y + i * 64f, 150f, 52f);
            }
            y += 208f;

            AddToggle(window.transform, 0, UiKit.Ui(App.Content, "ui.settings_instant_text", "글자 즉시 출력"), ref y, s => s.instantText, (s, v) => s.instantText = v);
            AddToggle(window.transform, 1, UiKit.Ui(App.Content, "ui.settings_reduce_motion", "동작 줄이기"), ref y, s => s.reduceMotion, (s, v) => s.reduceMotion = v);
            AddToggle(window.transform, 2, UiKit.Ui(App.Content, "ui.settings_reduce_flash", "점멸 줄이기"), ref y, s => s.reduceFlash, (s, v) => s.reduceFlash = v);
            AddToggle(window.transform, 3, UiKit.Ui(App.Content, "ui.settings_audio", "오디오"), ref y, s => s.audioEnabled, (s, v) => s.audioEnabled = v);
            AddToggle(window.transform, 4, UiKit.Ui(App.Content, "ui.settings_auto_advance", "자동 진행"), ref y, s => s.autoAdvance, (s, v) => s.autoAdvance = v);

            _status = UiKit.Label(window.transform, "", 22f, new Color(0.75f, 0.78f, 0.85f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_status.gameObject, 28f, y + 4f, 940f, 52f);
        }

        private void AddToggle(Transform parent, int index, string label, ref float y, Func<SettingsData, bool> get, Action<SettingsData, bool> set)
        {
            _getters[index] = get;
            _setters[index] = set;
            var text = UiKit.Label(parent, label, 26f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(text.gameObject, 28f, y, 500f, 44f);
            _toggleButtons[index] = UiKit.TextButton(parent, "", 22f, () => ApplyToggle(index), null, 150f, 52f);
            UiKit.SetRect(_toggleButtons[index].gameObject, 560f, y, 150f, 52f);
            y += 64f;
        }

        private void ApplyScale(float scale)
        {
            Apply(s => s.textScale = scale);
        }

        private void ApplySpeed(int index)
        {
            UiKit.TextSpeedIndex = index;
            Refresh();
        }

        private void ApplyToggle(int index)
        {
            var get = _getters[index];
            var set = _setters[index];
            if (get == null || set == null) return;
            Apply(s => set(s, !get(s)));
        }

        private void Apply(Action<SettingsData> mutate)
        {
            if (App.Session == null) return;
            var current = App.Session.Settings;
            var next = new SettingsData
            {
                textScale = current != null ? current.textScale : 1f,
                instantText = current != null && current.instantText,
                reduceMotion = current != null && current.reduceMotion,
                reduceFlash = current != null && current.reduceFlash,
                audioEnabled = current == null || current.audioEnabled,
                autoAdvance = current != null && current.autoAdvance,
                testToolsEnabled = App.TestToolsEnabled
            };
            mutate(next);
            next.testToolsEnabled = App.TestToolsEnabled;
            App.Session.ApplySettings(next);
            Refresh();
            App.Refresh();
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            var settings = App.Session.Settings;
            for (int i = 0; i < _scaleButtons.Length; i++)
                MarkSelected(_scaleButtons[i], settings != null && Mathf.Approximately(settings.textScale, _scales[i]));
            for (int i = 0; i < _speedButtons.Length; i++)
                MarkSelected(_speedButtons[i], UiKit.TextSpeedIndex == i);
            for (int i = 0; i < _toggleButtons.Length; i++)
            {
                bool value = settings != null && _getters[i] != null && _getters[i](settings);
                var label = _toggleButtons[i] != null ? _toggleButtons[i].GetComponentInChildren<TextMeshProUGUI>() : null;
                if (label != null)
                    label.text = value ? UiKit.Ui(App.Content, "ui.settings_on", "켜짐") : UiKit.Ui(App.Content, "ui.settings_off", "꺼짐");
                MarkSelected(_toggleButtons[i], value);
            }
            if (_status != null)
                _status.text = UiKit.Ui(App.Content, "ui.settings_test_tools", "테스트 지원") + ": " +
                    (App.TestToolsEnabled ? UiKit.Ui(App.Content, "ui.settings_on", "켜짐") : UiKit.Ui(App.Content, "ui.settings_off", "꺼짐"));
        }

        private static void MarkSelected(Button button, bool selected)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image == null) return;
            image.color = selected ? new Color(0.24f, 0.42f, 0.32f, 1f) : new Color(0.14f, 0.16f, 0.22f, 0.96f);
        }
    }

    public sealed class SaveLoadPanel : AnlScreen
    {
        private RectTransform _list;
        private TextMeshProUGUI _confirmText;
        private GameObject _confirmBox;
        private Action _confirmAction;

        public SaveLoadPanel(AppRoot app) : base(app)
        {
            IsModal = true;
        }

        public override void Build()
        {
            Root = UiKit.Dim(Kit.ModalLayer, "SaveLoadPanel");
            var window = UiKit.Panel(Root.transform, "Window", new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UiKit.SetRect(window, 360f, 100f, 1200f, 880f);

            var title = UiKit.Label(window.transform, UiKit.Ui(App.Content, "ui.save_slot", "저장 슬롯"), 34f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(title.gameObject, 28f, 20f, 800f, 50f);
            var close = UiKit.TextButton(window.transform, UiKit.Ui(App.Content, "ui.close", "닫기"), 24f, () => App.CloseModal(this), null, 140f, 52f);
            UiKit.SetRect(close.gameObject, 1032f, 18f, 140f, 52f);

            RectTransform content;
            var scroll = UiKit.Scroll(window.transform, "Slots", out content);
            UiKit.SetRect(scroll.gameObject, 28f, 92f, 1144f, 760f);
            _list = content;

            _confirmBox = UiKit.Panel(Root.transform, "Confirm", new Color(0.04f, 0.05f, 0.08f, 0.99f));
            UiKit.SetRect(_confirmBox, 660f, 420f, 600f, 240f);
            _confirmText = UiKit.Label(_confirmBox.transform, "", 24f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_confirmText.gameObject, 24f, 20f, 552f, 120f);
            var ok = UiKit.TextButton(_confirmBox.transform, UiKit.Ui(App.Content, "ui.confirm", "확인"), 24f, Confirm, new Color(0.16f, 0.3f, 0.22f, 0.96f), 160f, 52f);
            UiKit.SetRect(ok.gameObject, 220f, 164f, 160f, 52f);
            var cancel = UiKit.TextButton(_confirmBox.transform, UiKit.Ui(App.Content, "ui.cancel", "취소"), 24f, HideConfirm, null, 160f, 52f);
            UiKit.SetRect(cancel.gameObject, 400f, 164f, 160f, 52f);
            _confirmBox.SetActive(false);
        }

        public void Show()
        {
            App.OpenModal(this);
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            UiKit.ClearChildren(_list);
            string[] slots = { "autosave", "slot_01", "slot_02", "slot_03" };
            for (int i = 0; i < slots.Length; i++) BuildRow(slots[i]);
        }

        private void BuildRow(string slot)
        {
            var row = UiKit.Panel(_list, "Slot", new Color(0.1f, 0.12f, 0.18f, 0.96f));
            row.AddComponent<LayoutElement>().preferredHeight = 150f;

            bool exists = App.Session.Saves.Exists(slot);
            string name = slot == "autosave" ? UiKit.Ui(App.Content, "ui.slot_autosave", "자동 저장") : slot;
            var nameLabel = UiKit.Label(row.transform, name, 28f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(nameLabel.gameObject, 20f, 14f, 560f, 40f);

            string savedAt = exists ? SaveUi.SavedAt(App.Session, slot) : "";
            var info = UiKit.Label(row.transform,
                exists
                    ? UiKit.Ui(App.Content, "ui.saved_at", "저장 시각") + ": " + savedAt
                    : UiKit.Ui(App.Content, "ui.empty_slot", "빈 슬롯"),
                22f, new Color(0.72f, 0.76f, 0.85f, 1f), TextAlignmentOptions.TopLeft);
            UiKit.SetRect(info.gameObject, 20f, 62f, 700f, 34f);

            var save = UiKit.TextButton(row.transform, UiKit.Ui(App.Content, "ui.save", "저장"), 23f, () => RequestSave(slot), null, 170f, 56f);
            UiKit.SetRect(save.gameObject, 776f, 46f, 170f, 56f);

            var load = UiKit.TextButton(row.transform, UiKit.Ui(App.Content, "ui.load", "불러오기"), 23f, () => Load(slot), null, 170f, 56f);
            UiKit.SetRect(load.gameObject, 962f, 46f, 170f, 56f);
            load.interactable = exists;
        }

        private void RequestSave(string slot)
        {
            if (App.Session.Saves.Exists(slot))
            {
                ShowConfirm(UiKit.Ui(App.Content, "ui.overwrite_confirm", "기존 저장을 덮어쓸까요?"), () => Save(slot));
                return;
            }
            Save(slot);
        }

        private void Save(string slot)
        {
            bool ok = App.Session.SaveManual(slot);
            if (ok) App.Toast(UiKit.Ui(App.Content, "ui.auto_save_done", "자동 저장되었습니다."));
            else App.Toast(UiKit.Ui(App.Content, "ui.save_failed", "저장에 실패했습니다. 재시도하십시오."));
            Refresh();
        }

        private void Load(string slot)
        {
            LoadOutcome outcome;
            if (App.Session.LoadGame(slot, out outcome))
            {
                App.CloseModal(this);
                App.Refresh();
                App.Toast(UiKit.Ui(App.Content, "ui.load_success", "불러왔습니다."));
                return;
            }
            string message = SaveUi.IsIncompatible(outcome)
                ? UiKit.Ui(App.Content, "ui.save_incompatible", "이 저장은 현재 버전과 호환되지 않습니다. 원본은 보존됩니다.")
                : UiKit.Ui(App.Content, "ui.save_corrupt", "저장 파일이 손상되었습니다. 백업에서 복원할까요?");
            ShowConfirm(message, () => Restore(slot));
        }

        private void Restore(string slot)
        {
            LoadOutcome outcome;
            if (App.Session.LoadGame(slot, out outcome))
            {
                App.CloseModal(this);
                App.Refresh();
                App.Toast(UiKit.Ui(App.Content, "ui.load_success", "불러왔습니다."));
            }
            else
            {
                App.Toast(UiKit.Ui(App.Content, "ui.save_unknown_id", "저장에 알 수 없는 항목이 있습니다. 복원할 수 없습니다."));
            }
        }

        private void ShowConfirm(string message, Action action)
        {
            _confirmAction = action;
            _confirmText.text = message;
            _confirmBox.SetActive(true);
        }

        private void HideConfirm()
        {
            _confirmAction = null;
            _confirmBox.SetActive(false);
        }

        private void Confirm()
        {
            var action = _confirmAction;
            HideConfirm();
            if (action != null) action();
        }
    }

    public sealed class MenuPanel : AnlScreen
    {
        public SaveLoadPanel Saves { get; set; }

        public MenuPanel(AppRoot app) : base(app)
        {
            IsModal = true;
        }

        public override void Build()
        {
            Root = UiKit.Dim(Kit.ModalLayer, "MenuPanel");
            var window = UiKit.Panel(Root.transform, "Window", new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UiKit.SetRect(window, 660f, 180f, 600f, 720f);

            var title = UiKit.Label(window.transform, UiKit.Ui(App.Content, "ui.menu", "메뉴"), 34f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(title.gameObject, 28f, 20f, 400f, 50f);

            float y = 100f;
            MenuButton(window.transform, UiKit.Ui(App.Content, "ui.save", "저장"), ref y, () => OpenSaves());
            MenuButton(window.transform, UiKit.Ui(App.Content, "ui.load", "불러오기"), ref y, () => OpenSaves());
            MenuButton(window.transform, UiKit.Ui(App.Content, "ui.settings", "설정"), ref y, () => App.OpenSettings());
            MenuButton(window.transform, UiKit.Ui(App.Content, "ui.hints", "팁"), ref y, () => App.OpenHints());
            MenuButton(window.transform, UiKit.Ui(App.Content, "ui.back_to_title", "타이틀로"), ref y, GoTitle);
            MenuButton(window.transform, UiKit.Ui(App.Content, "ui.close", "닫기"), ref y, () => App.CloseModal(this));

            _info = UiKit.Label(window.transform, "", 22f, new Color(0.78f, 0.82f, 0.9f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_info.gameObject, 28f, 600f, 544f, 100f);
        }

        private TextMeshProUGUI _info;

        private void MenuButton(Transform parent, string label, ref float y, Action action)
        {
            var button = UiKit.TextButton(parent, label, 25f, action, null, 544f, 60f);
            UiKit.SetRect(button.gameObject, 28f, y, 544f, 60f);
            y += 76f;
        }

        private void OpenSaves()
        {
            if (Saves != null) Saves.Show();
        }

        private void GoTitle()
        {
            App.CloseModal(this);
            App.Session.Dispatcher.EnterScene("s000");
            App.Refresh();
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            var scene = App.Content.Scene(App.Session.State.CurrentSceneId);
            string sceneText = scene != null ? scene.id : "-";
            string savedAt = App.Session.Saves.Exists("autosave") ? SaveUi.SavedAt(App.Session, "autosave") : "";
            _info.text =
                UiKit.Ui(App.Content, "ui.current_objective", "현재 목표") + ": " + sceneText + "\n" +
                UiKit.Ui(App.Content, "ui.saved_at", "저장 시각") + ": " + savedAt;
        }
    }

    public sealed class BranchLinesOverlay : AnlScreen
    {
        private RectTransform _list;
        private PressLineDto[] _lines;
        private Action _onDone;

        public BranchLinesOverlay(AppRoot app) : base(app)
        {
            IsModal = true;
        }

        public override void Build()
        {
            Root = UiKit.Dim(Kit.ModalLayer, "BranchLinesOverlay");
            var window = UiKit.Panel(Root.transform, "Window", new Color(0.05f, 0.06f, 0.09f, 0.98f));
            UiKit.SetRect(window, 360f, 200f, 1200f, 680f);

            RectTransform content;
            var scroll = UiKit.Scroll(window.transform, "Lines", out content);
            UiKit.SetRect(scroll.gameObject, 28f, 28f, 1144f, 540f);
            _list = content;

            var confirm = UiKit.TextButton(window.transform, UiKit.Ui(App.Content, "ui.confirm", "확인"), 26f, Confirm, new Color(0.16f, 0.3f, 0.22f, 0.96f), 220f, 60f);
            UiKit.SetRect(confirm.gameObject, 952f, 588f, 220f, 60f);
        }

        public void Show(PressLineDto[] lines, Action onDone)
        {
            _lines = lines;
            _onDone = onDone;
            App.OpenModal(this);
        }

        public override void Refresh()
        {
            if (App.Content == null || _list == null) return;
            UiKit.ClearChildren(_list);
            if (_lines == null) return;
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line == null) continue;
                string speaker = string.IsNullOrEmpty(line.speaker) ? "" : line.speaker + ": ";
                var label = UiKit.Label(_list, speaker + line.text, 25f, Color.white, TextAlignmentOptions.TopLeft, true);
                label.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;
            }
        }

        private void Confirm()
        {
            App.CloseModal(this);
            var callback = _onDone;
            _onDone = null;
            if (callback != null) callback();
        }
    }

    public sealed class FatalScreen : AnlScreen
    {
        private TextMeshProUGUI _message;
        private TextMeshProUGUI _errors;
        private List<string> _errorList = new List<string>();
        private Action _onRetry;
        private Action _onQuit;

        public FatalScreen(AppRoot app) : base(app)
        {
        }

        public void Configure(List<string> errors, Action onRetry, Action onQuit)
        {
            _errorList = errors ?? new List<string>();
            _onRetry = onRetry;
            _onQuit = onQuit;
            if (Root != null) Apply();
        }

        public override void Build()
        {
            Root = UiKit.Dim(Kit.ModalLayer, "FatalScreen");
            var window = UiKit.Panel(Root.transform, "Window", new Color(0.08f, 0.04f, 0.05f, 0.99f));
            UiKit.SetRect(window, 360f, 160f, 1200f, 760f);

            var header = UiKit.Label(window.transform, "콘텐츠를 불러오지 못했습니다", 38f, new Color(1f, 0.75f, 0.7f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(header.gameObject, 32f, 28f, 1136f, 60f);
            _message = UiKit.Label(window.transform, "다시 시도하거나 종료하십시오.", 25f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_message.gameObject, 32f, 100f, 1136f, 70f);

            RectTransform content;
            var scroll = UiKit.Scroll(window.transform, "Errors", out content);
            UiKit.SetRect(scroll.gameObject, 32f, 184f, 1136f, 456f);
            _errors = UiKit.Label(content, "", 22f, new Color(0.95f, 0.85f, 0.85f, 1f), TextAlignmentOptions.TopLeft, true);

            var retry = UiKit.TextButton(window.transform, "다시 시도", 26f, () => { if (_onRetry != null) _onRetry(); }, new Color(0.16f, 0.3f, 0.22f, 0.96f), 220f, 60f);
            UiKit.SetRect(retry.gameObject, 724f, 664f, 220f, 60f);
            var quit = UiKit.TextButton(window.transform, "종료", 26f, () => { if (_onQuit != null) _onQuit(); }, new Color(0.35f, 0.15f, 0.15f, 0.96f), 220f, 60f);
            UiKit.SetRect(quit.gameObject, 956f, 664f, 220f, 60f);
            Apply();
        }

        public override void Refresh()
        {
            Apply();
        }

        private void Apply()
        {
            if (_errors == null) return;
            string text = "";
            for (int i = 0; i < _errorList.Count; i++)
                text += "• " + _errorList[i] + "\n";
            if (string.IsNullOrEmpty(text) && App.Session != null && !string.IsNullOrEmpty(App.Session.BlockingError))
                text = "• " + App.Session.BlockingError;
            _errors.text = text;
        }
    }
}
