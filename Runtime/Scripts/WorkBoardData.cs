using UnityEditor.Callbacks;

namespace WorkBoard {
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class WorkBoardData : ScriptableObject {
        [Serializable]
        public class NodeData {
            [SerializeField, SerializeReference]
            public BoardNodeData data;
            [SerializeField]
            public Rect position;

            public NodeData CloneData() {
                return new NodeData()
                {
                    data = data.ShallowCopy(),
                    position = position
                };
            }
        }

        [Serializable]
        public class EdgeData {
            public int fromIndex;
            public int toIndex;
        }

        [Serializable]
        public class GroupData {
            public string title;
            public int[] containedNodes;
        }

        [SerializeField]
        public List<NodeData> nodeData;

        [SerializeField]
        public List<EdgeData> edgeData;

        [SerializeField]
        public List<GroupData> groupData;
    }

    [Serializable]
    public class BoardNodeData {
        internal BoardNodeData ShallowCopy() {
            return (BoardNodeData)this.MemberwiseClone();
        }
    }


    [Serializable]
    public class FileData : BoardNodeData {
        [SerializeField]
        public UnityEngine.Object asset;
    }

    [Serializable]
    public class NoteData : BoardNodeData {
        public string note;
    }
}