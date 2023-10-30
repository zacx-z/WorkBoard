using System.Linq;

namespace WorkBoard {
    using System;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor.Experimental.GraphView;

    public interface IBoardElement {
        BoardNodeData Data { get; }
        void SubscribeWillChange(Action listener);
    }

    public interface IBoardNode : IBoardElement {
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

        public static IBoardElement Create(BoardNodeData data) {
            if (data == null) {
                Debug.LogWarning("data is null");
            }

            var dataType = data.GetType();
            foreach (var type in typeof(IBoardNode).Assembly.GetTypes()) {
                var baseType = type.BaseType;
                if (baseType != null) {
                    if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(BoardNode<>)) {
                        if (dataType == baseType.GetGenericArguments()[0]) {
                            return (IBoardElement)Activator.CreateInstance(type, data);
                        }
                    }

                    var attr = type.GetCustomAttributes(typeof(BoardElementDataTypeAttribute), true);
                    if (attr.Length > 0 && (attr[0] as BoardElementDataTypeAttribute).DataType == dataType) {
                        return (IBoardElement)Activator.CreateInstance(type, data);
                    }
                }
            }

            Debug.LogError($"Can't find node for {dataType}");

            return null;
        }

        public BoardNode(BoardNodeData data) {
            this._data = data;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
        }

        protected void OnWillChange() {
            onWillChange?.Invoke();
        }

        public IBoardNode CreateChild(BoardNodeData nodeData, Vector2 position) {
            var node = (GraphElement)Create(nodeData);
            var view = this.parentView;
            if (view is WorkBoardView bv) {
                bv.OnCreateNode(node, position);
            }

            var edge = ConnectChild((IBoardNode)node);

            if (view is WorkBoardView v) {
                v.OnEdgeAdded(edge);
            }

            return (IBoardNode)node;
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

    public class BoardElementDataTypeAttribute : Attribute {
        public readonly Type DataType;

        public BoardElementDataTypeAttribute(Type dataType) {
            DataType = dataType;
        }
    }

    public class BoardNode<T> : BoardNode where T : BoardNodeData {
        public readonly new T Data;
        public BoardNode(T data) : base(data) {
            this.Data = data;
        }
    }
}
