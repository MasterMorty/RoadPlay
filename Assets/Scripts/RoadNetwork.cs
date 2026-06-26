using System.Collections.Generic;
using UnityEngine;

namespace RoadPlay
{
    /// <summary>
    /// The road graph cars drive on. Two ways to build it:
    ///   1. procedural town  (a gridSize x gridSize grid + drawn roads/decor), or
    ///   2. from a <see cref="RoadMap"/> authored over a printed map image
    ///      (graph only, no art - the printed map supplies the visuals).
    /// Right-hand traffic. Axes: +Z North, +X East, +Y up.
    /// </summary>
    public class RoadNetwork : MonoBehaviour
    {
        public int gridSize = 4;
        public float cell = 0.28f;
        public float margin = 0.16f;

        public RoadMap map;
        public float sizeX = 0.3f, sizeZ = 0.3f;
        public bool drawRoads = true;

        public float laneWidth = 0.05f;

        public float RoadWidth => laneWidth * 2f;
        public float RoadHalf => laneWidth;
        public float LaneOffset => laneWidth * 0.5f;
        public float Span => (gridSize - 1) * cell;

        public float contentScale = 1f;
        public float ContentScale => contentScale;

        public List<Vector3> Nodes { get; private set; } = new List<Vector3>();
        public List<int> SignalNodes { get; private set; } = new List<int>();
        public int CenterNode { get; private set; }
        public int SouthEntryNode { get; private set; }
        public float BoardHalf { get; private set; } = 0.5f;

        List<int>[] adj;
        readonly HashSet<int> signalSet = new HashSet<int>();

        int Idx(int ix, int iz) => ix * gridSize + iz;
        public List<int> Neighbors(int n) => adj[n];
        public bool IsSignalized(int n) => signalSet.Contains(n);

        public void Build()
        {
            if (map != null && map.nodes.Count > 0) BuildFromMap();
            else BuildProcedural();
        }

        public int NearestNode(Vector3 local)
        {
            int best = 0;
            float bestSq = float.MaxValue;
            for (int i = 0; i < Nodes.Count; i++)
            {
                float dx = Nodes[i].x - local.x, dz = Nodes[i].z - local.z;
                float sq = dx * dx + dz * dz;
                if (sq < bestSq) { bestSq = sq; best = i; }
            }
            return best;
        }

        /// Right-offset entry/exit points for travelling a -> b.
        public void Lane(int a, int b, out Vector3 entry, out Vector3 exit)
        {
            Vector3 pa = Nodes[a], pb = Nodes[b];
            Vector3 dir = (pb - pa).normalized;
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            entry = pa + right * LaneOffset;
            exit = pb + right * LaneOffset;
        }

        void InitAdjacency(int count)
        {
            adj = new List<int>[count];
            for (int k = 0; k < count; k++) adj[k] = new List<int>();
        }

        void Connect(int a, int b) { adj[a].Add(b); adj[b].Add(a); }

        void BuildFromMap()
        {
            Nodes.Clear();
            for (int i = 0; i < map.nodes.Count; i++)
                Nodes.Add(map.LocalPosition(i, sizeX, sizeZ));

            InitAdjacency(Nodes.Count);
            int edges = Mathf.Min(map.edgeA.Count, map.edgeB.Count);
            for (int i = 0; i < edges; i++)
            {
                int a = map.edgeA[i], b = map.edgeB[i];
                if (a >= 0 && b >= 0 && a < Nodes.Count && b < Nodes.Count && a != b)
                    Connect(a, b);
            }

            SignalNodes.Clear();
            signalSet.Clear();
            foreach (int s in map.signalNodes)
                if (s >= 0 && s < Nodes.Count && signalSet.Add(s))
                    SignalNodes.Add(s);

            CenterNode = Nodes.Count > 0 ? NearestNode(Vector3.zero) : 0;
            float maxExtent = 0f;
            int southMost = 0; float minZ = float.MaxValue;
            for (int i = 0; i < Nodes.Count; i++)
            {
                maxExtent = Mathf.Max(maxExtent, Mathf.Max(Mathf.Abs(Nodes[i].x), Mathf.Abs(Nodes[i].z)));
                if (Nodes[i].z < minZ) { minZ = Nodes[i].z; southMost = i; }
            }
            SouthEntryNode = southMost;
            BoardHalf = maxExtent + 0.05f;

            if (drawRoads) DrawEdges();
        }

        void DrawEdges()
        {
            var road = Prims.MakeMaterial(new Color(0.62f, 0.63f, 0.66f));
            var drawn = new HashSet<long>();
            for (int a = 0; a < adj.Length; a++)
                foreach (int b in adj[a])
                {
                    long key = a < b ? ((long)a << 32 | (uint)b) : ((long)b << 32 | (uint)a);
                    if (!drawn.Add(key)) continue;
                    Vector3 pa = Nodes[a], pb = Nodes[b], mid = (pa + pb) * 0.5f;
                    Vector3 dir = (pb - pa).normalized;
                    var seg = Prims.Box(transform, "road", mid + new Vector3(0, 0.001f, 0),
                        new Vector3(RoadWidth, 0.003f, Vector3.Distance(pa, pb)), road);
                    seg.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                }
        }

        void BuildProcedural()
        {
            BuildGraph();
            BuildRoads();
            BuildDecorations();
        }

        void BuildGraph()
        {
            float half = Span * 0.5f;
            Nodes.Clear();
            for (int ix = 0; ix < gridSize; ix++)
                for (int iz = 0; iz < gridSize; iz++)
                    Nodes.Add(new Vector3(ix * cell - half, 0f, iz * cell - half));

            InitAdjacency(gridSize * gridSize);
            for (int ix = 0; ix < gridSize; ix++)
                for (int iz = 0; iz < gridSize; iz++)
                {
                    if (ix < gridSize - 1) Connect(Idx(ix, iz), Idx(ix + 1, iz));
                    if (iz < gridSize - 1) Connect(Idx(ix, iz), Idx(ix, iz + 1));
                }

            SignalNodes.Clear();
            signalSet.Clear();
            for (int ix = 1; ix < gridSize - 1; ix++)
                for (int iz = 1; iz < gridSize - 1; iz++)
                {
                    int n = Idx(ix, iz);
                    SignalNodes.Add(n);
                    signalSet.Add(n);
                }

            CenterNode = Idx(gridSize / 2, gridSize / 2);
            SouthEntryNode = Idx(gridSize / 2, 0);
            BoardHalf = Span * 0.5f + margin;
        }

        void BuildRoads()
        {
            var grass = Prims.MakeMaterial(new Color(0.32f, 0.56f, 0.30f));
            Prims.Box(transform, "Ground", new Vector3(0, -0.006f, 0),
                new Vector3(BoardHalf * 2f, 0.01f, BoardHalf * 2f), grass);

            var road = Prims.MakeMaterial(new Color(0.62f, 0.63f, 0.66f));
            var white = Prims.MakeMaterial(Color.white);

            var drawn = new HashSet<long>();
            for (int a = 0; a < adj.Length; a++)
                foreach (int b in adj[a])
                {
                    long key = a < b ? ((long)a << 32 | (uint)b) : ((long)b << 32 | (uint)a);
                    if (!drawn.Add(key)) continue;

                    Vector3 pa = Nodes[a], pb = Nodes[b];
                    Vector3 mid = (pa + pb) * 0.5f;
                    Vector3 dir = (pb - pa).normalized;
                    float len = Vector3.Distance(pa, pb);

                    var seg = Prims.Box(transform, "road", mid + new Vector3(0, 0.001f, 0),
                        new Vector3(RoadWidth, 0.004f, len), road);
                    seg.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);

                    BuildDashes(pa, dir, len, white);
                }

            foreach (var n in Nodes)
                Prims.Box(transform, "junction", n + new Vector3(0, 0.0012f, 0),
                    new Vector3(RoadWidth, 0.0042f, RoadWidth), road);

            foreach (var s in SignalNodes) BuildStopBars(Nodes[s], white);
        }

        void BuildDashes(Vector3 pa, Vector3 dir, float len, Material white)
        {
            const float dash = 0.03f, gap = 0.03f, w = 0.005f, t = 0.004f;
            for (float d = RoadHalf + 0.02f; d < len - RoadHalf; d += dash + gap)
            {
                Vector3 p = pa + dir * (d + dash * 0.5f);
                var box = Prims.Box(transform, "dash", p + new Vector3(0, 0.0022f, 0),
                    new Vector3(w, t, dash), white);
                box.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }

        void BuildStopBars(Vector3 c, Material white)
        {
            const float t = 0.004f, barW = 0.012f;
            float off = RoadHalf + barW * 0.5f;
            Prims.Box(transform, "stopN", c + new Vector3(0, 0.0025f, off), new Vector3(RoadWidth, t, barW), white);
            Prims.Box(transform, "stopS", c + new Vector3(0, 0.0025f, -off), new Vector3(RoadWidth, t, barW), white);
            Prims.Box(transform, "stopE", c + new Vector3(off, 0.0025f, 0), new Vector3(barW, t, RoadWidth), white);
            Prims.Box(transform, "stopW", c + new Vector3(-off, 0.0025f, 0), new Vector3(barW, t, RoadWidth), white);
        }

        void BuildDecorations()
        {
            float half = Span * 0.5f;
            var bodyColors = new[]
            {
                new Color(0.85f, 0.2f, 0.2f), new Color(0.3f, 0.55f, 0.85f),
                new Color(0.9f, 0.75f, 0.2f), new Color(0.8f, 0.4f, 0.15f),
                new Color(0.6f, 0.3f, 0.6f), new Color(0.7f, 0.7f, 0.75f),
            };
            int colorIdx = 0;

            for (int ix = 0; ix < gridSize - 1; ix++)
                for (int iz = 0; iz < gridSize - 1; iz++)
                {
                    Vector3 c = new Vector3(ix * cell - half + cell * 0.5f, 0f, iz * cell - half + cell * 0.5f);
                    int kind = (ix + iz) % 4;
                    if (kind == 0 || kind == 1)
                    {
                        var body = bodyColors[colorIdx++ % bodyColors.Length];
                        float h = 0.05f + (kind == 1 ? 0.05f : 0f);
                        AddBuilding(c, new Vector2(0.11f, 0.1f), h, body, body * 0.5f);
                        AddTree(c + new Vector3(cell * 0.28f, 0, cell * 0.28f));
                    }
                    else if (kind == 2)
                    {
                        AddTree(c + new Vector3(-0.05f, 0, 0.05f));
                        AddTree(c + new Vector3(0.05f, 0, -0.04f));
                        AddTree(c + new Vector3(0.04f, 0, 0.06f));
                    }
                    else
                    {
                        AddPond(c, 0.12f);
                    }
                }
        }

        void AddBuilding(Vector3 c, Vector2 foot, float h, Color body, Color roof)
        {
            Prims.Box(transform, "building", c + new Vector3(0, h * 0.5f, 0),
                new Vector3(foot.x, h, foot.y), Prims.MakeMaterial(body));
            Prims.Box(transform, "roof", c + new Vector3(0, h + 0.006f, 0),
                new Vector3(foot.x * 1.15f, 0.012f, foot.y * 1.15f), Prims.MakeMaterial(roof));
        }

        void AddTree(Vector3 p)
        {
            Prims.Cylinder(transform, "trunk", p + new Vector3(0, 0.02f, 0),
                new Vector3(0.012f, 0.02f, 0.012f), Prims.MakeMaterial(new Color(0.42f, 0.27f, 0.13f)));
            Prims.Sphere(transform, "leaves", p + new Vector3(0, 0.05f, 0),
                0.05f, Prims.MakeMaterial(new Color(0.18f, 0.5f, 0.2f)));
        }

        void AddPond(Vector3 p, float diameter)
        {
            Prims.Cylinder(transform, "pond", p + new Vector3(0, 0.0015f, 0),
                new Vector3(diameter, 0.003f, diameter), Prims.MakeMaterial(new Color(0.3f, 0.6f, 0.85f)));
        }
    }
}
