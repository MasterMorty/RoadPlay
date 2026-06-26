#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RoadPlay.EditorTools
{
    /// <summary>
    /// Authoring tool: open a RoadMap asset, drop in your printed street-map image,
    /// then click to lay out the road graph the cars will drive on. Nodes are stored
    /// in normalised image space so they line up with the printed map at runtime.
    /// Window ▸ RoadPlay ▸ Map Editor.
    /// </summary>
    public class RoadMapEditorWindow : EditorWindow
    {
        enum Mode { Add, Move, Connect, Signal, Erase }

        RoadMap map;
        Mode mode = Mode.Add;
        int selected = -1;
        int connectFrom = -1;
        int dragging = -1;

        [MenuItem("Window/RoadPlay/Map Editor")]
        static void Open() => GetWindow<RoadMapEditorWindow>("RoadPlay Map Editor");

        void OnGUI()
        {
            DrawToolbar();
            if (map == null)
            {
                EditorGUILayout.HelpBox("Assign or create a RoadMap asset (Project ▸ Create ▸ RoadPlay ▸ Road Map).", MessageType.Info);
                return;
            }
            DrawCanvas();
            HandleShortcuts();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            map = (RoadMap)EditorGUILayout.ObjectField(map, typeof(RoadMap), false, GUILayout.Width(180));
            if (map != null)
            {
                GUILayout.Space(6);
                map.mapImage = (Texture2D)EditorGUILayout.ObjectField(map.mapImage, typeof(Texture2D), false, GUILayout.Width(140));
                mode = (Mode)EditorGUILayout.EnumPopup(mode, EditorStyles.toolbarPopup, GUILayout.Width(90));
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    if (EditorUtility.DisplayDialog("Clear graph", "Remove all nodes, edges and lights?", "Yes", "No")) ClearGraph();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(48))) Save();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (map != null)
            {
                EditorGUILayout.BeginHorizontal();
                map.referenceImageName = EditorGUILayout.TextField("Reference image name", map.referenceImageName);
                map.physicalWidthMeters = EditorGUILayout.FloatField("Phys width (m)", map.physicalWidthMeters);
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                float frac = EditorGUILayout.Slider(
                    new GUIContent("Street width", "Drag until the lines cover the printed streets. 0 = auto."),
                    map.streetWidthFrac, 0f, 0.3f);
                if (EditorGUI.EndChangeCheck()) { map.streetWidthFrac = frac; MarkDirty(); }
                if (frac > 0.0001f)
                    EditorGUILayout.LabelField(
                        $"    ≈ {frac * map.physicalWidthMeters * 100f:0.0} cm wide on a {map.physicalWidthMeters * 100f:0} cm map",
                        EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Nodes {map.nodes.Count}    Edges {Mathf.Min(map.edgeA.Count, map.edgeB.Count)}    Lights {map.signalNodes.Count}",
                    EditorStyles.miniLabel);
                EditorGUILayout.HelpBox(
                    "Add: click empty space.   Move: drag a node.   Connect: click two nodes.   " +
                    "Signal: click a node to toggle a traffic light.   Erase: click a node (or select + Delete).",
                    MessageType.None);
            }
        }

        Rect CanvasRect()
        {
            const float top = 96f;
            var area = new Rect(8, top, position.width - 16, position.height - top - 8);
            if (map.mapImage == null) return area;
            float ar = (float)map.mapImage.height / Mathf.Max(1, map.mapImage.width);
            float w = area.width, h = w * ar;
            if (h > area.height) { h = area.height; w = h / Mathf.Max(0.0001f, ar); }
            return new Rect(area.x + (area.width - w) * 0.5f, area.y, w, h);
        }

        void DrawCanvas()
        {
            Rect rect = CanvasRect();
            if (map.mapImage != null) GUI.DrawTexture(rect, map.mapImage, ScaleMode.StretchToFill);
            else EditorGUI.DrawRect(rect, new Color(0.18f, 0.2f, 0.18f));

            Handles.BeginGUI();
            float wpx = map.streetWidthFrac > 0.0001f ? Mathf.Max(1f, map.streetWidthFrac * rect.width) : 2f;
            Handles.color = new Color(0.95f, 0.85f, 0.2f, 0.55f);
            int edges = Mathf.Min(map.edgeA.Count, map.edgeB.Count);
            for (int i = 0; i < edges; i++)
            {
                int a = map.edgeA[i], b = map.edgeB[i];
                if (a < map.nodes.Count && b < map.nodes.Count)
                    Handles.DrawAAPolyLine(wpx, ToScreen(map.nodes[a], rect), ToScreen(map.nodes[b], rect));
            }
            Handles.EndGUI();

            for (int i = 0; i < map.nodes.Count; i++)
            {
                Vector2 sp = ToScreen(map.nodes[i], rect);
                bool sig = map.signalNodes.Contains(i);
                Color c = i == selected ? Color.white : sig ? new Color(1f, 0.35f, 0.35f) : new Color(0.25f, 0.7f, 1f);
                float r = i == connectFrom ? 9f : 7f;
                EditorGUI.DrawRect(new Rect(sp.x - r, sp.y - r, r * 2, r * 2), c);
            }

            HandleMouse(rect);
            Repaint();
        }

        Vector2 ToScreen(Vector2 uv, Rect rect) => new Vector2(rect.x + uv.x * rect.width, rect.y + uv.y * rect.height);
        Vector2 ToUV(Vector2 sp, Rect rect) => new Vector2(Mathf.Clamp01((sp.x - rect.x) / rect.width), Mathf.Clamp01((sp.y - rect.y) / rect.height));

        int PickNode(Vector2 sp, Rect rect)
        {
            for (int i = 0; i < map.nodes.Count; i++)
                if (Vector2.Distance(sp, ToScreen(map.nodes[i], rect)) < 11f) return i;
            return -1;
        }

        void HandleMouse(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                int hit = PickNode(e.mousePosition, rect);
                switch (mode)
                {
                    case Mode.Add:
                        if (hit < 0) { map.nodes.Add(ToUV(e.mousePosition, rect)); selected = map.nodes.Count - 1; MarkDirty(); }
                        else selected = hit;
                        break;
                    case Mode.Move:
                        selected = dragging = hit;
                        break;
                    case Mode.Connect:
                        if (hit >= 0)
                        {
                            if (connectFrom < 0) connectFrom = hit;
                            else { if (connectFrom != hit) ToggleEdge(connectFrom, hit); connectFrom = -1; }
                        }
                        break;
                    case Mode.Signal:
                        if (hit >= 0) ToggleSignal(hit);
                        break;
                    case Mode.Erase:
                        if (hit >= 0) EraseNode(hit);
                        break;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && mode == Mode.Move && dragging >= 0)
            {
                map.nodes[dragging] = ToUV(e.mousePosition, rect); MarkDirty(); e.Use();
            }
            else if (e.type == EventType.MouseUp) dragging = -1;
        }

        void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && selected >= 0)
            {
                EraseNode(selected); e.Use();
            }
        }

        void ToggleEdge(int a, int b)
        {
            for (int i = 0; i < map.edgeA.Count; i++)
                if ((map.edgeA[i] == a && map.edgeB[i] == b) || (map.edgeA[i] == b && map.edgeB[i] == a))
                {
                    map.edgeA.RemoveAt(i); map.edgeB.RemoveAt(i); MarkDirty(); return;
                }
            map.edgeA.Add(a); map.edgeB.Add(b); MarkDirty();
        }

        void ToggleSignal(int n)
        {
            if (!map.signalNodes.Remove(n)) map.signalNodes.Add(n);
            MarkDirty();
        }

        void EraseNode(int idx)
        {
            for (int i = map.edgeA.Count - 1; i >= 0; i--)
                if (map.edgeA[i] == idx || map.edgeB[i] == idx) { map.edgeA.RemoveAt(i); map.edgeB.RemoveAt(i); }
            map.signalNodes.Remove(idx);
            map.nodes.RemoveAt(idx);

            for (int i = 0; i < map.edgeA.Count; i++) { if (map.edgeA[i] > idx) map.edgeA[i]--; if (map.edgeB[i] > idx) map.edgeB[i]--; }
            for (int i = 0; i < map.signalNodes.Count; i++) if (map.signalNodes[i] > idx) map.signalNodes[i]--;

            if (selected == idx) selected = -1; else if (selected > idx) selected--;
            connectFrom = -1;
            MarkDirty();
        }

        void ClearGraph()
        {
            map.nodes.Clear(); map.edgeA.Clear(); map.edgeB.Clear(); map.signalNodes.Clear();
            selected = connectFrom = dragging = -1;
            MarkDirty();
        }

        void MarkDirty() { if (map != null) EditorUtility.SetDirty(map); }
        void Save() { MarkDirty(); AssetDatabase.SaveAssets(); }
    }
}
#endif
