using ANL.Core.Conditions;
using ANL.Core.Contracts;
using ANL.Core.State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class HubScreen : AnlScreen
    {
        private GameObject _background;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _progress;
        private GameObject _cardsRoot;
        private Button _nextButton;
        private TextMeshProUGUI _nextReason;

        public HubScreen(AppRoot app) : base(app)
        {
        }

        public override void Build()
        {
            Root = UiKit.Panel(Kit.ContentLayer, "HubScreen", new Color(0.02f, 0.03f, 0.05f, 1f));
            UiKit.Stretch(Root);

            _background = UiKit.Panel(Root.transform, "Background", Color.white);
            UiKit.Stretch(_background);
            _background.GetComponent<Image>().raycastTarget = false;

            var header = UiKit.Panel(Root.transform, "Header", new Color(0.03f, 0.04f, 0.07f, 0.94f));
            UiKit.SetRect(header, 40f, 40f, 1840f, 150f);
            _title = UiKit.Label(header.transform, "", 40f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(_title.gameObject, 24f, 18f, 900f, 50f);
            _progress = UiKit.Label(header.transform, "", 26f, new Color(0.85f, 0.9f, 1f, 1f), TextAlignmentOptions.TopLeft);
            UiKit.SetRect(_progress.gameObject, 24f, 78f, 1200f, 44f);

            _nextButton = UiKit.TextButton(header.transform, "다음 단계", 26f,
                () => App.Exec(CommandKinds.ContinueScene), new Color(0.16f, 0.3f, 0.22f, 0.96f), 240f, 64f);
            UiKit.SetRect(_nextButton.gameObject, 1560f, 42f, 240f, 64f);
            _nextReason = UiKit.Label(header.transform, "", 21f, new Color(1f, 0.82f, 0.6f, 1f), TextAlignmentOptions.TopRight, true);
            UiKit.SetRect(_nextReason.gameObject, 900f, 110f, 900f, 32f);

            _cardsRoot = new GameObject("Cards", typeof(RectTransform));
            _cardsRoot.transform.SetParent(Root.transform, false);
            UiKit.Stretch(_cardsRoot);
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

            _title.text = scene.id + " · Act " + scene.act;
            UiKit.ClearChildren(_cardsRoot.transform);

            int total = scene.childSceneIds != null ? scene.childSceneIds.Length : 0;
            int done = 0;
            int index = 0;
            if (scene.childSceneIds != null)
            {
                for (int i = 0; i < scene.childSceneIds.Length; i++)
                {
                    if (state.IsSceneCompleted(scene.childSceneIds[i])) done++;
                    BuildCard(scene.childSceneIds[i], false, index++);
                }
            }
            if (scene.optionalChildSceneIds != null)
            {
                for (int i = 0; i < scene.optionalChildSceneIds.Length; i++)
                    BuildCard(scene.optionalChildSceneIds[i], true, index++);
            }
            _progress.text = "필수 " + done + "/" + total + " 완료";

            string target;
            string reason;
            bool available = App.Session.Dispatcher.TransitionAvailable(out target, out reason);
            _nextButton.interactable = available;
            if (available)
            {
                _nextReason.text = "";
            }
            else if (done >= total && total > 0)
            {
                _nextReason.text = UiKit.Ui(App.Content, "ui.hub_all_required_done", "필수 조사가 끝났습니다. 다음 단계로 진행할 수 있습니다.");
            }
            else
            {
                _nextReason.text = reason != null && reason.StartsWith("remaining:")
                    ? UiKit.Ui(App.Content, "ui.hub_locked_reason", "선행 조사 완료 후 열립니다.")
                    : reason;
            }
        }

        private void BuildCard(string childId, bool optional, int index)
        {
            var state = App.Session.State;
            var child = App.Content.Scene(childId);
            if (child == null) return;

            bool completed = state.IsSceneCompleted(childId);
            bool locked = !ConditionEvaluator.AllConditionsMet(child.entryConditions, state, App.Content);

            int col = index % 2;
            int row = index / 2;
            var card = UiKit.Panel(_cardsRoot.transform, "Card", new Color(0.05f, 0.06f, 0.1f, 0.96f));
            UiKit.SetRect(card, 40f + col * 620f, 230f + row * 250f, 580f, 230f);

            var thumb = UiKit.Panel(card.transform, "Thumb", new Color(0.03f, 0.04f, 0.06f, 1f));
            UiKit.SetRect(thumb, 16f, 20f, 210f, 190f);
            var thumbImage = UiKit.Panel(thumb.transform, "Image", Color.white);
            var image = thumbImage.GetComponent<Image>();
            image.sprite = Kit.Sprite(child.backgroundAssetId);
            image.color = image.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            if (image.sprite != null)
            {
                UiKit.Contain(thumbImage, 200f, 180f);
                thumbImage.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            }

            var label = UiKit.Label(card.transform, childId, 32f, Color.white, TextAlignmentOptions.TopLeft);
            UiKit.SetRect(label.gameObject, 246f, 22f, 320f, 44f);
            var kind = UiKit.Label(card.transform, child.screenType, 22f, new Color(0.7f, 0.75f, 0.85f, 1f));
            UiKit.SetRect(kind.gameObject, 246f, 72f, 320f, 32f);

            string badge = completed
                ? UiKit.Ui(App.Content, "ui.hub_done", "완료")
                : locked
                    ? UiKit.Ui(App.Content, "ui.hub_locked", "잠김")
                    : optional
                        ? UiKit.Ui(App.Content, "ui.hub_optional", "선택")
                        : UiKit.Ui(App.Content, "ui.hub_visit", "조사");
            var badgeLabel = UiKit.Label(card.transform, badge, 24f,
                completed ? new Color(0.5f, 0.95f, 0.6f, 1f) : locked ? new Color(1f, 0.6f, 0.5f, 1f) : new Color(0.95f, 0.85f, 0.5f, 1f));
            UiKit.SetRect(badgeLabel.gameObject, 246f, 118f, 200f, 34f);

            if (locked)
            {
                var reason = UiKit.Label(card.transform, UiKit.Ui(App.Content, "ui.hub_locked_reason", "선행 조사 완료 후 열립니다."), 20f,
                    new Color(0.85f, 0.7f, 0.65f, 1f), TextAlignmentOptions.TopLeft, true);
                UiKit.SetRect(reason.gameObject, 246f, 158f, 320f, 60f);
                return;
            }

            var id = childId;
            UiKit.HitArea(card, () => App.EnterChildScene(id));
        }
    }
}
