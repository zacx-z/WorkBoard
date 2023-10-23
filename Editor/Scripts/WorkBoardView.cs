namespace WorkBoard {
    using System.Linq;
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
            foreach (var p in DragAndDrop.paths) {
                AddAssetNode(AssetDatabase.LoadAssetAtPath<Object>(p), pos);
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
                    return f.data.asset;
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
        }

        private void CreateNoteNode(DropdownMenuAction action) {
            var node = new NoteNode(new NoteData());
            OnCreateNode(node, action.eventInfo.mousePosition);
        }

        public void OnCreateNode<T>(T node, Vector2 position) where T : BoardNode {
            var rect = node.GetPosition();
            rect.position = position;
            node.SetPosition(rect);

            AddElement(node);

            onNodeAdded?.Invoke(node, rect);
        }
    }
}