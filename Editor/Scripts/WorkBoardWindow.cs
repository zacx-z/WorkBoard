using UnityEngine.UIElements;

namespace WorkBoard {
    using System;
    using System.Collections.Generic;
    using UnityEngine;
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
                var win = GetWindow<WorkBoardWindow>();
                win.Show();
                win.SetTarget(data);
                return true;
            }

            return false;
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

        private void OnEnable() {
            titleContent = new GUIContent("WorkBoard");

            _graphView = new WorkBoardView()
            {
                style = { flexGrow = 1 }
            };
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(SaveChanges) { text = "Save" });
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
        }

        private void OnDisable() {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo() {
            _graphView.graphViewChanged -= OnGraphChanged;
            ClearGraph();
            RebuildGraph();
            _graphView.graphViewChanged += OnGraphChanged;
        }

        private void SetTarget(WorkBoardData data) {
            if (data == target) return;
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
        }

        private void ClearGraph() {
            _graphView.DeleteElements(_graphView.graphElements);
            _dataMap.Clear();
        }

        private void RebuildGraph() {
            try {
                if (nodeData != null) {
                    var nodeMap = new Dictionary<BoardNodeData, IBoardNode>();

                    foreach (var n in nodeData) {
                        var node = BoardNode.Create(n.data);
                        node.SetPosition(n.position);
                        _graphView.AddElement(node);
                        _dataMap[node] = n;
                        nodeMap[n.data] = node;
                    }

                    if (edgeData != null) {
                        foreach (var e in edgeData) {
                            nodeMap[e.from].ConnectChild(nodeMap[e.to]);
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
                if (node is IBoardNode bn)
                    bn.SubscribeWillChange(OnNodeWillChange);
            }
        }

        private void OnNodeAdded(GraphElement elem, Rect pos) {
            Undo.RegisterCompleteObjectUndo(this, "WorkBoard Add Node");
            hasUnsavedChanges = true;

            if (elem is IBoardNode node) {
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
            hasUnsavedChanges = true;
            Undo.RegisterCompleteObjectUndo(this, "Work Board Change Node");
            EditorUtility.SetDirty(this);
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change) {
            hasUnsavedChanges = true;
            Undo.RegisterCompleteObjectUndo(this, "WorkBoard Change Graph");
            if (change.movedElements != null) {
                foreach (var moved in change.movedElements) {
                    if (moved is Node node) {
                        if (_dataMap.TryGetValue(node, out var data)) {
                            data.position = moved.GetPosition();
                        }
                    }
                }
            }

            if (change.elementsToRemove != null) {
                foreach (var deleted in change.elementsToRemove) {
                    if (deleted is Node node) {
                        if (_dataMap.TryGetValue(node, out var data)) {
                            _dataMap.Remove(node);
                            nodeData.Remove(data);
                        }
                    }

                    if (deleted is Edge edge) {
                        var fromData = (edge.output.node as IBoardNode).Data;
                        var toData = (edge.input.node as IBoardNode).Data;
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
                from = (edge.output.node as IBoardNode).Data,
                to = (edge.input.node as IBoardNode).Data,
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

        public override void SaveChanges() {
            if (target == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target))) {
                var path = EditorUtility.SaveFilePanelInProject("Save Work Board", "WorkBoard", "asset", "Save");
                target = CreateInstance<WorkBoardData>();
                SaveToTarget();
                AssetDatabase.CreateAsset(target, path);
            } else {
                SaveToTarget();
            }
            base.SaveChanges();
        }

        private void SaveToTarget() {
            target.nodeData = CloneData(nodeData);

            target.edgeData = new List<WorkBoardData.EdgeData>();
            foreach (var edge in edgeData) {
                target.edgeData.Add(new WorkBoardData.EdgeData()
                {
                    fromIndex = nodeData.FindIndex(n => n.data == edge.from),
                    toIndex = nodeData.FindIndex(n => n.data == edge.to),
                });
            }

            target.groupData = new List<WorkBoardData.GroupData>();
            foreach (var group in groupData) {
                var data = new WorkBoardData.GroupData()
                {
                    title = group.title,
                    containedNodes = group.containedNodes.Select(node => nodeData.FindIndex(n => n.data == node)).ToArray()
                };
                target.groupData.Add(data);
            }

            EditorUtility.SetDirty(target);
        }

        private static List<NodeData> CloneData(List<NodeData> data) {
            var cloned = new List<NodeData>(data.Count);
            foreach (var n in data) {
                cloned.Add(n.CloneData());
            }

            return cloned;
        }

        private static List<BoardNodeData> CollectContainedNodes(Group group) {
            return group.containedElements.Cast<IBoardNode>().Where(n => n != null).Select(n => n.Data).ToList();
        }
    }
}
