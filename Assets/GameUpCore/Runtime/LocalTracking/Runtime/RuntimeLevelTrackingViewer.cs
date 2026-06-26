using System.Collections.Generic;
using System.Linq;
using GameUp.Core;
using UnityEngine;
using GameUpCore.Runtime.LocalTracking;

namespace GameUpCore.Runtime.LocalTracking
{
    public class RuntimeLevelTrackingViewer : MonoBehaviour
    {
        private const string PlayerLevelKey = "p_Level";
        private List<PlayerLevelTracking> _data;

        // =================================================================
        [Header("Security Settings")]
        [Tooltip("Key chính để quyết định có bật tính năng xem biểu đồ này không.")]
        [SerializeField] private bool _isViewerEnabled = true; 

        [Tooltip("Nếu tích chọn, tính năng sẽ TỰ ĐỘNG KHÓA CHẾT khi build Production (Release build), bất kể biến _isViewerEnabled ở trên có bật hay không.")]
        [SerializeField] private bool _autoBlockInProduction = true;
        // =================================================================

        [Header("Shortcut Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote; 
        [SerializeField] private int _requiredTouchCount = 3;            

        private bool _showWindow = false;
        private bool _showAttempts = true;
        private bool _showDuration = true;
        
        private int _minLevel = 1;
        private int _maxLevel = 100;
        private string _minLevelStr = "1";
        private string _maxLevelStr = "100";

        private Vector2 _scrollPosition;
        private Rect _windowRect = new Rect(50, 50, 650, 550);
        private PlayerLevelTracking _selectedNode = null;

        private void Start()
        {
            // Nếu tính năng bị tắt hoàn toàn bằng Key, tự hủy Component này để tiết kiệm bộ nhớ
            if (!IsFeatureAllowed())
            {
                Destroy(gameObject);
                return;
            }

            float width = Mathf.Min(Screen.width * 0.9f, 850f);
            float height = Mathf.Min(Screen.height * 0.9f, 600f);
            _windowRect = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            
            LoadData();
        }

        private void Update()
        {
            // Kiểm tra bảo mật liên tục
            if (!IsFeatureAllowed()) return;

            if (Input.GetKeyDown(_toggleKey)) ToggleWindow();

            if (Input.touchCount == _requiredTouchCount && Input.GetTouch(_requiredTouchCount - 1).phase == TouchPhase.Began)
            {
                ToggleWindow();
            }
        }

        private void OnGUI()
        {
            if (!_showWindow || !IsFeatureAllowed()) return;
            _windowRect = GUI.Window(0, _windowRect, DrawWindowContents, "Level Tracking Realtime Viewer");
        }

        // Hàm kiểm tra quyền bật tính năng
        private bool IsFeatureAllowed()
        {
            // 1. Nếu Key cấu hình bị tắt -> Không cho bật
            if (!_isViewerEnabled) return false;

            // 2. Nếu cơ chế tự động khóa Production bật:
            // Debug.isDebugBuild trả về TRUE trong Unity Editor và bản build Development. 
            // Nó trả về FALSE khi build bản Production (Release) chính thức thương mại.
            if (_autoBlockInProduction && !Debug.isDebugBuild && !Application.isEditor)
            {
                return false; 
            }

            return true;
        }

        private void ToggleWindow()
        {
            _showWindow = !_showWindow;
            if (_showWindow)
            {
                LoadData();
                _selectedNode = null;
            }
        }

        private void LoadData()
        {
            _data = LocalStorageUtils.HasKey(PlayerLevelKey) 
                ? LocalStorageUtils.GetObject<List<PlayerLevelTracking>>(PlayerLevelKey) 
                : new List<PlayerLevelTracking>();
            
            if (_data != null && _data.Count > 0)
            {
                _data = _data.OrderBy(x => x.level).ToList();
            }
        }

        private void DrawWindowContents(int windowID)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Data", GUILayout.Height(35))) 
            {
                LoadData();
                _selectedNode = null;
            }
            if (GUILayout.Button("Generate Fake Data (Test)", GUILayout.Height(35)))
            {
                LocalLevelTracking.Instance.GenerateFakeData();
                LoadData();
                _selectedNode = null;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(40), GUILayout.Height(35))) _showWindow = false;
            GUILayout.EndHorizontal();

            if (_data == null || _data.Count == 0)
            {
                GUILayout.Label("No data tracked yet.");
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("From Level:", GUILayout.Width(80));
            _minLevelStr = GUILayout.TextField(_minLevelStr, GUILayout.Width(50));
            GUILayout.Label("To Level:", GUILayout.Width(70));
            _maxLevelStr = GUILayout.TextField(_maxLevelStr, GUILayout.Width(50));
            
            if (int.TryParse(_minLevelStr, out int min)) _minLevel = min;
            if (int.TryParse(_maxLevelStr, out int max)) _maxLevel = max;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _showAttempts = GUILayout.Toggle(_showAttempts, " Show Attempts (Red Line)");
            _showDuration = GUILayout.Toggle(_showDuration, " Show Duration (Green Line)");
            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(240));
            DrawRuntimeGraph();
            GUILayout.EndScrollView();

            DrawSelectedNodeDetails();

            GUI.DragWindow();
        }

        private void DrawRuntimeGraph()
        {
            var filteredData = _data
                .Where(x => x.level >= _minLevel && x.level <= _maxLevel)
                .OrderBy(x => x.level)
                .ToList();

            if (filteredData.Count == 0)
            {
                GUILayout.Label("No data in selected range.");
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(_windowRect.width - 40, 180);
            GUI.Box(rect, "Graph Canvas");

            float padding = 25f;
            Rect graphRect = new Rect(
                rect.x + padding,
                rect.y + padding,
                rect.width - padding * 2,
                rect.height - padding * 2
            );

            float maxAttempts = Mathf.Max(filteredData.Max(x => x.startAttempt), 1);
            float maxDuration = Mathf.Max(filteredData.Max(x => x.levelDuration), 1);

            float scaleAttempts = _showAttempts && _showDuration ? Mathf.Max(maxAttempts, maxDuration) : maxAttempts;
            float scaleDuration = _showAttempts && _showDuration ? Mathf.Max(maxAttempts, maxDuration) : maxDuration;

            if (_showAttempts) DrawRuntimeLine(filteredData, graphRect, scaleAttempts, true, Color.red);
            if (_showDuration) DrawRuntimeLine(filteredData, graphRect, scaleDuration, false, Color.green);

            for (int i = 0; i < filteredData.Count; i++)
            {
                if (filteredData.Count > 12 && i % (filteredData.Count / 6) != 0) continue; 
                
                float x = graphRect.x + (i / (float)Mathf.Max(filteredData.Count - 1, 1)) * graphRect.width;
                GUI.Label(new Rect(x - 15, graphRect.yMax + 2, 30, 20), filteredData[i].level.ToString());
            }
        }

        private void DrawRuntimeLine(List<PlayerLevelTracking> data, Rect rect, float maxValue, bool isAttempt, Color color)
        {
            Vector2 lastPoint = Vector2.zero;

            for (int i = 0; i < data.Count; i++)
            {
                float value = isAttempt ? data[i].startAttempt : data[i].levelDuration;

                float x = rect.x + (i / (float)Mathf.Max(data.Count - 1, 1)) * rect.width;
                float y = rect.yMax - (value / maxValue) * rect.height;

                Vector2 currentPoint = new Vector2(x, y);

                Rect touchHitbox = new Rect(x - 18, y - 18, 36, 36);
                if (Event.current.type == EventType.MouseDown && touchHitbox.Contains(Event.current.mousePosition))
                {
                    _selectedNode = data[i];
                    Event.current.Use(); 
                }

                if (i > 0)
                {
                    DrawGuiLine(lastPoint, currentPoint, color);
                }

                GUI.DrawTexture(new Rect(x - 3, y - 3, 6, 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);

                if (_selectedNode == data[i])
                {
                    GUI.DrawTexture(new Rect(x - 6, y - 6, 12, 12), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Color.yellow, 0, 0);
                    GUI.DrawTexture(new Rect(x - 4, y - 4, 8, 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
                }

                lastPoint = currentPoint;
            }
        }

        private void DrawGuiLine(Vector2 p1, Vector2 p2, Color color)
        {
            float distance = Vector2.Distance(p1, p2);
            float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
            
            Matrix4x4 savedMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, p1);
            
            GUI.DrawTexture(new Rect(p1.x, p1.y - 1, distance, 2), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
            
            GUI.matrix = savedMatrix;
        }

        private void DrawSelectedNodeDetails()
        {
            GUILayout.Space(5);
            if (_selectedNode != null)
            {
                GUI.backgroundColor = Color.yellow;
                GUILayout.BeginVertical("box");
                GUI.backgroundColor = Color.white;

                GUILayout.Label($"<b>[ THÔNG TIN CHI TIẾT LEVEL {_selectedNode.level} ]</b>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });
                GUILayout.Space(2);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"• <b>Số lần chơi (Attempts):</b> {_selectedNode.startAttempt}", new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Label($"• <b>Thời gian chơi (Duration):</b> {_selectedNode.levelDuration:F2} giây", new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.EndHorizontal();

                string failReason = string.IsNullOrEmpty(_selectedNode.reason) ? "Không có (Thắng trận)" : _selectedNode.reason;
                GUILayout.Label($"• <b>Lý do Thất bại/Dừng:</b> <color=orange>{failReason}</color>", new GUIStyle(GUI.skin.label) { richText = true });

                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.Box("Mẹo: Chạm trực tiếp vào bất kỳ điểm nút nào trên biểu đồ để xem chi tiết thông số Fail / Thời gian.", GUILayout.ExpandWidth(true), GUILayout.Height(45));
            }
        }
    }
}