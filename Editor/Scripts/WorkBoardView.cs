namespace WorkBoard {
    using System.Linq;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;

    public class WorkBoardView : GraphView {
        public event System.Action<GraphElement, Rect> onNodeAdded;
        public event System.Action<Edge> onEdgeAdded;
        public event System.Action<Group> onGroupAdded;
        public WorkBoardView() {
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new ContentZoomer());
            this.Insert(0, new GridBackground());

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
            pos = contentViewContainer.WorldToLocal(pos);
            foreach (var o in DragAndDrop.objectReferences) {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o))) continue;
                AddAssetNode(o, pos);
                pos += 50 * Vector2.up;
            }
            DragAndDrop.AcceptDrag();
        }

        private void AddAssetNode(Object asset, Vector2 position) {
            var node = new FileNode(new FileData() { asset = asset });
            OnCreateNode(node, position);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            evt.menu.AppendAction("Select Assets", SelectAssets);
            evt.menu.AppendAction("Create Group", CreateGroup);
            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
            evt.menu.AppendAction("Create Note", CreateNoteNode);
        }

        private void SelectAssets(DropdownMenuAction action) {
            Selection.objects = selection.Select(n => {
                if (n is FileNode f) {
                    return f.Data.asset;
                }

                return null;
            }).Where(a => a != null).ToArray();
        }

        private void CreateGroup(DropdownMenuAction action) {
            var group = new Group() { title = "New Group" };
            foreach (var sel in selection) {
                if (sel is Node n) {
                    group.AddElement(n);
                }
            }
            AddElement(group);
            onGroupAdded?.Invoke(group);
        }

        private void CreateNoteNode(DropdownMenuAction action) {
            var node = new NoteNode(new NoteData());
            OnCreateNode(node, action.eventInfo.mousePosition);
        }

        public void OnCreateNode<T>(T node, Vector2 position) where T : GraphElement {
            var rect = node.GetPosition();
            rect.position = position;
            node.SetPosition(rect);

            AddElement(node);

            onNodeAdded?.Invoke(node, rect);
        }

        internal void OnEdgeAdded(Edge edge) {
            onEdgeAdded?.Invoke(edge);
        }
    }
}