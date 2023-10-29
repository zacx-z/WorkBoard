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
        private Object asset => data.asset;
        private InspectorElement inspectorElement;
        private Image iconPreviewElement;
        private Dictionary<Component, InspectorElement> componentInspectors;
        private List<Type> _previews;
        private ObjectPreview _activePreview;
        private PreviewElement _previewElement;

        public FileNode(FileData data) : base (data){
            this.title = asset.name;
            this.titleContainer.Insert(0, new Image()
            {
                image = AssetPreview.GetMiniThumbnail(asset)
            });
            this.expanded = false;

            RefreshInspectorElement();

            if (data.showInspector) this.expanded = true;

            if (data.inspectedComponents != null && asset is GameObject go) {
                componentInspectors = new Dictionary<Component, InspectorElement>();
                foreach (var c in data.inspectedComponents) {
                    var type = System.Type.GetType(c.typeName);
                    // TODO: if type is null, show missing components
                    var components = go.GetComponents(type);
                    if (c.index < components.Length) {
                        var comp =  components[c.index];
                        var insElem = new InspectorElement(comp)
                        {
                            style = { minWidth = 320 }
                        };
                        extensionContainer.Add(insElem);
                        componentInspectors.Add(comp, insElem);
                    } else {
                        // TODO: show missing components
                    }
                }
            }

            if (!string.IsNullOrEmpty(data.activePreviewType)) {
                var t = Type.GetType(data.activePreviewType);
                if (t != null) OpenPreview(t);
            }

            this.mainContainer.RegisterCallback<MouseDownEvent>(OnClick);
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
            evt.menu.AppendAction("Show Asset Preview", ShowPreview, (data.showPreview ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Show Inspector", ShowInspector, (data.showInspector ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
            if (data.showInspector && data.asset is GameObject go) {
                foreach (var comp in go.GetComponents<Component>()) {
                    var compEnabled = componentInspectors != null && componentInspectors.ContainsKey(comp);
                    evt.menu.AppendAction($"Inspector/{comp.GetType()}", action => {
                        ToggleComponentInspectorEnabled(go, comp);
                    }, (compEnabled ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
                }
            }

            _previews ??= InspectorUtils.GetPreviewableTypesForType(asset.GetType()) ?? new List<Type>();
            foreach (var preview in _previews) {
                evt.menu.AppendAction($"Preview/{preview.Name}", action => OpenPreview(preview), (_activePreview != null && _activePreview.GetType() == preview ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.None) | DropdownMenuAction.Status.Normal);
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
                parentView.AddToSelection(CreateChild(subFolder, pos));
                pos += 100 * Vector2.up;
            }

            foreach (var p in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly)) {
                if (p.EndsWith(".meta")) continue;
                parentView.AddToSelection(CreateChild(p, pos));
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
                parentView.AddToSelection(CreateChild(ch, pos));
                pos += 100 * Vector2.up;
            }
        }

        private void ToggleComponentInspectorEnabled(GameObject go, Component comp) {
            componentInspectors ??= new Dictionary<Component, InspectorElement>();
            data.inspectedComponents ??= new List<FileData.ComponentLocator>();
            var type = comp.GetType();
            var typeName = type.AssemblyQualifiedName;
            int compId = System.Array.IndexOf(go.GetComponents(type), comp);

            if (componentInspectors.TryGetValue(comp, out var ins)) {
                extensionContainer.Remove(ins);
                componentInspectors.Remove(comp);
                data.inspectedComponents.RemoveAll(c => c.typeName == typeName && c.index == compId);
            } else {
                var insElem = new InspectorElement(comp)
                {
                    style = { minWidth = 320 }
                };
                extensionContainer.Add(insElem);
                componentInspectors.Add(comp, insElem);
                data.inspectedComponents.Add(new FileData.ComponentLocator()
                {
                    typeName = typeName,
                    index = compId
                });
                expanded = true;
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
            data.showInspector = !data.showInspector;
            if (data.showInspector) expanded = true;
            RefreshInspectorElement();
        }

        private void ShowPreview(DropdownMenuAction action) {
            data.showPreview = !data.showPreview;
            if (data.showPreview) expanded = true;
            RefreshPreviewElement();
        }

        private void OpenPreview(Type previewType) {
            if (_activePreview != null && _activePreview.GetType() == previewType) {
                ClosePreview();
                data.activePreviewType = null;
                return;
            }

            ClosePreview();

            try {
                _activePreview = InspectorUtils.GetPreviewForTarget(new[] { asset }, previewType);
                _previewElement = new PreviewElement(asset, _activePreview)
                {
                    style = { width = 390, height = 320 }
                };
                extensionContainer.Add(_previewElement);
                data.activePreviewType = previewType.AssemblyQualifiedName;

                expanded = true;
            }
            catch (Exception e) {
                Debug.LogException(e);
                _activePreview = null;
                _previewElement = null;
            }
        }

        private void ClosePreview() {
            if (_activePreview != null) _activePreview.Cleanup();
            if (_previewElement != null) {
                extensionContainer.Remove(_previewElement);
            }
            _activePreview = null;
            _previewElement = null;
        }

        private void RefreshInspectorElement() {
            if (data.showInspector) {
                if (inspectorElement == null) {
                    inspectorElement = new InspectorElement(asset)
                    {
                        style = { minWidth = 320 }
                    };
                    extensionContainer.Add(inspectorElement);
                }

                if (componentInspectors != null) {
                    foreach (var ins in componentInspectors.Values) {
                        ins.style.display = DisplayStyle.Flex;
                    }
                }
            } else {
                if (inspectorElement != null) {
                    extensionContainer.Remove(inspectorElement);
                    inspectorElement = null;
                }

                if (componentInspectors != null) {
                    foreach (var ins in componentInspectors.Values) {
                        ins.style.display = DisplayStyle.None;
                    }
                }
            }
        }

        private void RefreshPreviewElement() {
            if (iconPreviewElement != null) {
                iconPreviewElement.style.display = data.showPreview ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (iconPreviewElement == null && data.showPreview) {
                iconPreviewElement = new Image();
                iconPreviewElement.image = AssetPreview.GetAssetPreview(asset);
                extensionContainer.Insert(0, iconPreviewElement);
            }
        }

        private Node CreateChild(string path, Vector2 pos) {
            return CreateChild(AssetDatabase.LoadAssetAtPath<Object>(path), pos);
        }

        private Node CreateChild(Object o, Vector2 pos) {
            return base.CreateChild(new FileData() { asset = o }, pos);
        }

        private Vector2 GetNewChildPosition() {
            var pos = GetPosition();
            return pos.position + (pos.width + 50) * Vector2.right;
        }
    }
}