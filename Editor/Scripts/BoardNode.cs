using UnityEngine.UIElements;

namespace WorkBoard {
    using System;
    using UnityEngine;
    using UnityEditor.Experimental.GraphView;

    public class BoardNode : Node {
        public readonly BoardNodeData data;
        private Port childrenPort;
        private Port parentPort;

        protected GraphView parentView => _parentView ??= GetFirstAncestorOfType<GraphView>();
        private GraphView _parentView;
        public event Action onWillChange;

        public static BoardNode Create(BoardNodeData data) {
            if (data == null) {
                Debug.LogWarning("data is null");
            }

            var dataType = data.GetType();
            foreach (var type in typeof(BoardNode).Assembly.GetTypes()) {
                var baseType = type.BaseType;
                if (baseType != null && baseType.IsGenericType &&
                    baseType.GetGenericTypeDefinition() == typeof(BoardNode<>)) {
                    if (dataType == baseType.GetGenericArguments()[0]) {
                        return (BoardNode)Activator.CreateInstance(type, data);
                    }
                }
            }

            return null;
        }

        public BoardNode(BoardNodeData data) {
            this.data = data;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            if (this.GetContainingScope() is Group group) {
                evt.menu.AppendAction("Move Out Of Group", action => {
                    group.RemoveElement(this);
                });
            }
        }

        protected void OnWillChange() {
            onWillChange?.Invoke();
        }

        public Node CreateChild(BoardNodeData nodeData, Vector2 position) {
            var node = Create(nodeData);
            var view = this.parentView;
            if (view is WorkBoardView bv) {
                bv.OnCreateNode(node, position);
            }

            var edge = ConnectChild(node);

            if (view is WorkBoardView v) {
                v.OnEdgeAdded(edge);
            }

            return node;
        }

        public Edge ConnectChild(BoardNode node) {
            CreateChildrenPort();
            if (node.parentPort == null) {
                node.parentPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, null);
                node.parentPort.SetEnabled(false);
                node.inputContainer.Add(node.parentPort);
            }

            var edge = childrenPort.ConnectTo(node.parentPort);
            edge.SetEnabled(false);
            parentView.AddElement(edge);

            RefreshExpandedState();
            node.RefreshExpandedState();

            return edge;
        }

        private void CreateChildrenPort() {
            if (childrenPort == null) {
                childrenPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, null);
                childrenPort.SetEnabled(false);
                outputContainer.Add(childrenPort);
            }
        }
    }

    public class BoardNode<T> : BoardNode where T : BoardNodeData {
        public readonly new T data;
        public BoardNode(T data) : base(data) {
            this.data = data;
        }
    }
}