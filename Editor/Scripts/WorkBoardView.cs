namespace WorkBoard {
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;

    public class WorkBoardView : GraphView {
        public event System.Action<GraphElement, Rect> onNodeAdded;
        public WorkBoardView() {
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());

            this.RegisterCallback<DragEnterEvent>(OnDragEnter);
            this.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            this.RegisterCallback<DragPerformEvent>(OnDragPerformed);
        }

        private void OnDragEnter(DragEnterEvent e) {
        }

        private void OnDragUpdated(DragUpdatedEvent e) {
            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
        }

        private void OnDragPerformed(DragPerformEvent e) {
            var pos = e.localMousePosition;
            foreach (var p in DragAndDrop.paths) {
                AddAssetNode(AssetDatabase.LoadAssetAtPath<Object>(p), pos);
                pos += 50 * Vector2.down;
            }
            DragAndDrop.AcceptDrag();
        }

        private void AddAssetNode(Object asset, Vector2 position) {
            var node = new FileNode(new FileData() { asset = asset });
            OnCreateNode(node, position);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            base.BuildContextualMenu(evt);
            evt.menu.AppendAction("Create Note", CreateNoteNode);
            evt.menu.AppendAction("Select Assets", SelectAssets);
        }

        private void SelectAssets(DropdownMenuAction action) {
            foreach (var n in selection) {
                if (n is FileNode f) {
                    Debug.Log(n);
                }
            }
        }

        private void CreateNoteNode(DropdownMenuAction action) {
            var node = new NoteNode(new NoteData());
            OnCreateNode(node, action.eventInfo.mousePosition);
        }

        private void OnCreateNode<T>(T node, Vector2 position) where T : BoardNode {
            var rect = node.GetPosition();
            rect.position = position;
            node.SetPosition(rect);

            AddElement(node);

            onNodeAdded?.Invoke(node, rect);
        }
    }
}