namespace WorkBoard {
    using System;
    using UnityEngine;
    using UnityEditor.Experimental.GraphView;

    public class BoardNode : Node {
        public readonly BoardNodeData data;

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
    }

    public class BoardNode<T> : BoardNode where T : BoardNodeData {
        public readonly new T data;
        public BoardNode(T data) : base(data) {
            this.data = data;
        }
    }
}