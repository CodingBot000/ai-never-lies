using System;
using ANL.Core.Contracts;
using ANL.Core.Puzzles;
using ANL.Runtime.Assets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class PuzzleHostScreen : AnlScreen
    {
        private GameObject _background;
        private Image _backgroundImage;
        private GameObject _scrim;
        private GameObject _content;
        private PuzzleWidget _widget;
        private static AssetCatalog _catalog;
        private static bool _catalogLoaded;

        public PuzzleHostScreen(AppRoot app) : base(app)
        {
        }

        public override void Build()
        {
            Root = UiKit.Panel(Kit.ContentLayer, "PuzzleHostScreen", new Color(0.02f, 0.03f, 0.05f, 1f));
            UiKit.Stretch(Root);

            _background = UiKit.Panel(Root.transform, "Background", Color.white);
            UiKit.Stretch(_background);
            _backgroundImage = _background.GetComponent<Image>();
            _backgroundImage.raycastTarget = false;

            _scrim = UiKit.Panel(Root.transform, "Scrim", new Color(0.02f, 0.03f, 0.05f, 0.84f));
            UiKit.Stretch(_scrim);
            var scrimImage = _scrim.GetComponent<Image>();
            if (scrimImage != null) scrimImage.raycastTarget = false;

            _content = new GameObject("Widget", typeof(RectTransform));
            _content.transform.SetParent(Root.transform, false);
            UiKit.Stretch(_content);
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            var state = App.Session.State;
            var scene = App.Content.Scene(state.CurrentSceneId);
            if (scene != null && _backgroundImage != null)
            {
                var sprite = LoadSprite(scene.backgroundAssetId);
                _backgroundImage.sprite = sprite;
                _backgroundImage.color = sprite != null ? Color.white : new Color(0f, 0f, 0f, 0f);
                UiKit.Cover(_background, 1920f, 1080f);
            }

            if (_widget != null && _widget.Root != null) UnityEngine.Object.Destroy(_widget.Root);
            _widget = null;
            if (_content != null) UiKit.ClearChildren(_content.transform);

            var puzzle = App.Content.Puzzle(state.ActivePuzzleId);
            if (puzzle == null)
            {
                BuildMessage(UiKit.Ui(App.Content, "ui.no_active_puzzle", "진행 중인 문제가 없습니다."), true);
                return;
            }
            if (puzzle.type == PuzzleTypes.HotspotSet)
            {
                BuildMessage(UiKit.Ui(App.Content, "ui.interaction_hint", "이 문제는 조사 화면에서 진행됩니다."), false);
                return;
            }
            var draft = state.GetDraft(puzzle.id, true);
            _widget = CreateWidget(puzzle, draft);
            if (_widget == null)
            {
                BuildMessage(UiKit.Ui(App.Content, "ui.interaction_hint", "지원하지 않는 문제 형식입니다."), false);
                return;
            }
            _widget.Build(_content.transform);
        }

        private PuzzleWidget CreateWidget(PuzzleDefinitionDto puzzle, PuzzleDraftDto draft)
        {
            var parent = _content.transform;
            switch (puzzle.type)
            {
                case PuzzleTypes.StatementRefutation: return new StatementWidget(App, puzzle, draft, parent);
                case PuzzleTypes.SourceMapping:
                case PuzzleTypes.TraceMapping: return new SlotAssignWidget(App, puzzle, draft, parent);
                case PuzzleTypes.TimelineOrder:
                case PuzzleTypes.Chronology:
                case PuzzleTypes.DeploymentChain: return new OrderingWidget(App, puzzle, draft, parent);
                case PuzzleTypes.CrossCamera: return new CrossCameraWidget(App, puzzle, draft, parent);
                case PuzzleTypes.ArgumentChoice: return new ChoiceWidget(App, puzzle, draft, parent);
                case PuzzleTypes.DiffSelect: return new DiffSelectWidget(App, puzzle, draft, parent);
                case PuzzleTypes.AssumptionLink: return new AssumptionWidget(App, puzzle, draft, parent);
                case PuzzleTypes.MatrixGap: return new MatrixWidget(App, puzzle, draft, parent);
                case PuzzleTypes.Counterfactual: return new CounterfactualWidget(App, puzzle, draft, parent);
                case PuzzleTypes.CausalGraph: return new CausalBoardWidget(App, puzzle, draft, parent);
                case PuzzleTypes.ResponsibilityMatrix: return new ReportWidget(App, puzzle, draft, parent);
                default: return null;
            }
        }

        private void BuildMessage(string message, bool showContinue)
        {
            var panel = UiKit.Panel(_content.transform, "Message", new Color(0.07f, 0.08f, 0.12f, 0.97f));
            UiKit.SetRect(panel, 560f, 400f, 800f, 280f);
            var label = UiKit.Label(panel.transform, message, 28f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(label.gameObject, 32f, 32f, 736f, 130f);
            if (showContinue)
            {
                var button = UiKit.TextButton(panel.transform, UiKit.Ui(App.Content, "ui.continue_scene", "계속"), 26f,
                    () => App.Exec(CommandKinds.ContinueScene), new Color(0.16f, 0.3f, 0.22f, 0.96f), 220f, 64f);
                UiKit.SetRect(button.gameObject, 548f, 184f, 220f, 64f);
            }
        }

        private static Sprite LoadSprite(string assetId)
        {
            if (string.IsNullOrEmpty(assetId)) return null;
            try
            {
                if (!_catalogLoaded)
                {
                    _catalogLoaded = true;
                    _catalog = Resources.Load<AssetCatalog>("AssetCatalog");
                }
                if (_catalog == null || _catalog.entries == null) return null;
                for (int i = 0; i < _catalog.entries.Length; i++)
                {
                    var entry = _catalog.entries[i];
                    if (entry != null && entry.id == assetId) return entry.sprite;
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }
    }
}
