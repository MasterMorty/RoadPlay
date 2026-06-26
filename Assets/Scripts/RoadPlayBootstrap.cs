using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RoadPlay
{
    /// <summary>
    /// Entry point. Builds the whole RoadPlay prototype from code so the scene
    /// only needs an AR Session, an AR rig, and this component.
    ///
    /// Flow: scan a plane -> tap to anchor the road carpet -> drive the player
    /// car and watch the rule engine react. Spatial anchoring here is the
    /// plane-locked root under ARKit world tracking; on Meta Quest this becomes
    /// a marker/anchor-tracked carpet and a tracked physical car.
    /// </summary>
    public class RoadPlayBootstrap : MonoBehaviour
    {
        [Header("Mode")]
        [Tooltip("ON: detect a printed map image + ArUco-style markers (the real concept). " +
                 "OFF: tap a detected plane to place a procedural town.")]
        public bool useImageTracking;
        [Tooltip("Image-tracking mode: the authored road graph for your printed map.")]
        public RoadMap roadMap;
        [Tooltip("Image-tracking mode: AR Reference Image Library holding the map + marker images.")]
        public XRReferenceImageLibrary referenceImageLibrary;
        [Tooltip("Image-tracking mode: rotation of the town relative to the tracked map image. " +
                 "If the city stands up vertically, try (0,0,0) or (-90,0,0).")]
        public Vector3 mapPlaneEuler = Vector3.zero;

        [Header("Tap-to-place mode")]
        [Tooltip("Scales the whole road board. 1 = ~1.2 m across; lower it for a small table.")]
        public float boardScale = 1.0f;

        ARRaycastManager raycaster;
        ARPlaneManager planeManager;
        readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        bool placed;
        Transform root;

        RoadNetwork net;
        TrafficLightController signals;
        RuleEngine rules;
        FeedbackManager fx;
        PlayerCar player;
        readonly List<AICar> ais = new List<AICar>();

        void Start()
        {
            if (useImageTracking)
            {
                var mt = gameObject.AddComponent<MarkerTracking>();
                mt.mapPlaneEuler = mapPlaneEuler;
                mt.Setup(roadMap, referenceImageLibrary);
                enabled = false;
                return;
            }

            raycaster = FindAnyObjectByType<ARRaycastManager>();
            planeManager = FindAnyObjectByType<ARPlaneManager>();
            EnsureAudioListener();
            EnsureLight();
            fx = gameObject.AddComponent<FeedbackManager>();
        }

        void EnsureAudioListener()
        {
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
        }

        void EnsureLight()
        {
            if (FindAnyObjectByType<Light>() != null) return;
            var go = new GameObject("Sun");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        void Update()
        {
            if (!placed) TryPlace();
        }

        void TryPlace()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 screen = pointer.position.ReadValue();
            Pose pose;

            if (raycaster != null && raycaster.Raycast(screen, hits, TrackableType.PlaneWithinPolygon))
            {
                pose = hits[0].pose;
            }
            else
            {
                var cam = Camera.main;
                if (cam == null) return;
                Vector3 pos = cam.transform.position + cam.transform.forward * 0.5f + Vector3.down * 0.2f;
                pose = new Pose(pos, Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f));
            }

            Place(pose);
        }

        void Place(Pose pose)
        {
            placed = true;

            var go = new GameObject("RoadPlayRoot");
            go.transform.SetPositionAndRotation(pose.position, Quaternion.Euler(0f, pose.rotation.eulerAngles.y, 0f));
            go.transform.localScale = Vector3.one * boardScale;
            root = go.transform;

            Traffic.Clear();
            ais.Clear();

            net = go.AddComponent<RoadNetwork>();
            net.Build();

            signals = go.AddComponent<TrafficLightController>();
            signals.Build(net);

            var colors = new[]
            {
                new Color(0.2f, 0.5f, 0.95f),
                new Color(0.95f, 0.8f, 0.2f),
                new Color(0.6f, 0.3f, 0.8f),
                new Color(0.95f, 0.55f, 0.2f),
                new Color(0.3f, 0.8f, 0.55f),
            };
            const int carCount = 8;
            var usedEdges = new HashSet<long>();
            int spawned = 0, tries = 0;
            while (spawned < carCount && tries++ < 200)
            {
                int from = Random.Range(0, net.Nodes.Count);
                var neighbors = net.Neighbors(from);
                int to = neighbors[Random.Range(0, neighbors.Count)];

                long edgeKey = (long)from << 32 | (uint)to;
                if (!usedEdges.Add(edgeKey)) continue;

                var carGo = new GameObject("AICar" + spawned);
                carGo.transform.SetParent(root, false);
                var car = carGo.AddComponent<AICar>();
                float startT = Random.Range(0.2f, 0.8f);
                car.Init(net, signals, from, to, colors[spawned % colors.Length], spawned, startT);
                ais.Add(car);
                spawned++;
            }

            var playerGo = new GameObject("PlayerCar");
            playerGo.transform.SetParent(root, false);
            player = playerGo.AddComponent<PlayerCar>();
            player.Init(net);

            rules = go.AddComponent<RuleEngine>();
            rules.Init(net, signals, player, ais, fx);

            if (planeManager != null)
            {
                planeManager.enabled = false;
                foreach (var plane in planeManager.trackables)
                    plane.gameObject.SetActive(false);
            }
        }

        void OnGUI()
        {
            float sw = Screen.width, sh = Screen.height;

            if (fx != null && fx.ShowFlash)
            {
                var prev = GUI.color;
                GUI.color = fx.FlashColor;
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var center = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                fontSize = Mathf.RoundToInt(sh * 0.03f)
            };
            center.normal.textColor = Color.white;

            if (!placed)
            {
                var r = new Rect(sw * 0.08f, sh * 0.40f, sw * 0.84f, sh * 0.2f);
                DimBox(r);
                GUI.Label(r, "RoadPlay\n\nScan a flat surface (table or floor),\nthen TAP to place the road", center);
                return;
            }

            var hud = new GUIStyle(center) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(sh * 0.028f) };
            var hudRect = new Rect(sw * 0.04f, sh * 0.04f, sw * 0.7f, sh * 0.2f);
            DimBox(hudRect);
            GUI.Label(hudRect,
                $"  Followed: {rules.cleanCrossings}\n  Violations: {rules.violations}\n  Lights A/B: {signals.NS}/{signals.EW}",
                hud);

            var hint = new GUIStyle(center) { alignment = TextAnchor.LowerCenter, fontSize = Mathf.RoundToInt(sh * 0.024f) };
            GUI.Label(new Rect(sw * 0.05f, sh * 0.82f, sw * 0.9f, sh * 0.16f),
                "Hold to drive   ·   slide left / right to steer   ·   release to brake", hint);

            if (fx.ShowBanner)
            {
                var br = new Rect(sw * 0.1f, sh * 0.3f, sw * 0.8f, sh * 0.14f);
                var prev = GUI.color;
                GUI.color = fx.BannerColor;
                GUI.DrawTexture(br, Texture2D.whiteTexture);
                GUI.color = prev;

                var bs = new GUIStyle(center) { fontSize = Mathf.RoundToInt(sh * 0.06f) };
                bs.normal.textColor = Color.white;
                GUI.Label(br, fx.Banner, bs);
            }
        }

        static void DimBox(Rect r)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
