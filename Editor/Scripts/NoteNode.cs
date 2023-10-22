namespace WorkBoard {
    using UnityEngine;
    using UnityEngine.UIElements;

    public class NoteNode : BoardNode<NoteData> {
        public NoteNode(NoteData data) : base(data) {
            this.title = "Note";
            var field = new TextField()
            {
                style = { flexGrow = 1, minHeight = 150, minWidth = 150 },
                multiline = true,
                value = data.note
            };
            this.titleContainer.style.height = 16;
            this.mainContainer.style.backgroundColor = new StyleColor(Color.yellow);
            this.extensionContainer.Add(field);
            expanded = true;
            RefreshExpandedState();
            
            field.RegisterCallback<ChangeEvent<string>>(OnTextChanged);
        }

        private void OnTextChanged(ChangeEvent<string> evt) {
            data.note = evt.newValue;
        }
    }
}