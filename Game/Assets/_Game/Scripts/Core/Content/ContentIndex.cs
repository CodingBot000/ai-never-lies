using System;
using System.Collections.Generic;
using ANL.Core.Contracts;

namespace ANL.Core.Content
{
    public interface IContentIndex
    {
        EpisodeDefinitionDto Episode { get; }
        IReadOnlyDictionary<string, SceneDefinitionDto> Scenes { get; }
        IReadOnlyDictionary<string, DialogueLineDto> Dialogues { get; }
        IReadOnlyDictionary<string, EvidenceDefinitionDto> Evidence { get; }
        IReadOnlyDictionary<string, TestimonyStatementDto> Testimonies { get; }
        IReadOnlyDictionary<string, BranchDefinitionDto> Branches { get; }
        IReadOnlyDictionary<string, PuzzleDefinitionDto> Puzzles { get; }
        IReadOnlyDictionary<string, HintDefinitionDto> Hints { get; }
        IReadOnlyDictionary<string, UiStringDto> UiStrings { get; }
        GraphFileDto Graph { get; }
        MatrixFileDto Matrix { get; }
        IReadOnlyDictionary<string, string> Text { get; }

        SceneDefinitionDto Scene(string id);
        DialogueLineDto Dialogue(string id);
        EvidenceDefinitionDto EvidenceDef(string id);
        TestimonyStatementDto Testimony(string id);
        BranchDefinitionDto Branch(string id);
        PuzzleDefinitionDto Puzzle(string id);
        HintDefinitionDto Hint(string id);
        string Ui(string id);
        string TextOf(string id);
        List<DialogueLineDto> SceneDialogue(string sceneId, string phase);
        List<BranchDefinitionDto> BranchGroup(string groupId);
        List<TestimonyStatementDto> SceneTestimonies(string sceneId);
        List<GraphNodeDto> GraphNodes();
        List<EdgeDto> GraphEdges(bool causal);
        int EventIndex(string eventId);
        bool HasEvidence(string id);
        bool HasScene(string id);
        bool HasPuzzle(string id);
    }

    public sealed class ContentIndex : IContentIndex
    {
        private readonly Dictionary<string, SceneDefinitionDto> _scenes = new Dictionary<string, SceneDefinitionDto>();
        private readonly Dictionary<string, DialogueLineDto> _dialogues = new Dictionary<string, DialogueLineDto>();
        private readonly Dictionary<string, EvidenceDefinitionDto> _evidence = new Dictionary<string, EvidenceDefinitionDto>();
        private readonly Dictionary<string, TestimonyStatementDto> _testimonies = new Dictionary<string, TestimonyStatementDto>();
        private readonly Dictionary<string, BranchDefinitionDto> _branches = new Dictionary<string, BranchDefinitionDto>();
        private readonly Dictionary<string, PuzzleDefinitionDto> _puzzles = new Dictionary<string, PuzzleDefinitionDto>();
        private readonly Dictionary<string, HintDefinitionDto> _hints = new Dictionary<string, HintDefinitionDto>();
        private readonly Dictionary<string, UiStringDto> _ui = new Dictionary<string, UiStringDto>();
        private readonly Dictionary<string, string> _text = new Dictionary<string, string>();
        private readonly List<GraphNodeDto> _nodes = new List<GraphNodeDto>();
        private readonly List<EdgeDto> _causal = new List<EdgeDto>();
        private readonly List<EdgeDto> _signal = new List<EdgeDto>();
        private readonly List<EventLogDto> _events = new List<EventLogDto>();
        private readonly List<MatrixRowDto> _matrixRows = new List<MatrixRowDto>();

        public EpisodeDefinitionDto Episode { get; private set; }
        public GraphFileDto Graph { get; private set; }
        public MatrixFileDto Matrix { get; private set; }

        public IReadOnlyDictionary<string, SceneDefinitionDto> Scenes => _scenes;
        public IReadOnlyDictionary<string, DialogueLineDto> Dialogues => _dialogues;
        public IReadOnlyDictionary<string, EvidenceDefinitionDto> Evidence => _evidence;
        public IReadOnlyDictionary<string, TestimonyStatementDto> Testimonies => _testimonies;
        public IReadOnlyDictionary<string, BranchDefinitionDto> Branches => _branches;
        public IReadOnlyDictionary<string, PuzzleDefinitionDto> Puzzles => _puzzles;
        public IReadOnlyDictionary<string, HintDefinitionDto> Hints => _hints;
        public IReadOnlyDictionary<string, UiStringDto> UiStrings => _ui;
        public IReadOnlyDictionary<string, string> Text => _text;
        public List<MatrixRowDto> MatrixRows => _matrixRows;

        public ContentIndex(
            EpisodeDefinitionDto episode,
            IEnumerable<SceneDefinitionDto> scenes,
            IEnumerable<DialogueLineDto> dialogues,
            IEnumerable<EvidenceDefinitionDto> evidence,
            IEnumerable<TestimonyStatementDto> testimonies,
            IEnumerable<BranchDefinitionDto> branches,
            IEnumerable<PuzzleDefinitionDto> puzzles,
            IEnumerable<HintDefinitionDto> hints,
            IEnumerable<UiStringDto> uiStrings,
            GraphFileDto graph,
            MatrixFileDto matrix)
        {
            Episode = episode;
            Graph = graph;
            Matrix = matrix;
            AddAll(_scenes, scenes, s => s.id);
            AddAll(_dialogues, dialogues, d => d.id);
            AddAll(_evidence, evidence, e => e.id);
            AddAll(_testimonies, testimonies, t => t.id);
            AddAll(_branches, branches, b => b.id);
            AddAll(_puzzles, puzzles, p => p.id);
            AddAll(_hints, hints, h => h.id);
            AddAll(_ui, uiStrings, u => u.id);
            if (graph != null)
            {
                if (graph.nodes != null) _nodes.AddRange(graph.nodes);
                if (graph.causalEdges != null) _causal.AddRange(graph.causalEdges);
                if (graph.signalEdges != null) _signal.AddRange(graph.signalEdges);
                if (graph.eventLog != null) _events.AddRange(graph.eventLog);
            }
            if (matrix != null && matrix.rows != null) _matrixRows.AddRange(matrix.rows);
            BuildTextMap();
        }

        private static void AddAll<T>(Dictionary<string, T> target, IEnumerable<T> source, Func<T, string> key)
        {
            if (source == null) return;
            foreach (var item in source)
            {
                if (item == null) continue;
                var k = key(item);
                if (!string.IsNullOrEmpty(k)) target[k] = item;
            }
        }

        private void BuildTextMap()
        {
            foreach (var pair in _dialogues) if (!string.IsNullOrEmpty(pair.Value.text)) _text[pair.Value.textId] = pair.Value.text;
            foreach (var pair in _evidence)
            {
                var e = pair.Value;
                Put(e.titleTextId, e.title); Put(e.sourceTextId, e.source); Put(e.bodyTextId, e.body);
                Put(e.provesTextId, e.proves); Put(e.limitsTextId, e.limits);
            }
            foreach (var pair in _testimonies) if (!string.IsNullOrEmpty(pair.Value.text)) _text[pair.Value.textId] = pair.Value.text;
            foreach (var pair in _hints) if (!string.IsNullOrEmpty(pair.Value.text)) _text[pair.Value.textId] = pair.Value.text;
            foreach (var pair in _ui) if (!string.IsNullOrEmpty(pair.Value.text)) _text[pair.Value.id] = pair.Value.text;
        }

        private void Put(string id, string text)
        {
            if (!string.IsNullOrEmpty(id)) _text[id] = text ?? "";
        }

        public SceneDefinitionDto Scene(string id) => Lookup(_scenes, id);
        public DialogueLineDto Dialogue(string id) => Lookup(_dialogues, id);
        public EvidenceDefinitionDto EvidenceDef(string id) => Lookup(_evidence, id);
        public TestimonyStatementDto Testimony(string id) => Lookup(_testimonies, id);
        public BranchDefinitionDto Branch(string id) => Lookup(_branches, id);
        public PuzzleDefinitionDto Puzzle(string id) => Lookup(_puzzles, id);
        public HintDefinitionDto Hint(string id) => Lookup(_hints, id);
        public string Ui(string id) => _ui.TryGetValue(id ?? "", out var u) ? u.text : (id ?? "");
        public string TextOf(string id) => _text.TryGetValue(id ?? "", out var t) ? t : "";
        public bool HasEvidence(string id) => !string.IsNullOrEmpty(id) && _evidence.ContainsKey(id);
        public bool HasScene(string id) => !string.IsNullOrEmpty(id) && _scenes.ContainsKey(id);
        public bool HasPuzzle(string id) => !string.IsNullOrEmpty(id) && _puzzles.ContainsKey(id);

        private static T Lookup<T>(Dictionary<string, T> map, string id) where T : class
        {
            if (string.IsNullOrEmpty(id)) return null;
            return map.TryGetValue(id, out var value) ? value : null;
        }

        public List<DialogueLineDto> SceneDialogue(string sceneId, string phase)
        {
            var result = new List<DialogueLineDto>();
            var scene = Scene(sceneId);
            if (scene == null) return result;
            var ids = phase == "pre" ? scene.preDialogueIds : scene.postDialogueIds;
            if (ids == null) return result;
            for (int i = 0; i < ids.Length; i++)
            {
                var line = Dialogue(ids[i]);
                if (line != null) result.Add(line);
            }
            return result;
        }

        public List<BranchDefinitionDto> BranchGroup(string groupId)
        {
            var result = new List<BranchDefinitionDto>();
            foreach (var pair in _branches)
                if (pair.Value.groupId == groupId) result.Add(pair.Value);
            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        public List<TestimonyStatementDto> SceneTestimonies(string sceneId)
        {
            var result = new List<TestimonyStatementDto>();
            foreach (var pair in _testimonies)
                if (pair.Value.sceneId == sceneId) result.Add(pair.Value);
            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        public List<GraphNodeDto> GraphNodes() => _nodes;
        public List<EdgeDto> GraphEdges(bool causal) => causal ? _causal : _signal;

        public int EventIndex(string eventId)
        {
            for (int i = 0; i < _events.Count; i++)
                if (_events[i].id == eventId) return i;
            return -1;
        }

        public EventLogDto EventAt(int index) => index >= 0 && index < _events.Count ? _events[index] : null;
        public int EventCount => _events.Count;
    }
}
