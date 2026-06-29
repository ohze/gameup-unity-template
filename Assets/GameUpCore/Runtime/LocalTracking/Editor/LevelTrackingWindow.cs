using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GameUp.Core;
using UnityEditor;
using UnityEngine;
using GameUpCore.Runtime.LocalTracking;

public class LevelTrackingWindow : EditorWindow
{
    private List<PlayerLevelTracking> _data;
    private Vector2 _scroll;

    private const string PlayerLevelKey = "p_Level";

    private bool _showAttempts = true;
    private bool _showDuration = true;
    private int _minLevel = 1;
    private int _maxLevel = 999;

    [MenuItem("GameUp/Level Tracking Viewer")]
    public static void ShowWindow()
    {
        GetWindow<LevelTrackingWindow>("Level Tracking");
    }

    private void OnEnable()
    {
        LoadData();
    }

    private void LoadData()
    {
        _data = LocalStorageUtils.HasKey(PlayerLevelKey) ? LocalStorageUtils.GetObject<List<PlayerLevelTracking>>(PlayerLevelKey) : new List<PlayerLevelTracking>();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (_data == null || _data.Count == 0)
        {
            GUILayout.Label("No Data");
            return;
        }

        _data = _data.OrderBy(x => x.level).ToList();

        DrawToggle();
        DrawDifficultyGraph();
        DrawLegend();
    }

    #region UI

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Refresh"))
        {
            LoadData();
        }

        if (GUILayout.Button("Clear"))
        {
            if (EditorUtility.DisplayDialog("Confirm", "Clear all data?", "Yes", "No"))
            {
                PlayerPrefs.DeleteKey(PlayerLevelKey);
                LoadData();
            }
        }
        
        if (GUILayout.Button("Export CSV"))
        {
            ExportCsv();
        }

        if (GUILayout.Button("Import CSV"))
        {
            ImportCsv();
        }
        
        GUILayout.EndHorizontal();
        
        DrawLevelRangeFilter();
    }

    private void DrawToggle()
    {
        GUILayout.BeginHorizontal();
        _showAttempts = GUILayout.Toggle(_showAttempts, "Attempts");
        _showDuration = GUILayout.Toggle(_showDuration, "Duration");
        GUILayout.EndHorizontal();
    }
    
    private void DrawLevelRangeFilter()
    {
        GUILayout.Space(5);
        GUILayout.Label("Level Range Filter", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();

        GUILayout.Label("From", GUILayout.Width(40));
        _minLevel = EditorGUILayout.IntField(_minLevel, GUILayout.Width(60));

        GUILayout.Label("To", GUILayout.Width(25));
        _maxLevel = EditorGUILayout.IntField(_maxLevel, GUILayout.Width(60));

        GUILayout.EndHorizontal();

        // Clamp logic
        if (_minLevel > _maxLevel)
        {
            (_minLevel, _maxLevel) = (_maxLevel, _minLevel);
        }
    }

    #endregion

    #region GRAPH

    private void DrawDifficultyGraph()
    {
        GUILayout.Space(10);

        Rect rect = GUILayoutUtility.GetRect(800, 300);
        GUI.Box(rect, "");

        float padding = 40f;

        Rect graphRect = new Rect(
            rect.x + padding,
            rect.y + padding,
            rect.width - padding * 2,
            rect.height - padding * 2
        );

        float maxAttempts = Mathf.Max(_data.Max(x => x.startAttempt), 1);
        float maxDuration = Mathf.Max(_data.Max(x => x.levelDuration), 1);

        float scaleAttempts = maxAttempts;
        float scaleDuration = maxDuration;

        // Nếu bật cả 2 → dùng chung scale
        if (_showAttempts && _showDuration)
        {
            float globalMax = Mathf.Max(maxAttempts, maxDuration);
            scaleAttempts = globalMax;
            scaleDuration = globalMax;
        }

        var filteredData = _data
            .Where(x => x.level >= _minLevel && x.level <= _maxLevel)
            .OrderBy(x => x.level)
            .ToList();

        if (filteredData.Count == 0)
        {
            GUILayout.Label("No data in selected range");
            return;
        }

        Handles.BeginGUI();

        if (_showAttempts)
            DrawLine(filteredData, graphRect, scaleAttempts, true, Color.red);

        if (_showDuration)
            DrawLine(filteredData, graphRect, scaleDuration, false, Color.green);

        Handles.EndGUI();

        DrawXAxis(graphRect, filteredData);
    }

    private void DrawLine(List<PlayerLevelTracking> data, Rect rect, float maxValue, bool isAttempt, Color color)
    {
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < data.Count; i++)
        {
            float value = isAttempt ? data[i].startAttempt : data[i].levelDuration;

            float x = rect.x + (i / (float)(data.Count - 1)) * rect.width;
            float y = rect.yMax - (value / maxValue) * rect.height;

            points.Add(new Vector3(x, y, 0));
        }

        DrawSmoothLine(points, color);

        Vector2 mouse = Event.current.mousePosition;

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];

            Rect pointRect = new Rect(p.x - 4, p.y - 4, 8, 8);
            EditorGUI.DrawRect(pointRect, color);

            // Tooltip
            if (pointRect.Contains(mouse))
            {
                GUI.Box(
                    new Rect(mouse.x + 10, mouse.y, 150, 50),
                    $"Level {_data[i].level}\nAttempts: {_data[i].startAttempt}\nDuration: {_data[i].levelDuration:F1}"
                );
            }
        }
    }

    private void DrawSmoothLine(List<Vector3> points, Color color)
    {
        Handles.color = color;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p0 = points[i];
            Vector3 p1 = points[i + 1];

            Vector3 tangent = (p1 - p0) * 0.5f;

            Handles.DrawBezier(
                p0,
                p1,
                p0 + tangent,
                p1 - tangent,
                color,
                null,
                2f
            );
        }
    }

    private void DrawXAxis(Rect rect, List<PlayerLevelTracking> data)
    {
        for (int i = 0; i < data.Count; i++)
        {
            float x = rect.x + (i / (float)(data.Count - 1)) * rect.width;

            GUI.Label(
                new Rect(x - 20, rect.yMax + 5, 40, 20),
                $"{data[i].level}",
                new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 10
                }
            );
        }
    }

    private void DrawLegend()
    {
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        GUI.color = Color.red;
        GUILayout.Box("", GUILayout.Width(15), GUILayout.Height(15));
        GUI.color = Color.white;
        GUILayout.Label("Attempts", GUILayout.Width(80));

        GUI.color = Color.green;
        GUILayout.Box("", GUILayout.Width(15), GUILayout.Height(15));
        GUI.color = Color.white;
        GUILayout.Label("Duration", GUILayout.Width(80));

        GUILayout.EndHorizontal();
    }

    #endregion

    private void ExportCsv()
    {
        if (_data == null || _data.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No data to export", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel(
            "Export Level Tracking",
            "",
            "LevelTracking.csv",
            "csv"
        );

        if (string.IsNullOrEmpty(path)) return;

        StringBuilder sb = new StringBuilder();

        // Header
        sb.AppendLine("Level,Attempts,Duration,Reason");

        foreach (var item in _data.OrderBy(x => x.level))
        {
            string reason = item.reason?.Replace(",", ";"); // tránh vỡ CSV
            sb.AppendLine($"{item.level},{item.startAttempt},{item.levelDuration},{reason}");
        }

        File.WriteAllText(path, sb.ToString());

        EditorUtility.DisplayDialog("Success", "Exported CSV successfully!", "OK");
    }
    
    private void ImportCsv()
    {
        string path = EditorUtility.OpenFilePanel("Import CSV", "", "csv");

        if (string.IsNullOrEmpty(path)) return;

        if (!EditorUtility.DisplayDialog(
                "Confirm Import",
                "This will overwrite current data. Continue?",
                "Yes", "No"))
        {
            return;
        }

        try
        {
            var lines = File.ReadAllLines(path);

            List<PlayerLevelTracking> newData = new List<PlayerLevelTracking>();

            for (int i = 1; i < lines.Length; i++) // skip header
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',');

                if (parts.Length < 4) continue;

                PlayerLevelTracking item = new PlayerLevelTracking
                {
                    level = int.Parse(parts[0]),
                    startAttempt = int.Parse(parts[1]),
                    levelDuration = float.Parse(parts[2]),
                    reason = parts[3]
                };

                newData.Add(item);
            }

            _data = newData;

            // Save lại vào LocalStorage
            LocalStorageUtils.SetObject(PlayerLevelKey, _data);

            EditorUtility.DisplayDialog("Success", "Imported CSV successfully!", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            EditorUtility.DisplayDialog("Error", "Failed to import CSV", "OK");
        }
    }
}