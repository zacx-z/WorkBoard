namespace WorkBoard {
    using System;
    using UnityEngine.UIElements;
    using UnityEditor.Experimental.GraphView;

    [BoardElementDataType(typeof(NoteData))]
    public class NoteNode : StickyNote, IBoardElement {
        private readonly NoteData _data;
        public BoardNodeData Data => _data;
        public event Action onWillChange;

        public void SubscribeWillChange(Action listener) {
            onWillChange += listener;
        }

        public NoteNode(NoteData data) {
            _data = data;
            this.contents = data.note;
            this.Q<TextField>("contents-field").RegisterCallback<ChangeEvent<string>>(OnTextChanged);
        }

        private void OnTextChanged(ChangeEvent<string> evt) {
            _data.note = evt.newValue;
            onWillChange?.Invoke();
        }
    }
}