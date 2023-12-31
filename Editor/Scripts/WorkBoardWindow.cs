#nullable disable

namespace WorkBoard {
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEditor.Callbacks;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor.UIElements;
    using System.Linq;
    using NodeData = WorkBoardData.NodeData;

    [Serializable]
    public class WorkBoardWindow : EditorWindow {
        [MenuItem("Tools/WorkBoard/Open")]
        public static void OpenWindow() {
            GetWindow<WorkBoardWindow>().Show();
        }

        [OnOpenAsset]
        public static bool OnOpenBoard(int instanceID) {
            var o = EditorUtility.InstanceIDToObject(instanceID);
            if (o is WorkBoardData data) {
                OpenBoard(data);
                return true;
            }

            return false;
        }

        public static void OpenBoard(WorkBoardData target) {
            var win = GetWindow<WorkBoardWindow>();
            win.Show();
            win.SetTarget(target);
        }

        public static void OpenBoardInNewWindow(WorkBoardData target) {
            var win = CreateWindow<WorkBoardWindow>(typeof(WorkBoardWindow));
            win.Show();
            win.SetTarget(target);
            win.Focus();
        }

        [Serializable]
        private struct EdgeData {
            [SerializeField, SerializeReference]
            public BoardNodeData from;
            [SerializeField, SerializeReference]
            public BoardNodeData to;
        }

        [Serializable]
        private class GroupData {
            [SerializeField]
            public string title;
            [SerializeField, SerializeReference]
            public List<BoardNodeData> containedNodes;
        }

        [SerializeField]
        private List<NodeData> nodeData;
        [SerializeField]
        private List<EdgeData> edgeData;
        [SerializeField]
        private List<GroupData> groupData;

        [SerializeField]
        private WorkBoardData target;

        private Dictionary<GraphElement, NodeData> _dataMap;
        private Dictionary<Group, GroupData> _groupMap;
        private WorkBoardView _graphView;
        private ToolbarButton _selectButton;
        private ToolbarButton _saveButton;

        private void OnEnable() {
            titleContent = new GUIContent(target == null ? "New WorkBoard" : target.name);

            _graphView = new WorkBoardView()
            {
                style = { flexGrow = 1 }
            };
            var toolbar = new Toolbar();
            toolbar.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.nelasystem.workboard/Editor/StyleSheets/Toolbar.uss"));
            toolbar.Add(new ToolbarButton(OnOpenMenu) { text = "Open ..." });
#if UNITY_2021_2_OR_NEWER
            toolbar.Add(new UnityEditor.Toolbars.EditorToolbarDropdown(OpenCreateMenu) { text = "Create" });
#else
            toolbar.Add(new ToolbarButton(OpenCreateMenu) { text = "Create ▾" });
#endif
            toolbar.Add(new VisualElement() { style = { flexGrow = 1 }});
            _selectButton = new ToolbarButton(SelectBoardAsset) { text = "Select Board" };
            toolbar.Add(_selectButton);
            toolbar.Add(_saveButton = new ToolbarButton(SaveChanges) { text = "Save" });
            rootVisualElement.Add(toolbar);
            rootVisualElement.Add(_graphView);

            _dataMap = new Dictionary<GraphElement, NodeData>();
            RebuildGraph();

            Undo.undoRedoPerformed += OnUndoRedo;
            _graphView.graphViewChanged += OnGraphChanged;
            _graphView.onNodeAdded += OnNodeAdded;
            _graphView.onEdgeAdded += OnEdgeAdded;
            _graphView.onGroupAdded += OnGroupAdded;
            _graphView.groupTitleChanged = OnGroupTitleChanged;
            _graphView.elementsAddedToGroup += OnElementsAddedToGroup;
            _graphView.elementsRemovedFromGroup += OnElementsRemovedFromGroup;

            _selectButton.SetEnabled(target != null);
        }

        private void OnDisable() {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnGUI() {
            if (Event.current.type == EventType.ExecuteCommand) {
                if (Event.current.commandName == "ObjectSelectorClosed") {
                    var obj = EditorGUIUtility.GetObjectPickerObject();
                    if (obj is WorkBoardData board) {
                        SetTarget(board);
                    }
                }
            }
        }

        private void OnUndoRedo() {
            _graphView.graphViewChanged -= OnGraphChanged;
            ClearGraph();
            RebuildGraph();
            _graphView.graphViewChanged += OnGraphChanged;
        }

        private void SetTarget(WorkBoardData data) {
            if (data == target) return;

            if (hasUnsavedChanges) {
                var res = EditorUtility.DisplayDialogComplex("Unsaved Changes Detected", saveChangesMessage, "Save",
                    "Cancel", "Discard");
                if (res == 0)
                    SaveChanges();
                if (res == 1)
                    return;
            }

            target = data;
            _graphView.graphViewChanged -= OnGraphChanged;
            ClearGraph();

            // load
            nodeData = CloneData(data.nodeData);
            edgeData = new List<EdgeData>();
            foreach (var edge in data.edgeData) {
                edgeData.Add(new EdgeData()
                {
                    from = nodeData[edge.fromIndex].data,
                    to = nodeData[edge.toIndex].data
                });
            }

            groupData = new List<GroupData>();

            foreach (var group in data.groupData) {
                groupData.Add(new GroupData()
                {
                    title = group.title,
                    containedNodes = group.containedNodes.Select(i => nodeData[i].data).ToList()
                });
            }

            RebuildGraph();

            _graphView.graphViewChanged += OnGraphChanged;

            OnTargetChanged();
        }

        private void OnTargetChanged() {
            if (target != null) titleContent = new GUIContent(target.name);
            _selectButton.SetEnabled(target != null);
            hasUnsavedChanges = false;
            _saveButton.SetEnabled(false);
        }

        private void ClearGraph() {
#if UNITY_2021_3_OR_NEWER
            _graphView.DeleteElements(_graphView.graphElements);
#else
            _graphView.DeleteElements(_graphView.graphElements.ToList());
#endif
            _dataMap.Clear();
        }

        private void RebuildGraph() {
            try {
                if (nodeData != null) {
                    var nodeMap = new Dictionary<BoardNodeData, IBoardElement>();

                    foreach (var n in nodeData) {
                        var node = BoardNode.Create(n.data);
                        var ge = (GraphElement)node;
                        ge.SetPosition(n.position);
                        _graphView.AddElement(ge);
                        _dataMap[ge] = n;
                        nodeMap[n.data] = node;
                    }

                    if (edgeData != null) {
                        foreach (var e in edgeData) {
                            (nodeMap[e.from] as IBoardNode).ConnectChild(nodeMap[e.to] as IBoardNode);
                        }
                    }

                    if (groupData != null) {
                        _groupMap = new Dictionary<Group, GroupData>();
                        foreach (var g in groupData) {
                            var group = new Group
                            {
                                title = g.title
                            };
                            foreach (var n in g.containedNodes) {
                                group.AddElement(nodeMap[n] as GraphElement);
                            }
                            _graphView.AddElement(group);
                            _groupMap[group] = g;
                        }
                    }
                }
            }
            catch (Exception e) {
                Debug.LogException(e);
                if (nodeData != null) nodeData.Clear();
            }

            foreach (var node in _dataMap.Keys) {
                if (node is IBoardElement bn)
                    bn.SubscribeWillChange(OnNodeWillChange);
            }
        }

        private void OnNodeAdded(GraphElement elem, Rect pos) {
            Undo.RegisterCompleteObjectUndo(this, "WorkBoard Add Node");
            MarkChanged();

            if (elem is IBoardElement node) {
                nodeData ??= new List<NodeData>();
                var data = new NodeData()
                {
                    data = node.Data,
                    position = pos
                };
                nodeData.Add(data);
                _dataMap[node as GraphElement] = data;

                node.SubscribeWillChange(OnNodeWillChange);
            }
            EditorUtility.SetDirty(this);
        }

        private void OnNodeWillChange() {
            MarkChanged();
            Undo.RegisterCompleteObjectUndo(this, "Work Board Change Node");
            EditorUtility.SetDirty(this);
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change) {
            MarkChanged();
            Undo.RegisterCompleteObjectUndo(this, "WorkBoard Change Graph");
            if (change.movedElements != null) {
                foreach (var moved in change.movedElements) {
                    if (_dataMap.TryGetValue(moved, out var data)) {
                        data.position = moved.GetPosition();
                    }
                }
            }

            if (change.elementsToRemove != null) {
                foreach (var deleted in change.elementsToRemove) {
                    {
                        if (_dataMap.TryGetValue(deleted, out var data))
                        {
                            _dataMap.Remove(deleted);
                            nodeData.Remove(data);
                        }
                    }

                    if (deleted is Edge edge) {
                        var fromData = (edge.output.node as IBoardElement).Data;
                        var toData = (edge.input.node as IBoardElement).Data;
                        edgeData.RemoveAll(d => d.from == fromData && d.to == toData);
                    }

                    if (deleted is Group group) {
                        var data = _groupMap[group];
                        groupData.Remove(data);
                        _groupMap.Remove(group);
                    }
                }
            }

            if (change.edgesToCreate != null) {
                edgeData ??= new List<EdgeData>();
                foreach (var edge in change.edgesToCreate) {
                    OnEdgeAdded(edge);
                }
            }

            return change;
        }

        private void OnEdgeAdded(Edge edge) {
            edgeData ??= new List<EdgeData>();
            edgeData.Add(new EdgeData()
            {
                from = (edge.output.node as IBoardElement).Data,
                to = (edge.input.node as IBoardElement).Data,
            });
        }

        private void OnGroupAdded(Group group) {
            Undo.RegisterCompleteObjectUndo(this, "Add Group");
            groupData ??= new List<GroupData>();
            var data = new GroupData()
            {
                title = group.title,
                containedNodes = CollectContainedNodes(group)
            };
            groupData.Add(data);

            _groupMap ??= new Dictionary<Group, GroupData>();
            _groupMap[group] = data;
        }

        private void OnGroupTitleChanged(Group group, string str) {
            _groupMap[group].title = str;
        }

        private void OnElementsAddedToGroup(Group group, IEnumerable<GraphElement> elems) {
            _groupMap[group].containedNodes = CollectContainedNodes(group);
        }

        private void OnElementsRemovedFromGroup(Group group, IEnumerable<GraphElement> elems) {
            _groupMap[group].containedNodes = CollectContainedNodes(group);
        }

        private void OnOpenMenu() {
            EditorGUIUtility.ShowObjectPicker<WorkBoardData>(null, false, "", 0);
        }
        
        private void OpenCreateMenu() {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Note"), false, () => _graphView.CreateNoteNode());
            menu.AddItem(new GUIContent("Label"), false, () => _graphView.CreateLabelNode());
            menu.ShowAsContext();
        }

        private void SelectBoardAsset() {
            Selection.activeObject = target;
        }

        private void MarkChanged() {
            hasUnsavedChanges = true;
            _saveButton.SetEnabled(true);
        }

        public override void SaveChanges() {
            if (target == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target))) {
                var path = EditorUtility.SaveFilePanelInProject("Save Work Board", "WorkBoard", "asset", "Save");
                target = CreateInstance<WorkBoardData>();
                SaveToTarget();
                AssetDatabase.CreateAsset(target, path);
                OnTargetChanged();
            } else {
                SaveToTarget();
            }
            base.SaveChanges();
            _saveButton.SetEnabled(false);
        }

        private void SaveToTarget() {
            target.nodeData = CloneData(nodeData);

            target.edgeData = new List<WorkBoardData.EdgeData>();
            if (edgeData != null) {
                foreach (var edge in edgeData) {
                    target.edgeData.Add(new WorkBoardData.EdgeData() {
                        fromIndex = nodeData.FindIndex(n => n.data == edge.from),
                        toIndex = nodeData.FindIndex(n => n.data == edge.to),
                    });
                }
            }

            target.groupData = new List<WorkBoardData.GroupData>();
            if (groupData != null) {
                foreach (var group in groupData) {
                    var data = new WorkBoardData.GroupData() {
                        title = group.title,
                        containedNodes = group.containedNodes
                            .Select(node => nodeData.FindIndex(n => n.data == node)).ToArray()
                    };
                    target.groupData.Add(data);
                }
            }

            EditorUtility.SetDirty(target);
        }

        public override IEnumerable<Type> GetExtraPaneTypes() {
            yield return typeof(WorkBoardWindow);
        }

        private static List<NodeData> CloneData(List<NodeData> data) {
            var cloned = new List<NodeData>(data.Count);
            foreach (var n in data) {
                cloned.Add(n.CloneData());
            }

            return cloned;
        }

        private static List<BoardNodeData> CollectContainedNodes(Group group) {
            return group.containedElements.Cast<IBoardElement>().Where(n => n != null).Select(n => n.Data).ToList();
        }
    }
}
