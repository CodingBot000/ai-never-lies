using System;
using ANL.Core.Commands;
using ANL.Core.Content;
using ANL.Core.Contracts;
using ANL.Core.Hints;
using ANL.Core.State;
using ANL.Runtime.Diagnostics;
using ANL.Runtime.Persistence;

namespace ANL.Runtime.Presentation
{
    public sealed class GameSession
    {
        private readonly ContentIndex _content;
        private int _commandCounter;

        public ContentIndex Content => _content;
        public GameState State { get; private set; }
        public CommandDispatcher Dispatcher { get; private set; }
        public SaveService Saves { get; }
        public HintService Hints { get; }
        public SettingsData Settings { get; private set; }
        public string ContentVersion { get; }
        public string ArtManifestSha256 { get; }
        public string BlockingError { get; private set; } = "";

        public event Action<CommandResult> Executed;

        public GameSession(ContentIndex content, SaveService saves, string contentVersion, string artManifestSha256)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            ContentVersion = contentVersion ?? "";
            ArtManifestSha256 = artManifestSha256 ?? "";
            Settings = Saves.LoadSettings();
            Hints = new HintService(_content);
            State = new GameState();
            Dispatcher = new CommandDispatcher(_content, State);
            WireDispatcher();
        }

        public long NextSequence => State.LastCommandSequence + 1;

        public CommandResult Execute(string type, string targetId = null, string[] ids = null, string slot = null, string value = null, double numeric = 0)
        {
            return ExecuteOrigin(Origins.User, type, targetId, ids, slot, value, numeric);
        }

        public CommandResult ExecuteOrigin(string origin, string type, string targetId = null, string[] ids = null, string slot = null, string value = null, double numeric = 0)
        {
            var command = new GameCommand
            {
                commandId = "cmd_" + (++_commandCounter),
                sequence = NextSequence,
                sceneId = State.CurrentSceneId,
                origin = string.IsNullOrEmpty(origin) ? Origins.User : origin,
                type = type,
                targetId = targetId,
                ids = ids,
                slot = slot,
                value = value,
                numeric = numeric
            };

            var result = Dispatcher.Execute(command);
            HandleCheckpoint(result);
            Executed?.Invoke(result);
            return result;
        }

        public void NewGame(string slotId)
        {
            State = new GameState();
            Dispatcher = new CommandDispatcher(_content, State);
            WireDispatcher();
            _commandCounter = 0;
            BlockingError = "";

            var result = Execute(CommandKinds.StartEpisode);
            if (result != null && result.status != ResultStatus.Rejected && !string.IsNullOrEmpty(slotId))
                SaveManual(slotId);
        }

        public bool LoadGame(string slotId, out LoadOutcome outcome)
        {
            outcome = Saves.Load(slotId, out var snapshot);
            if (outcome != LoadOutcome.Ok || snapshot == null) return false;

            if (!string.IsNullOrEmpty(snapshot.currentSceneId) && !_content.HasScene(snapshot.currentSceneId))
            {
                BlockingError = "save references unknown scene: " + snapshot.currentSceneId;
                Diag.Error(BlockingError);
                outcome = LoadOutcome.UnknownIds;
                return false;
            }
            if (!string.IsNullOrEmpty(snapshot.activePuzzleId) && !_content.HasPuzzle(snapshot.activePuzzleId))
            {
                BlockingError = "save references unknown puzzle: " + snapshot.activePuzzleId;
                Diag.Error(BlockingError);
                outcome = LoadOutcome.UnknownIds;
                return false;
            }

            State = GameState.FromSnapshot(snapshot);
            Dispatcher = new CommandDispatcher(_content, State);
            WireDispatcher();
            _commandCounter = 0;
            BlockingError = "";
            Executed?.Invoke(null);
            return true;
        }

        public void AutoSave()
        {
            if (!Saves.Save(State, "autosave")) Diag.Warn("GameSession: autosave failed");
        }

        public bool SaveManual(string slotId)
        {
            return Saves.Save(State, slotId);
        }

        public void ApplySettings(SettingsData s)
        {
            if (s == null) return;
            Settings = s;
            Saves.SaveSettings(s);
        }

        private void WireDispatcher()
        {
            Dispatcher.SaveRequested = (slot, _) => SaveManual(slot);
            Dispatcher.LoadRequested = slot => LoadGame(slot, out _);
        }

        private void HandleCheckpoint(CommandResult result)
        {
            if (result == null || result.status == ResultStatus.Rejected) return;

            bool checkpoint = result.reasonCode == "branch_advanced";
            if (!checkpoint && result.events != null)
            {
                for (int i = 0; i < result.events.Count; i++)
                {
                    var kind = result.events[i] != null ? result.events[i].kind : null;
                    if (kind == "evidence_granted" || kind == "puzzle_solved" || kind == "scene_completed")
                    {
                        checkpoint = true;
                        break;
                    }
                }
            }

            if (checkpoint) AutoSave();
        }
    }
}
