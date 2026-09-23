using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GameUp.UIBuilder.Editor
{
    /// <summary>
    /// Cây node của spec ngay trong cửa sổ UI Builder: ô tick chọn dựng hay bỏ, đổi tên tại chỗ (F2 / bấm đúp), kéo thả
    /// để đổi cha và đổi thứ tự vẽ, chọn nhiều node. Node đang chọn được tô trên ảnh demo bên phải và ngược lại.
    /// </summary>
    public sealed class UISpecTree : TreeView
    {
        private const float ToggleWidth = 16f;

        private readonly Dictionary<int, UISpecNode> _nodes = new Dictionary<int, UISpecNode>();
        private readonly Dictionary<string, int> _ids = new Dictionary<string, int>();
        private readonly HashSet<int> _excluded = new HashSet<int>();
        private readonly HashSet<string> _selected = new HashSet<string>();

        private UISpec _spec;
        private int _nextId;
        private bool _needsReload;

        /// <summary>Gọi sau mỗi thay đổi cấu trúc (tick, đổi tên, kéo thả) để cửa sổ ghi lại spec.</summary>
        public Action Changed;

        public UISpecTree(TreeViewState state) : base(state)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            rowHeight = 18f;
        }

        /// <summary>id các node đang chọn (cả node còn dựng lẫn node đã bỏ).</summary>
        public ICollection<string> Selected => _selected;

        public void SetSpec(UISpec spec)
        {
            _spec = spec;
            Reload();
        }

        /// <summary>Chọn một node theo id và cuộn tới nó — dùng khi bấm vào khung trên ảnh demo.</summary>
        public void SelectNode(string id)
        {
            if (!_ids.TryGetValue(id, out var item)) return;
            SetSelection(new List<int> { item },
                TreeViewSelectionOptions.RevealAndFrame | TreeViewSelectionOptions.FireSelectionChanged);
        }

        public override void OnGUI(Rect rect)
        {
            if (_needsReload)
            {
                _needsReload = false;
                Reload();
            }

            base.OnGUI(rect);
        }

        protected override TreeViewItem BuildRoot()
        {
            _nodes.Clear();
            _ids.Clear();
            _excluded.Clear();
            _nextId = 1;

            var root = new TreeViewItem { id = 0, depth = -1, displayName = "root" };
            if (_spec == null)
            {
                root.AddChild(new TreeViewItem { id = _nextId++, depth = 0, displayName = "(chưa có spec)" });
                return root;
            }

            AddChildren(root, string.Empty, 0);
            if (!root.hasChildren) root.AddChild(new TreeViewItem { id = _nextId++, depth = 0, displayName = "(spec rỗng)" });
            RefreshSelected(state.selectedIDs);
            return root;
        }

        /// <summary>Node còn dựng trước, node đã bỏ xếp cuối cùng cấp (đã mất thứ tự vẽ gốc).</summary>
        private void AddChildren(TreeViewItem parent, string parentId, int depth)
        {
            foreach (var node in UISpecEdit.ChildrenOf(_spec, parentId)) AddNode(parent, node, depth, false);
            foreach (var node in _spec.excluded.Where(n => (n.parent ?? string.Empty) == parentId))
                AddNode(parent, node, depth, true);
        }

        private void AddNode(TreeViewItem parent, UISpecNode node, int depth, bool excluded)
        {
            var item = new TreeViewItem { id = _nextId++, depth = depth, displayName = node.id };
            _nodes[item.id] = node;
            _ids[node.id] = item.id;
            if (excluded) _excluded.Add(item.id);
            parent.AddChild(item);
            AddChildren(item, node.id, depth + 1);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (!_nodes.TryGetValue(args.item.id, out var node))
            {
                base.RowGUI(args);
                return;
            }

            var excluded = _excluded.Contains(args.item.id);
            var rect = args.rowRect;
            var indent = GetContentIndent(args.item);

            var toggle = new Rect(rect.x + indent, rect.y + 1f, ToggleWidth, ToggleWidth);
            EditorGUI.BeginChangeCheck();
            var build = EditorGUI.Toggle(toggle, !excluded);
            if (EditorGUI.EndChangeCheck()) SetBuild(node, build);

            var label = new Rect(toggle.xMax + 2f, rect.y, rect.width - indent - ToggleWidth - 110f, rect.height);
            if (!args.isRenaming)
            {
                var style = excluded ? Disabled : EditorStyles.label;
                GUI.Label(label, node.id, style);
                var detail = new Rect(rect.xMax - 108f, rect.y, 104f, rect.height);
                GUI.Label(detail, excluded ? $"{node.kind} · bỏ" : $"{node.kind} · {node.w}×{node.h}", Detail);
            }

            // Ô tick nằm chồng lên vùng bấm chọn của hàng → tự xử lý chọn, không để TreeView nuốt cú bấm.
            if (Event.current.type == EventType.MouseDown && toggle.Contains(Event.current.mousePosition))
                Event.current.Use();
        }

        private void SetBuild(UISpecNode node, bool build)
        {
            if (build) UISpecExclusions.Restore(_spec, node.id);
            else UISpecEdit.Exclude(_spec, node.id);
            Changed?.Invoke();
            // Đang ở giữa vòng vẽ hàng → dựng lại cây ở đầu lần vẽ sau, không phá danh sách hàng đang duyệt.
            _needsReload = true;
        }

        protected override void SelectionChanged(IList<int> selectedIds) => RefreshSelected(selectedIds);

        private void RefreshSelected(IList<int> ids)
        {
            _selected.Clear();
            foreach (var id in ids)
                if (_nodes.TryGetValue(id, out var node)) _selected.Add(node.id);
        }

        // ─── Đổi tên ────────────────────────────────────────────────────────

        protected override bool CanRename(TreeViewItem item) => _nodes.ContainsKey(item.id);

        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (!args.acceptedRename || !_nodes.TryGetValue(args.itemID, out var node)) return;
            var error = UISpecEdit.Rename(_spec, node.id, args.newName);
            if (error != null)
            {
                EditorUtility.DisplayDialog("Không đổi tên được", error, "OK");
                return;
            }

            Changed?.Invoke();
            Reload();
        }

        // ─── Kéo thả: đổi cha, đổi thứ tự vẽ ────────────────────────────────

        protected override bool CanStartDrag(CanStartDragArgs args) => _nodes.ContainsKey(args.draggedItem.id);

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("UIBuilderNodes", args.draggedItemIDs.Where(_nodes.ContainsKey).ToList());
            DragAndDrop.objectReferences = new UnityEngine.Object[0];
            DragAndDrop.StartDrag(args.draggedItemIDs.Count == 1 ? _nodes[args.draggedItemIDs[0]].id : "Nhiều node");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (!(DragAndDrop.GetGenericData("UIBuilderNodes") is List<int> dragged)) return DragAndDropVisualMode.None;
            if (args.dragAndDropPosition == DragAndDropPosition.OutsideItems) return DragAndDropVisualMode.None;
            if (!args.performDrop) return DragAndDropVisualMode.Move;

            var ids = dragged.Where(_nodes.ContainsKey).Select(i => _nodes[i].id).ToList();
            var (parent, index) = DropTarget(args);
            UISpecEdit.Reparent(_spec, ids, parent, index);
            Changed?.Invoke();
            Reload();
            SetSelection(ids.Where(_ids.ContainsKey).Select(i => _ids[i]).ToList(),
                TreeViewSelectionOptions.FireSelectionChanged);
            return DragAndDropVisualMode.Move;
        }

        /// <summary>Thả lên một node = vào làm con nó; thả giữa hai hàng = chèn vào đúng chỗ đó trong danh sách anh em.</summary>
        private (string parent, int index) DropTarget(DragAndDropArgs args)
        {
            if (args.dragAndDropPosition == DragAndDropPosition.UponItem)
                return (_nodes.TryGetValue(args.parentItem.id, out var upon) ? upon.id : string.Empty, -1);

            var parentId = _nodes.TryGetValue(args.parentItem?.id ?? 0, out var parent) ? parent.id : string.Empty;
            return (parentId, args.insertAtIndex);
        }

        // ─── Kiểu chữ ───────────────────────────────────────────────────────

        private static GUIStyle _disabled;
        private static GUIStyle _detail;

        private static GUIStyle Disabled => _disabled ??= new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
        };

        private static GUIStyle Detail => _detail ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };
    }
}
