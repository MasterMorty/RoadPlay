using System.Collections.Generic;
using UnityEngine;

namespace RoadPlay
{
    public enum Signal { Red, Yellow, Green }

    /// <summary>
    /// Drives the intersection's two signal heads (North-South and East-West)
    /// through a green / yellow / red cycle.
    /// </summary>
    public class TrafficLightController : MonoBehaviour
    {
        public float greenTime = 5f;
        public float yellowTime = 1.5f;

        public Signal NS { get; private set; } = Signal.Green;
        public Signal EW { get; private set; } = Signal.Red;

        float timer;
        int phase; // 0: NS green, 1: NS yellow, 2: EW green, 3: EW yellow
        readonly List<LightHead> nsHeads = new List<LightHead>();
        readonly List<LightHead> ewHeads = new List<LightHead>();

        public void Build(RoadNetwork net)
        {
            float corner = net.RoadHalf + 0.5f * net.laneWidth;
            float scale = net.ContentScale;
            foreach (var s in net.SignalNodes)
            {
                Vector3 c = net.Nodes[s];
                var ns = LightHead.Create(transform, "Light_NS", c + new Vector3(-corner, 0, -corner));
                var ew = LightHead.Create(transform, "Light_EW", c + new Vector3(corner, 0, -corner));
                ns.transform.localScale = Vector3.one * scale;
                ew.transform.localScale = Vector3.one * scale;
                nsHeads.Add(ns);
                ewHeads.Add(ew);
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
            switch (phase)
            {
                case 0: NS = Signal.Green; EW = Signal.Red; break;
                case 1: NS = Signal.Yellow; EW = Signal.Red; break;
                case 2: NS = Signal.Red; EW = Signal.Green; break;
                default: NS = Signal.Red; EW = Signal.Yellow; break;
            }
            foreach (var h in nsHeads) h.Set(NS);
            foreach (var h in ewHeads) h.Set(EW);
        }
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
            // Unity cylinder is 2m tall at scale 1, so y-scale 0.04 => 0.08m pole.
            Prims.Cylinder(transform, "pole", new Vector3(0, 0.04f, 0), new Vector3(0.006f, 0.04f, 0.006f), poleMat);
            Prims.Box(transform, "head", new Vector3(0, 0.10f, 0), new Vector3(0.02f, 0.05f, 0.018f), boxMat);

            redM = Prims.MakeMaterial(RedOff, true);
            yelM = Prims.MakeMaterial(YelOff, true);
            grnM = Prims.MakeMaterial(GrnOff, true);
            Prims.Sphere(transform, "red", new Vector3(0, 0.115f, 0.01f), 0.013f, redM);
            Prims.Sphere(transform, "yellow", new Vector3(0, 0.10f, 0.01f), 0.013f, yelM);
            Prims.Sphere(transform, "green", new Vector3(0, 0.085f, 0.01f), 0.013f, grnM);
        }

        public void Set(Signal s)
        {
            Apply(redM, s == Signal.Red ? RedOn : RedOff);
            Apply(yelM, s == Signal.Yellow ? YelOn : YelOff);
            Apply(grnM, s == Signal.Green ? GrnOn : GrnOff);
        }

        static void Apply(Material m, Color c)
        {
            if (m == null) return;
            Prims.SetColor(m, c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 3f);
        }
    }
}
