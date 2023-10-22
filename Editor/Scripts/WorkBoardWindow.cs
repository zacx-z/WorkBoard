namespace WorkBoard {
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.Callbacks;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor.UIElements;
    using NodeData = WorkBoardData.NodeData;

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

        [SerializeField]
        private List<NodeData> nodeData;

        [SerializeField]
        private WorkBoardData target;

        private Dictionary<Node, NodeData> _dataMap;
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

            _dataMap = new Dictionary<Node, NodeData>();
            RebuildGraph();

            _graphView.graphViewChanged += OnGraphChanged;
            _graphView.onNodeAdded += OnNodeAdded;
        }

        private void SetTarget(WorkBoardData data) {
            if (data == target) return;
            target = data;
            _graphView.graphViewChanged -= OnGraphChanged;
            _graphView.DeleteElements(_graphView.graphElements);
            _dataMap.Clear();
            nodeData = CloneData(data.nodeData);

            RebuildGraph();

            _graphView.graphViewChanged += OnGraphChanged;
        }

        private void RebuildGraph() {
            try {
                if (nodeData != null) {
                    foreach (var n in nodeData) {
                        var node = BoardNode.Create(n.data);
                        node.SetPosition(n.position);
                        _graphView.AddElement(node);
                        _dataMap[node] = n;
                    }
                }
            }
            catch (Exception e) {
                Debug.LogException(e);
                if (nodeData != null) nodeData.Clear();
            }
        }

        private void OnNodeAdded(GraphElement elem, Rect pos) {
            hasUnsavedChanges = true;

            if (elem is BoardNode node) {
                nodeData ??= new List<NodeData>();
                var data = new NodeData()
                {
                    data = node.data,
                    position = pos
                };
                nodeData.Add(data);
                _dataMap[node] = data;
            }
        }

        private GraphViewChange OnGraphChanged(GraphViewChange change) {
            hasUnsavedChanges = true;
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
                }
            }

            return change;
        }

        public override void SaveChanges() {
            if (target == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(target))) {
                var path = EditorUtility.SaveFilePanelInProject("Save Work Board", "WorkBoard", "asset", "Save");
                target = ScriptableObject.CreateInstance<WorkBoardData>();
                SaveToTarget();
                AssetDatabase.CreateAsset(target, path);
            } else {
                SaveToTarget();
            }
            base.SaveChanges();
        }

        private void SaveToTarget() {
            target.nodeData = CloneData(nodeData);
            EditorUtility.SetDirty(target);
        }

        private static List<NodeData> CloneData(List<NodeData> data) {
            var cloned = new List<NodeData>(data.Count);
            foreach (var n in data) {
                cloned.Add(n.CloneData());
            }

            return cloned;
        }
    }
}