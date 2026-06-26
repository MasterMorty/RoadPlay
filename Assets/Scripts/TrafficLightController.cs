using System.Collections.Generic;
using UnityEngine;

namespace RoadPlay
{
    public enum Signal { Red, Yellow, Green }

    /// <summary>
    /// Drives every signalised intersection through a green / yellow / red cycle.
    ///
    /// Each intersection controls itself based on its OWN incoming roads, not the
    /// world axes, so a hand-drawn map whose roads are rotated or skewed still
    /// works. For each signal node we pick a local reference axis (the first
    /// incoming road), then split all approaches into two phase groups:
    ///   - group A = roads roughly parallel to that axis (the "through" road),
    ///   - group B = the crossing roads.
    /// One global timer alternates which group is green, so all intersections stay
    /// in sync. A car asks <see cref="SignalFor"/> for the light that governs its
    /// exact approach edge instead of guessing from world North/South/East/West.
    /// </summary>
    public class TrafficLightController : MonoBehaviour
    {
        public float greenTime = 5f;
        public float yellowTime = 1.5f;

        // NS == group A, EW == group B.
        public Signal NS { get; private set; } = Signal.Green;
        public Signal EW { get; private set; } = Signal.Red;

        float timer;
        int phase; // 0: A green, 1: A yellow, 2: B green, 3: B yellow

        RoadNetwork net;
        readonly List<Inter> inters = new List<Inter>();

        /// <summary>One signalised intersection: its local axis + heads per group.</summary>
        class Inter
        {
            public int node;
            public Vector3 axis;
            public readonly List<LightHead> a = new List<LightHead>();
            public readonly List<LightHead> b = new List<LightHead>();
        }

        public void Build(RoadNetwork network)
        {
            net = network;
            float scale = net.ContentScale * 1.15f;

            foreach (int s in net.SignalNodes)
            {
                var it = new Inter { node = s };
                Vector3 c = net.Nodes[s];
                var nbrs = net.Neighbors(s);

                it.axis = Vector3.forward;
                if (nbrs.Count > 0)
                {
                    Vector3 d0 = Flat(c - net.Nodes[nbrs[0]]);
                    if (d0.sqrMagnitude > 1e-8f) it.axis = d0.normalized;
                }

                foreach (int nb in nbrs)
                {
                    Vector3 travel = Flat(c - net.Nodes[nb]);
                    if (travel.sqrMagnitude < 1e-8f) continue;
                    travel.Normalize();

                    int g = GroupOf(it, travel);
                    Vector3 right = new Vector3(travel.z, 0f, -travel.x);
                    float stop = net.RoadHalf + 0.6f * net.laneWidth;
                    Vector3 pos = c - travel * stop + right * (net.RoadHalf + 0.15f * net.laneWidth);

                    var head = LightHead.Create(transform, g == 0 ? "Light_A" : "Light_B", pos);
                    head.transform.localScale = Vector3.one * scale;
                    (g == 0 ? it.a : it.b).Add(head);
                }

                inters.Add(it);
            }
            Apply();
        }

        void Update()
        {
            timer += Time.deltaTime;
            float dur = (phase == 0 || phase == 2) ? greenTime : yellowTime;
            if (timer >= dur)
            {
                timer = 0f;
                phase = (phase + 1) % 4;
                Apply();
            }
        }

        void Apply()
        {
            NS = phase == 0 ? Signal.Green : phase == 1 ? Signal.Yellow : Signal.Red;
            EW = phase == 2 ? Signal.Green : phase == 3 ? Signal.Yellow : Signal.Red;
            foreach (var it in inters)
            {
                foreach (var h in it.a) h.Set(NS);
                foreach (var h in it.b) h.Set(EW);
            }
        }

        /// <summary>Light a car obeys when driving the edge fromNode -> node.</summary>
        public Signal SignalFor(int node, int fromNode)
        {
            var it = Find(node);
            if (it == null) return Signal.Green; // uncontrolled
            return SignalForHeading(node, net.Nodes[node] - net.Nodes[fromNode]);
        }

        /// <summary>Light governing something arriving at node along travelDir (XZ).</summary>
        public Signal SignalForHeading(int node, Vector3 travelDir)
        {
            var it = Find(node);
            if (it == null) return Signal.Green;
            travelDir = Flat(travelDir);
            if (travelDir.sqrMagnitude < 1e-8f) return Signal.Green;
            return GroupOf(it, travelDir.normalized) == 0 ? NS : EW;
        }

        Inter Find(int node)
        {
            for (int i = 0; i < inters.Count; i++)
                if (inters[i].node == node) return inters[i];
            return null;
        }

        static int GroupOf(Inter it, Vector3 travelDir)
            => Mathf.Abs(Vector3.Dot(travelDir, it.axis)) >= 0.5f ? 0 : 1;

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    /// <summary>A single three-light signal head (pole + box + 3 lamps).</summary>
    public class LightHead : MonoBehaviour
    {
        static readonly Color RedOn = new Color(1f, 0.12f, 0.12f), RedOff = new Color(0.22f, 0.04f, 0.04f);
        static readonly Color YelOn = new Color(1f, 0.85f, 0.12f), YelOff = new Color(0.28f, 0.24f, 0.05f);
        static readonly Color GrnOn = new Color(0.15f, 1f, 0.25f), GrnOff = new Color(0.04f, 0.22f, 0.08f);

        Material redM, yelM, grnM;

        public static LightHead Create(Transform parent, string name, Vector3 localPos)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            var lh = root.AddComponent<LightHead>();
            lh.BuildVisual();
            return lh;
        }

        void BuildVisual()
        {
            var poleMat = Prims.MakeMaterial(new Color(0.2f, 0.2f, 0.22f));
            var boxMat = Prims.MakeMaterial(new Color(0.09f, 0.09f, 0.09f));
            Prims.Cylinder(transform, "pole", new Vector3(0, 0.025f, 0), new Vector3(0.005f, 0.025f, 0.005f), poleMat);
            Prims.Box(transform, "head", new Vector3(0, 0.062f, 0), new Vector3(0.016f, 0.036f, 0.014f), boxMat);

            redM = Prims.MakeMaterial(RedOff, true);
            yelM = Prims.MakeMaterial(YelOff, true);
            grnM = Prims.MakeMaterial(GrnOff, true);
            Prims.Sphere(transform, "red", new Vector3(0, 0.072f, 0.008f), 0.01f, redM);
            Prims.Sphere(transform, "yellow", new Vector3(0, 0.062f, 0.008f), 0.01f, yelM);
            Prims.Sphere(transform, "green", new Vector3(0, 0.052f, 0.008f), 0.01f, grnM);
        }

        public void Set(Signal s)
        {
            Apply(redM, s == Signal.Red ? RedOn : RedOff);
            Apply(yelM, s == Signal.Yellow ? YelOn : YelOff);
            Apply(grnM, s == Signal.Green ? GrnOn : GrnOff);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        static void Apply(Material m, Color c)
        {
            if (m == null) return;
            Prims.SetColor(m, c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 3f);
        }
    }
}
