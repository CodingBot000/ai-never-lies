using ANL.Core.Contracts;
using ANL.Core.State;
using ANL.Runtime.Assets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class InvestigationScreen : AnlScreen
    {
        private GameObject _background;
        private Image _backgroundImage;
        private GameObject _regionsRoot;
        private TextMeshProUGUI _progress;
        private Button _toggleButton;
        private GameObject _detailPanel;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private GameObject _completePanel;
        private bool _showOutlines;

        public InvestigationScreen(AppRoot app) : base(app)
        {
        }

        public override void Build()
        {
            Root = UiKit.Panel(Kit.ContentLayer, "InvestigationScreen", new Color(0.02f, 0.03f, 0.05f, 1f));
            UiKit.Stretch(Root);

            _background = UiKit.Panel(Root.transform, "Background", Color.white);
            UiKit.Stretch(_background);
            _backgroundImage = _background.GetComponent<Image>();
            _backgroundImage.raycastTarget = false;

            _regionsRoot = new GameObject("Regions", typeof(RectTransform));
            _regionsRoot.transform.SetParent(Root.transform, false);
            UiKit.Stretch(_regionsRoot);

            _toggleButton = UiKit.TextButton(Root.transform, UiKit.Ui(App.Content, "ui.inspect_toggle", "조사 표시"), 24f,
                ToggleOutlines, new Color(0.1f, 0.12f, 0.18f, 0.94f), 220f, 56f);
            UiKit.SetRect(_toggleButton.gameObject, 40f, 28f, 220f, 56f);

            _progress = UiKit.Label(Root.transform, "", 24f, new Color(0.9f, 0.92f, 1f, 1f), TextAlignmentOptions.TopLeft);
            UiKit.SetRect(_progress.gameObject, 280f, 38f, 700f, 40f);

            _detailPanel = UiKit.Panel(Root.transform, "Detail", new Color(0.03f, 0.04f, 0.06f, 0.96f));
            UiKit.SetRect(_detailPanel, 1380f, 120f, 500f, 500f);
            _detailTitle = UiKit.Label(_detailPanel.transform, "", 28f, new Color(0.95f, 0.85f, 0.5f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_detailTitle.gameObject, 24f, 20f, 452f, 60f);
            _detailBody = UiKit.Label(_detailPanel.transform, "", 23f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_detailBody.gameObject, 24f, 90f, 452f, 260f);
            var hint = UiKit.Label(_detailPanel.transform, UiKit.Ui(App.Content, "ui.inspect_hint", "조사 표시를 켜면 사건과 관련된 지점이 표시됩니다."), 20f, new Color(0.75f, 0.78f, 0.86f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(hint.gameObject, 24f, 366f, 452f, 110f);
            _detailPanel.SetActive(false);

            _completePanel = UiKit.Panel(Root.transform, "Complete", new Color(0.04f, 0.07f, 0.05f, 0.97f));
            UiKit.SetRect(_completePanel, 560f, 400f, 800f, 280f);
            var completeLabel = UiKit.Label(_completePanel.transform, UiKit.Ui(App.Content, "ui.investigation_complete", "필요한 조사 지점을 모두 확인했습니다."), 28f, new Color(0.6f, 0.95f, 0.65f, 1f), TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(completeLabel.gameObject, 28f, 28f, 744f, 120f);
            var cont = UiKit.TextButton(_completePanel.transform, UiKit.Ui(App.Content, "ui.continue_scene", "계속"), 26f,
                () => App.Exec(CommandKinds.ContinueScene), new Color(0.16f, 0.3f, 0.22f, 0.96f), 220f, 60f);
            UiKit.SetRect(cont.gameObject, 552f, 190f, 220f, 60f);
            _completePanel.SetActive(false);
        }

        private void ToggleOutlines()
        {
            _showOutlines = !_showOutlines;
            Refresh();
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            var state = App.Session.State;
            var scene = App.Content.Scene(state.CurrentSceneId);
            if (scene == null) return;
            var puzzle = App.Content.Puzzle(state.ActivePuzzleId);

            var sprite = Kit.Sprite(scene.backgroundAssetId);
            _backgroundImage.sprite = sprite;
            _backgroundImage.color = sprite != null ? Color.white : new Color(0.06f, 0.07f, 0.11f, 1f);
            UiKit.Cover(_background, 1920f, 1080f);

            var entry = Kit.Entry(scene.backgroundAssetId);
            Vector2 panelSize = new Vector2(1920f, 1080f);
            Vector2 imageSize = sprite != null ? sprite.rect.size : panelSize;
            var displayed = ComputeContainRect(panelSize, imageSize);

            UiKit.ClearChildren(_regionsRoot.transform);
            int total = 0;
            int inspected = 0;
            if (puzzle != null && puzzle.regions != null)
            {
                total = puzzle.regions.Length;
                for (int i = 0; i < puzzle.regions.Length; i++)
                {
                    var region = puzzle.regions[i];
                    if (region == null) continue;
                    bool done = IsRegionInspected(puzzle.id, region.regionId);
                    if (done) inspected++;
                    var assetRegion = FindAssetRegion(entry, region.regionId);
                    if (assetRegion == null) continue;
                    BuildRegionButton(region, assetRegion, displayed, done);
                }
            }
            _progress.text = "조사 " + inspected + " / " + total;

            _toggleButton.GetComponent<Image>().color = _showOutlines
                ? new Color(0.24f, 0.42f, 0.32f, 1f)
                : new Color(0.1f, 0.12f, 0.18f, 0.94f);

            bool solved = puzzle != null && state.IsPuzzleSolved(puzzle.id);
            _completePanel.SetActive(solved);
        }

        private void BuildRegionButton(RegionDto region, AssetRegion assetRegion, Rect displayed, bool done)
        {
            Color color = _showOutlines
                ? (done ? new Color(0.4f, 0.9f, 0.5f, 0.16f) : new Color(1f, 0.85f, 0.3f, 0.18f))
                : new Color(1f, 1f, 1f, 0.001f);
            var go = UiKit.Panel(_regionsRoot.transform, "Region_" + region.regionId, color);
            float x = displayed.x + assetRegion.x * displayed.width;
            float y = displayed.y + assetRegion.y * displayed.height;
            float w = Mathf.Max(8f, assetRegion.width * displayed.width);
            float h = Mathf.Max(8f, assetRegion.height * displayed.height);
            UiKit.SetRect(go, x, y, w, h);
            var captured = region;
            UiKit.HitArea(go, () => InspectRegion(captured));
            if (_showOutlines)
            {
                var label = UiKit.Label(go.transform, region.regionId, 20f, new Color(1f, 0.95f, 0.75f, 1f), TextAlignmentOptions.TopLeft);
                UiKit.SetRect(label.gameObject, 6f, 4f, Mathf.Max(80f, w - 12f), 30f);
            }
        }

        private bool IsRegionInspected(string puzzleId, string regionId)
        {
            var draft = App.Session.State.GetDraft(puzzleId, false);
            if (draft == null || draft.placedCards == null) return false;
            for (int i = 0; i < draft.placedCards.Length; i++)
                if (draft.placedCards[i].slot == "region" && draft.placedCards[i].value == regionId) return true;
            return false;
        }

        private static AssetRegion FindAssetRegion(AssetEntry entry, string regionId)
        {
            if (entry == null || entry.regions == null) return null;
            for (int i = 0; i < entry.regions.Length; i++)
                if (entry.regions[i].regionId == regionId) return entry.regions[i];
            return null;
        }

        private void InspectRegion(RegionDto region)
        {
            App.Exec(CommandKinds.InspectRegion, region.regionId);
            ShowDetail(region);
        }

        private void ShowDetail(RegionDto region)
        {
            _detailPanel.SetActive(true);
            _detailTitle.text = region.regionId;
            var evidence = App.Content.EvidenceDef(region.evidenceId);
            if (evidence != null && App.Session.State.Owns(evidence.id))
            {
                _detailBody.text = evidence.title + "\n\n" + evidence.proves;
            }
            else
            {
                _detailBody.text = UiKit.Ui(App.Content, "ui.region_not_related", "이 부분에서는 사건과 직접 연결되는 기록을 찾지 못했습니다.");
            }
        }

        public static Rect ComputeContainRect(Vector2 panelSize, Vector2 imageSize)
        {
            if (imageSize.x <= 0f || imageSize.y <= 0f)
                return new Rect(0f, 0f, panelSize.x, panelSize.y);
            float scale = Mathf.Min(panelSize.x / imageSize.x, panelSize.y / imageSize.y);
            float width = imageSize.x * scale;
            float height = imageSize.y * scale;
            return new Rect((panelSize.x - width) * 0.5f, (panelSize.y - height) * 0.5f, width, height);
        }

        public static bool TryRegionLocalPoint(Vector2 localPoint, Rect displayedRect, AssetRegion region)
        {
            if (region == null) return false;
            float x = displayedRect.x + region.x * displayedRect.width;
            float y = displayedRect.y + region.y * displayedRect.height;
            float width = region.width * displayedRect.width;
            float height = region.height * displayedRect.height;
            return localPoint.x >= x && localPoint.x <= x + width
                && localPoint.y >= y && localPoint.y <= y + height;
        }
    }
}
