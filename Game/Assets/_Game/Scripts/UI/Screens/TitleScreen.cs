using System;
using ANL.Core.Contracts;
using ANL.Core.State;
using ANL.Runtime.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ANL.UI.Screens
{
    public sealed class TitleScreen : AnlScreen
    {
        private GameObject _background;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private Button _continueButton;
        private TextMeshProUGUI _continueReason;
        private Button _archiveButton;
        private TextMeshProUGUI _archiveReason;
        private GameObject _confirm;
        private TextMeshProUGUI _confirmBody;
        private TextMeshProUGUI _confirmLabel;
        private Action _pendingAction;
        private ArchiveScreen _archive;
        private bool _autoContinued;

        public TitleScreen(AppRoot app) : base(app)
        {
        }

        public override void Build()
        {
            Root = UiKit.Panel(Kit.ContentLayer, "TitleScreen", new Color(0.02f, 0.03f, 0.05f, 1f));
            UiKit.Stretch(Root);

            _background = UiKit.Panel(Root.transform, "Background", Color.white);
            UiKit.Stretch(_background);
            _background.GetComponent<Image>().raycastTarget = false;

            var veil = UiKit.Panel(Root.transform, "Veil", new Color(0f, 0f, 0f, 0.42f));
            UiKit.Stretch(veil);

            _titleText = UiKit.Label(Root.transform, "", 72f, Color.white, TextAlignmentOptions.Center, true);
            UiKit.SetRect(_titleText.gameObject, 260f, 230f, 1400f, 100f);
            _subtitleText = UiKit.Label(Root.transform, "", 34f, new Color(0.85f, 0.88f, 0.95f, 1f), TextAlignmentOptions.Center, true);
            UiKit.SetRect(_subtitleText.gameObject, 260f, 340f, 1400f, 60f);

            float y = 500f;
            MakeButton(UiKit.Ui(App.Content, "ui.new_case", "새 사건"), ref y, NewCase, null, 84f);
            _continueButton = MakeButton(UiKit.Ui(App.Content, "ui.continue", "이어하기"), ref y, Continue, null, 150f);
            _continueReason = UiKit.Label(Root.transform, "", 20f, new Color(1f, 0.8f, 0.6f, 1f), TextAlignmentOptions.Center, true);
            UiKit.SetRect(_continueReason.gameObject, 760f, y - 56f, 400f, 34f);

            _archiveButton = MakeButton(UiKit.Ui(App.Content, "ui.archive", "사건 기록"), ref y, OpenArchive, null, 150f);
            _archiveReason = UiKit.Label(Root.transform, "", 20f, new Color(1f, 0.8f, 0.6f, 1f), TextAlignmentOptions.Center, true);
            UiKit.SetRect(_archiveReason.gameObject, 760f, y - 56f, 400f, 34f);

            MakeButton(UiKit.Ui(App.Content, "ui.settings", "설정"), ref y, App.OpenSettings, null, 84f);
            MakeButton(UiKit.Ui(App.Content, "ui.quit", "종료"), ref y, UiKit.QuitGame, null, 84f);

            BuildConfirm();
        }

        private Button MakeButton(string label, ref float y, Action action, Color? color, float step)
        {
            var button = UiKit.TextButton(Root.transform, label, 28f, action, color, 320f, 64f);
            UiKit.SetRect(button.gameObject, 800f, y, 320f, 64f);
            y += step;
            return button;
        }

        private void BuildConfirm()
        {
            _confirm = UiKit.Dim(Root.transform, "Confirm");
            var box = UiKit.Panel(_confirm.transform, "Box", new Color(0.05f, 0.06f, 0.09f, 1f));
            UiKit.SetRect(box, 560f, 340f, 800f, 400f);
            _confirmBody = UiKit.Label(box.transform, "", 24f, Color.white, TextAlignmentOptions.TopLeft, true);
            UiKit.SetRect(_confirmBody.gameObject, 28f, 24f, 744f, 280f);
            var confirm = UiKit.TextButton(box.transform, UiKit.Ui(App.Content, "ui.confirm", "확인"), 25f, Confirm, new Color(0.16f, 0.3f, 0.22f, 0.96f), 180f, 56f);
            UiKit.SetRect(confirm.gameObject, 430f, 320f, 180f, 56f);
            var cancel = UiKit.TextButton(box.transform, UiKit.Ui(App.Content, "ui.cancel", "취소"), 25f, HideConfirm, null, 180f, 56f);
            UiKit.SetRect(cancel.gameObject, 620f, 320f, 180f, 56f);
            _confirmLabel = confirm.GetComponentInChildren<TextMeshProUGUI>();
            _confirm.SetActive(false);
        }

        public override void Refresh()
        {
            if (App.Session == null || App.Content == null) return;
            var state = App.Session.State;
            if (!_autoContinued && state.CurrentSceneId == "s000" && state.Phase == Phases.ReadyToExit
                && state.IsSceneCompleted("s000") && !state.IsSceneVisited("s010"))
            {
                _autoContinued = true;
                App.Exec(CommandKinds.ContinueScene);
                return;
            }
            var episode = App.Content.Episode;
            _titleText.text = episode != null && !string.IsNullOrEmpty(episode.title)
                ? episode.title
                : UiKit.Ui(App.Content, "ui.title", "AI는 거짓말을 하지 않는다");
            _subtitleText.text = episode != null && !string.IsNullOrEmpty(episode.subtitle)
                ? episode.subtitle
                : UiKit.Ui(App.Content, "ui.subtitle", "Episode 01 — 1.8초 전의 명령");

            var titleScene = App.Content.Scene("s000");
            string backgroundId = titleScene != null && !string.IsNullOrEmpty(titleScene.backgroundAssetId)
                ? titleScene.backgroundAssetId
                : "bg_bureau_night";
            var image = _background.GetComponent<Image>();
            image.sprite = Kit.Sprite(backgroundId);
            image.color = image.sprite != null ? Color.white : new Color(0.06f, 0.07f, 0.11f, 1f);
            UiKit.Cover(_background, 1920f, 1080f);

            bool canContinue = HasAnySave();
            _continueButton.interactable = canContinue;
            _continueReason.text = canContinue ? "" : UiKit.Ui(App.Content, "ui.empty_slot", "빈 슬롯");

            bool archiveOpen = App.Session.State.IsSceneCompleted("s710");
            _archiveButton.interactable = archiveOpen;
            _archiveReason.text = archiveOpen ? "" : UiKit.Ui(App.Content, "ui.archive_locked", "사건을 완료하면 열립니다.");
        }

        private bool HasAnySave()
        {
            var saves = App.Session.Saves;
            if (saves == null) return false;
            return saves.Exists("autosave") || saves.Exists("slot_01") || saves.Exists("slot_02") || saves.Exists("slot_03");
        }

        private void NewCase()
        {
            var body = new System.Text.StringBuilder();
            for (int i = 1; i <= 4; i++)
            {
                var line = App.Content.Ui("ui.content_warning_" + i);
                if (!string.IsNullOrEmpty(line) && line != "ui.content_warning_" + i) body.AppendLine("• " + line);
            }
            body.AppendLine();
            body.AppendLine(UiKit.Ui(App.Content, "ui.new_game_confirm", "새 사건을 시작할까요? 현재 자동 저장은 새 진행으로 대체됩니다."));
            ShowConfirm(body.ToString(), UiKit.Ui(App.Content, "ui.confirm", "확인"), StartNewGame);
        }

        private void StartNewGame()
        {
            App.Session.NewGame("autosave");
            int guard = 0;
            while (App.Session.State.Phase == Phases.PreDialogue && guard++ < 32)
                App.Exec(CommandKinds.AdvanceDialogue);
            App.Exec(CommandKinds.ContinueScene);
            App.Refresh();
        }

        private void Continue()
        {
            string slot;
            if (!FindLatestSlot(out slot))
            {
                App.Toast(UiKit.Ui(App.Content, "ui.empty_slot", "빈 슬롯"));
                return;
            }
            LoadOutcome outcome;
            if (App.Session.LoadGame(slot, out outcome))
            {
                App.Refresh();
                App.Toast(UiKit.Ui(App.Content, "ui.load_success", "불러왔습니다."));
                return;
            }
            ShowCorrupt(slot, outcome);
        }

        private bool FindLatestSlot(out string slot)
        {
            string[] slots = { "autosave", "slot_01", "slot_02", "slot_03" };
            DateTime best = DateTime.MinValue;
            slot = "";
            for (int i = 0; i < slots.Length; i++)
            {
                if (!App.Session.Saves.Exists(slots[i])) continue;
                var stamp = App.Session.Saves.ReadSlotTimestamp(slots[i]);
                DateTime parsed;
                if (!DateTime.TryParse(stamp, out parsed)) parsed = DateTime.MinValue;
                if (string.IsNullOrEmpty(slot) || parsed > best)
                {
                    best = parsed;
                    slot = slots[i];
                }
            }
            return !string.IsNullOrEmpty(slot);
        }

        private void ShowCorrupt(string slot, LoadOutcome outcome)
        {
            string message = SaveUi.IsIncompatible(outcome)
                ? UiKit.Ui(App.Content, "ui.save_incompatible", "이 저장은 현재 버전과 호환되지 않습니다. 원본은 보존됩니다.")
                : UiKit.Ui(App.Content, "ui.save_corrupt", "저장 파일이 손상되었습니다. 백업에서 복원할까요?");
            ShowConfirm(message, UiKit.Ui(App.Content, "ui.save_restore_backup", "백업에서 복원"), () => Restore(slot));
        }

        private void Restore(string slot)
        {
            LoadOutcome outcome;
            if (App.Session.LoadGame(slot, out outcome))
            {
                App.Refresh();
                App.Toast(UiKit.Ui(App.Content, "ui.load_success", "불러왔습니다."));
            }
            else
            {
                App.Toast(UiKit.Ui(App.Content, "ui.save_unknown_id", "저장에 알 수 없는 항목이 있습니다. 복원할 수 없습니다."));
            }
        }

        private void OpenArchive()
        {
            if (!App.Session.State.IsSceneCompleted("s710")) return;
            if (_archive == null)
            {
                _archive = new ArchiveScreen(App);
                _archive.OnExit = CloseArchive;
                _archive.Build();
            }
            Root.SetActive(false);
            _archive.SetVisible(true);
        }

        private void CloseArchive()
        {
            if (_archive != null) _archive.SetVisible(false);
            Root.SetActive(true);
            Refresh();
        }

        private void ShowConfirm(string message, string confirmLabel, Action action)
        {
            _pendingAction = action;
            _confirmBody.text = message;
            if (_confirmLabel != null) _confirmLabel.text = confirmLabel;
            _confirm.SetActive(true);
        }

        private void HideConfirm()
        {
            _pendingAction = null;
            _confirm.SetActive(false);
        }

        private void Confirm()
        {
            _confirm.SetActive(false);
            var action = _pendingAction;
            _pendingAction = null;
            if (action != null) action();
        }
    }
}
