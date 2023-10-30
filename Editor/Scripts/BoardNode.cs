using UnityEngine.UIElements;

namespace WorkBoard {
    using System;
    using UnityEngine;
    using UnityEditor.Experimental.GraphView;

    public interface IBoardNode
    {
        void SubscribeWillChange(Action listener);
        BoardNodeData Data { get; }
        Port ParentPort { get; }
        Edge ConnectChild(IBoardNode node);
        void OnConnected();
    }

    public class BoardNode : Node, IBoardNode
    {
        private readonly BoardNodeData _data;
        private Port _childrenPort;
        private Port _parentPort;

        public BoardNodeData Data => _data;
        public Port ParentPort {
            get {
                if (_parentPort == null) {
                    _parentPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, null);
                    _parentPort.SetEnabled(false);
                    inputContainer.Add(_parentPort);
                }
                return _parentPort;
            }
        }

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
            this._data = data;
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

        public void SubscribeWillChange(Action listener) => onWillChange += listener;

        public Edge ConnectChild(IBoardNode node) {
            CreateChildrenPort();

            var edge = _childrenPort.ConnectTo(node.ParentPort);
            edge.SetEnabled(false);
            parentView.AddElement(edge);

            RefreshExpandedState();
            node.OnConnected();

            return edge;
        }

        public void OnConnected() => RefreshExpandedState();

        private void CreateChildrenPort() {
            if (_childrenPort == null) {
                _childrenPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, null);
                _childrenPort.SetEnabled(false);
                outputContainer.Add(_childrenPort);
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
