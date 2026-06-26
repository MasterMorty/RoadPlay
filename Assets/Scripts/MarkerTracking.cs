using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RoadPlay
{
    /// <summary>
    /// Image-tracking flow (the real RoadPlay concept on iOS):
    ///   - the printed MAP image anchors the road graph (authored in the Map Editor);
    ///   - the PLAYER marker drives the player car (its position follows the marker);
    ///   - every other marker spawns an autonomous AI car that obeys the traffic rules.
    /// Reference images are matched by their name in the AR Reference Image Library.
    /// </summary>
    public class MarkerTracking : MonoBehaviour
    {
        public RoadMap roadMap;
        public XRReferenceImageLibrary library;
        public int maxMovingImages = 10;
        public int maxAiCars = 8;

        [Tooltip("Rotation applied to the road root relative to the tracked image. " +
                 "If the town stands up vertically, try (0,0,0) or (-90,0,0).")]
        public Vector3 mapPlaneEuler = Vector3.zero;

        public string mapImageName = "map";
        public string playerImageName = "player";

        static readonly Color[] CarColors =
        {
            new Color(0.2f, 0.5f, 0.95f), new Color(0.95f, 0.8f, 0.2f),
            new Color(0.6f, 0.3f, 0.8f), new Color(0.95f, 0.55f, 0.2f),
            new Color(0.3f, 0.8f, 0.55f), new Color(0.85f, 0.3f, 0.5f),
        };

        ARTrackedImageManager manager;
        Transform mapRoot;
        RoadNetwork net;
        TrafficLightController signals;
        RuleEngine rules;
        FeedbackManager fx;
        PlayerCar player;

        readonly List<AICar> ais = new List<AICar>();
        readonly Dictionary<TrackableId, AICar> markerCars = new Dictionary<TrackableId, AICar>();
        int colorIdx;
        bool mapBuilt;

        public void Setup(RoadMap map, XRReferenceImageLibrary lib)
        {
            roadMap = map;
            library = lib;
        }

        void Start()
        {
            fx = gameObject.AddComponent<FeedbackManager>();
            EnsureAudioListener();
            EnsureLight();

            var planes = FindAnyObjectByType<ARPlaneManager>();
            if (planes != null) planes.enabled = false;

            var origin = FindAnyObjectByType<XROrigin>();
            var host = origin != null ? origin.gameObject : gameObject;
            manager = host.GetComponent<ARTrackedImageManager>();
            if (manager == null) manager = host.AddComponent<ARTrackedImageManager>();

            manager.enabled = false;
            manager.trackablesChanged.AddListener(OnChanged);
            if (library != null) manager.referenceLibrary = library;
            manager.requestedMaxNumberOfMovingImages = maxMovingImages;
            manager.enabled = true;
        }

        void OnDestroy()
        {
            if (manager != null) manager.trackablesChanged.RemoveListener(OnChanged);
        }

        void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> e)
        {
            foreach (var img in e.added) Handle(img);
            foreach (var img in e.updated) Handle(img);
        }

        void Handle(ARTrackedImage img)
        {
            if (img.trackingState == TrackingState.None) return;
            string n = (img.referenceImage.name ?? "").ToLowerInvariant();

            if (n.Contains(playerImageName.ToLowerInvariant()) || n.Contains("player"))
            {
                if (mapBuilt) HandlePlayer(img);
                return;
            }
            if (n.Contains(mapImageName.ToLowerInvariant()) || n.Contains("map"))
            {
                HandleMap(img);
                return;
            }
            if (mapBuilt) HandleAiMarker(img);
        }

        void HandleMap(ARTrackedImage img)
        {
            if (mapBuilt) return;

            mapBuilt = true;
            Traffic.Clear();
            ais.Clear();
            markerCars.Clear();
            var go = new GameObject("RoadPlayRoot");
            go.transform.SetParent(img.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(mapPlaneEuler);
            mapRoot = go.transform;

            float sx = img.size.x > 0.0001f ? img.size.x : roadMap.physicalWidthMeters;
            float aspect = (roadMap.mapImage != null && roadMap.mapImage.width > 0)
                ? (float)roadMap.mapImage.height / roadMap.mapImage.width : 1f;
            float sz = img.size.y > 0.0001f ? img.size.y : sx * aspect;

            net = go.AddComponent<RoadNetwork>();
            net.map = roadMap;
            net.sizeX = sx;
            net.sizeZ = sz;
            net.drawRoads = false;
            net.laneWidth = roadMap.streetWidthFrac > 0.0001f
                ? roadMap.streetWidthFrac * sx * 0.5f
                : Mathf.Max(0.01f, Mathf.Min(sx, sz) * 0.05f);
            net.contentScale = Mathf.Min(sx, sz) * 0.7f;
            net.Build();

            signals = go.AddComponent<TrafficLightController>();
            signals.Build(net);

            var pgo = new GameObject("PlayerCar");
            pgo.transform.SetParent(mapRoot, false);
            player = pgo.AddComponent<PlayerCar>();
            player.InitMarker(net);
            pgo.SetActive(false);

            rules = go.AddComponent<RuleEngine>();
            rules.Init(net, signals, player, ais, fx);
        }

        void HandlePlayer(ARTrackedImage img)
        {
            if (player == null || mapRoot == null) return;
            if (!player.gameObject.activeSelf) player.gameObject.SetActive(true);
            Vector3 local = mapRoot.InverseTransformPoint(img.transform.position);
            player.SetMarkerTarget(local);
        }

        void HandleAiMarker(ARTrackedImage img)
        {
            if (markerCars.ContainsKey(img.trackableId)) return;
            if (ais.Count >= maxAiCars) return;

            Vector3 local = mapRoot.InverseTransformPoint(img.transform.position);
            int from = net.NearestNode(local);
            if (net.Neighbors(from).Count == 0) return;
            ChooseSpawn(from, out int to, out float startT);

            var go = new GameObject("AICar_marker");
            go.transform.SetParent(mapRoot, false);
            var car = go.AddComponent<AICar>();
            car.Init(net, signals, from, to, CarColors[colorIdx++ % CarColors.Length], ais.Count, startT);
            ais.Add(car);
            markerCars[img.trackableId] = car;
        }

        void ChooseSpawn(int from, out int to, out float startT)
        {
            var neighbors = net.Neighbors(from);
            float minGap = Mathf.Max(net.laneWidth * 1.2f, net.ContentScale * 0.08f);
            float minGapSq = minGap * minGap;
            float[] ts = { 0f, 0.25f, 0.5f, 0.75f };

            int bestTo = neighbors[0];
            float bestT = 0f, bestClear = -1f;
            int start = Random.Range(0, neighbors.Count);
            for (int k = 0; k < neighbors.Count; k++)
            {
                int n = neighbors[(start + k) % neighbors.Count];
                net.Lane(from, n, out var entry, out var exit);
                foreach (float t in ts)
                {
                    Vector3 p = Vector3.Lerp(entry, exit, t);
                    float clearSq = NearestVehicleSq(p);
                    if (clearSq >= minGapSq) { to = n; startT = t; return; }
                    if (clearSq > bestClear) { bestClear = clearSq; bestTo = n; bestT = t; }
                }
            }
            to = bestTo;
            startT = bestT;
        }

        static float NearestVehicleSq(Vector3 p)
        {
            float best = float.MaxValue;
            var agents = Traffic.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                Vector3 d = agents[i].VLocalPos - p; d.y = 0f;
                float sq = d.sqrMagnitude;
                if (sq < best) best = sq;
            }
            return best;
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

            if (!mapBuilt)
            {
                var r = new Rect(sw * 0.08f, sh * 0.34f, sw * 0.84f, sh * 0.18f);
                Dim(r);
                GUI.Label(r, "RoadPlay\n\nPoint the camera at the printed MAP to anchor the city", center);

                var dbg = new GUIStyle(center) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(sh * 0.022f) };
                var dr = new Rect(sw * 0.06f, sh * 0.54f, sw * 0.88f, sh * 0.42f);
                Dim(dr);
                string txt = $"  library: {(library != null ? library.name : "<NONE — assign it!>")}\n";
                txt += $"  roadMap: {(roadMap != null ? roadMap.name : "<NONE — assign it!>")}\n";
                int n = 0;
                if (manager != null)
                {
                    txt += $"  manager enabled: {manager.enabled}   maxMoving: {manager.requestedMaxNumberOfMovingImages}\n";
                    txt += "  images seen:\n";
                    foreach (var t in manager.trackables)
                    {
                        txt += $"    • {t.referenceImage.name} : {t.trackingState}\n";
                        n++;
                    }
                    if (n == 0) txt += "    (none yet — hold steady, fill the frame,\n     more light, less glare)";
                }
                else txt += "  manager: <null>";
                GUI.Label(dr, txt, dbg);
                return;
            }

            var hud = new GUIStyle(center) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(sh * 0.028f) };
            var hudRect = new Rect(sw * 0.04f, sh * 0.04f, sw * 0.72f, sh * 0.2f);
            Dim(hudRect);
            GUI.Label(hudRect,
                $"  Followed: {rules.cleanCrossings}\n  Violations: {rules.violations}\n  Cars: {ais.Count}   Lights A/B: {signals.NS}/{signals.EW}",
                hud);

            var hint = new GUIStyle(center) { alignment = TextAnchor.LowerCenter, fontSize = Mathf.RoundToInt(sh * 0.024f) };
            GUI.Label(new Rect(sw * 0.05f, sh * 0.84f, sw * 0.9f, sh * 0.14f),
                "Show a CAR marker to add traffic   ·   move the PLAYER marker to drive", hint);

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

        static void Dim(Rect r)
        {
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
