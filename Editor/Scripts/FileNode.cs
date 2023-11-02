using System;

namespace WorkBoard {
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UIElements;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;

    public class FileNode : BoardNode<FileData> {
        private Object asset => Data.asset;
        private InspectorElement _inspectorElement;
        private Image _iconPreviewElement;
        private Dictionary<Component, InspectorElement> componentInspectors;
        private List<Type> _previewTypes;
        private List<ObjectPreview> _previews;
        private PreviewElement _previewElement;
        private Editor _cachedEditor;

        public FileNode(FileData data) : base (data){
            this.title = asset.name;
            this.titleContainer.Insert(0, new Image()
            {
                image = AssetPreview.GetMiniThumbnail(asset),
                style =
                {
                    maxWidth = 64
                }
            });
            this.expanded = false;

            RefreshInspectorElement();
            RefreshAssetPreviewElement();

            if (data.showInspector)
            {
                this.expanded = true;
                RefreshExpandedState();
            }

            if (data.inspectedComponents != null && asset is GameObject go) {
                componentInspectors = new Dictionary<Component, InspectorElement>();
                foreach (var c in data.inspectedComponents) {
                    var type = System.Type.GetType(c.typeName);
                    // TODO: if type is null, show missing components
                    var components = go.GetComponents(type);
                    if (c.index < components.Length) {
                        var comp = components[c.index];
                        var insElem = new InspectorElement(comp)
                        {
                            style = { minWidth = 320 }
                        };
                        if (!data.showInspector) insElem.style.display = DisplayStyle.None;
                        extensionContainer.Add(insElem);
                        componentInspectors.Add(comp, insElem);
                    } else {
                        // TODO: show missing components
                    }
                }
            }

            if (!string.IsNullOrEmpty(data.activePreviewType)) {
                var t = Type.GetType(data.activePreviewType);
                if (t != null)
                {
                    OpenPreview(t, typeof(Editor).IsAssignableFrom(t) ? new EditorPreviewProvider(_cachedEditor ??= Editor.CreateEditor(asset)) : null);
                }
            }

            this.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanelEvent);
            this.mainContainer.RegisterCallback<MouseDownEvent>(OnClick);
        }

        private void OnDetachFromPanelEvent(DetachFromPanelEvent evt) {
            if (_previews != null) {
                _previews.ForEach(p => p.Cleanup());
                _previews.Clear();
            }

            if (_cachedEditor != null) {
                Object.DestroyImmediate(_cachedEditor);
            }
        }

        private void OnClick(MouseDownEvent e) {
            if (e.clickCount == 2) {
                EditorGUIUtility.PingObject(asset);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt) {
            evt.menu.AppendAction("Ping", PingAsset);
            if (asset is DefaultAsset) {
                var path = AssetDatabase.GetAssetPath(asset);
                if (AssetDatabase.IsValidFolder(path)) {
                    evt.menu.AppendAction("Expand Folder Content", ExpandFolderContent);
                }
            } else {
                evt.menu.AppendAction("Expand Children References", ExpandChildrenReferences);
            }
            evt.menu.AppendAction("Show Asset Preview", ShowPreview, (Data.showPreview ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Show Inspector", ShowInspector, (Data.showInspector ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
            if (Data.showInspector && Data.asset is GameObject go) {
                foreach (var comp in go.GetComponents<Component>()) {
                    var compEnabled = componentInspectors != null && componentInspectors.ContainsKey(comp);
                    evt.menu.AppendAction($"Inspector/{comp.GetType()}", action => {
                        ToggleComponentInspectorEnabled(go, comp);
                    }, (compEnabled ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
                }
            }

            _previewTypes ??= InspectorUtils.GetPreviewableTypesForType(asset.GetType()) ?? new List<Type>();
            if (_previews != null) {
                _previews.ForEach(p => p.Cleanup());
                _previews.Clear();
            }

            _previews ??= new List<ObjectPreview>();
            foreach (var previewType in _previewTypes) {
                var preview = InspectorUtils.GetPreviewForTarget(new[] { asset }, previewType);
                if (preview.HasPreviewGUI()) {
                    _previews.Add(preview);
                    evt.menu.AppendAction($"Preview/{previewType.Name}", action => OpenPreview(previewType, preview != null ? new ObjectPreviewProvider(preview) : null),
                        (_previewElement != null && _previewElement.previewType == previewType
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
                } else {
                    preview.Cleanup();
                }
            }

            if (_inspectorElement != null && _inspectorElement.Editor != null) {
                _cachedEditor = _inspectorElement.Editor;
            }

            if (_cachedEditor == null) {
                _cachedEditor = Editor.CreateEditor(asset);
            }

            if (_cachedEditor.HasPreviewGUI()) {
                var previewType = _cachedEditor.GetType();
                evt.menu.AppendAction($"Preview/{_cachedEditor.GetType()}", action => OpenPreview(previewType, new EditorPreviewProvider(_cachedEditor))
                        , (_previewElement != null && _previewElement.previewType == previewType
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
            }
            evt.menu.AppendSeparator();
            base.BuildContextualMenu(evt);
        }

        private void PingAsset(DropdownMenuAction action) {
            EditorGUIUtility.PingObject(asset);
        }

        private void ExpandFolderContent(DropdownMenuAction obj) {
            var path = AssetDatabase.GetAssetPath(asset);
            Vector2 pos = GetNewChildPosition();
            parentView.ClearSelection();
            foreach (var subFolder in AssetDatabase.GetSubFolders(path)) {
                parentView.AddToSelection((GraphElement)CreateChild(subFolder, pos));
                pos += 100 * Vector2.up;
            }

            foreach (var p in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)) {
                if (p.EndsWith(".meta")) continue;
                parentView.AddToSelection((GraphElement)CreateChild(p, pos));
                pos += 100 * Vector2.up;
            }
        }

        private void ExpandChildrenReferences(DropdownMenuAction action) {
            Vector2 pos = GetNewChildPosition();
            parentView.ClearSelection();

            var children = CollectChildren(asset);

            if (asset is GameObject go) {
                children = go.GetComponents<Component>().SelectMany(CollectChildren);
            }

            foreach (var ch in children.Distinct()) {
                parentView.AddToSelection((GraphElement)CreateChild(ch, pos));
                pos += 100 * Vector2.up;
            }
        }

        private void ToggleComponentInspectorEnabled(GameObject go, Component comp) {
            componentInspectors ??= new Dictionary<Component, InspectorElement>();
            Data.inspectedComponents ??= new List<FileData.ComponentLocator>();
            var type = comp.GetType();
            var typeName = type.AssemblyQualifiedName;
            int compId = Array.IndexOf(go.GetComponents(type), comp);

            if (componentInspectors.TryGetValue(comp, out var ins)) {
                extensionContainer.Remove(ins);
                componentInspectors.Remove(comp);
                Data.inspectedComponents.RemoveAll(c => c.typeName == typeName && c.index == compId);
            } else {
                var compInsElem = new InspectorElement(comp)
                {
                    style = { minWidth = 320 }
                };
                extensionContainer.Add(compInsElem);
                componentInspectors.Add(comp, compInsElem);
                Data.inspectedComponents.Add(new FileData.ComponentLocator()
                {
                    typeName = typeName,
                    index = compId
                });
                expanded = true;
                RefreshExpandedState();
            }
        }

        private IEnumerable<Object> CollectChildren(Object o) {
            var so = new SerializedObject(o);
            for (var iter = so.GetIterator(); iter.Next(true);) {
                if ((iter.propertyType == SerializedPropertyType.ObjectReference || iter.propertyType == SerializedPropertyType.ManagedReference) && iter.objectReferenceValue != null) {
                    if (iter.objectReferenceValue.GetType() == typeof(Object)) continue;
                    if (iter.objectReferenceValue is Component comp && comp.gameObject == asset) continue;
                    if (iter.objectReferenceValue == asset) continue;
                    var path = AssetDatabase.GetAssetPath(iter.objectReferenceValue);
                    if (!string.IsNullOrEmpty(path)) {
                        yield return iter.objectReferenceValue;
                    }
                }
            }
            so.Dispose();
        }

        private void ShowInspector(DropdownMenuAction action) {
            OnWillChange();
            Data.showInspector = !Data.showInspector;
            RefreshInspectorElement();
            if (Data.showInspector)
            {
                expanded = true;
                RefreshExpandedState();
            }
        }

        private void ShowPreview(DropdownMenuAction action) {
            OnWillChange();
            Data.showPreview = !Data.showPreview;
            RefreshAssetPreviewElement();
        }

        private void OpenPreview(Type previewType, IPreviewProvider preview) {
            if (_previewElement != null && _previewElement.previewType == previewType) {
                ClosePreview();
                OnWillChange();
                Data.activePreviewType = null;
                return;
            }

            ClosePreview();

            if (preview != null && preview is ObjectPreviewProvider op) {
                _previews.Remove(op.preview);
            }

            try {
                OnWillChange();
                _previewElement = new PreviewElement(asset, previewType, preview);
                extensionContainer.Add(_previewElement);
                Data.activePreviewType = previewType.AssemblyQualifiedName;

                expanded = true;
                RefreshExpandedState();
            }
            catch (Exception e) {
                Debug.LogException(e);
                _previewElement = null;
            }
        }

        private void ClosePreview() {
            if (_previewElement != null) {
                extensionContainer.Remove(_previewElement);
            }
            _previewElement = null;
        }

        private void RefreshInspectorElement() {
            if (Data.showInspector) {
                if (_inspectorElement == null) {
                    _inspectorElement = new InspectorElement(asset, _cachedEditor)
                    {
                        style = { minWidth = 320 }
                    };
                    extensionContainer.Add(_inspectorElement);
                } else {
                    _inspectorElement.style.display = DisplayStyle.Flex;
                }

                if (componentInspectors != null) {
                    foreach (var ins in componentInspectors.Values) {
                        ins.style.display = DisplayStyle.Flex;
                    }
                }
            } else {
                if (_inspectorElement != null) {
                    _inspectorElement.style.display = DisplayStyle.None;
                }

                if (componentInspectors != null) {
                    foreach (var ins in componentInspectors.Values) {
                        ins.style.display = DisplayStyle.None;
                    }
                }
            }
        }

        private void RefreshAssetPreviewElement() {
            if (_iconPreviewElement != null) {
                _iconPreviewElement.style.display = Data.showPreview ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_iconPreviewElement == null && Data.showPreview) {
                _iconPreviewElement = new Image
                {
                    image = AssetPreview.GetAssetPreview(asset)
                };
                extensionContainer.Insert(0, _iconPreviewElement);
                expanded = true;
                RefreshExpandedState();
            }
        }

        private IBoardNode CreateChild(string path, Vector2 pos) {
            return CreateChild(AssetDatabase.LoadAssetAtPath<Object>(path), pos);
        }

        private IBoardNode CreateChild(Object o, Vector2 pos) {
            return base.CreateChild(new FileData() { asset = o }, pos);
        }

        private Vector2 GetNewChildPosition() {
            var pos = GetPosition();
            return pos.position + (pos.width + 50) * Vector2.right;
        }
    }
}
