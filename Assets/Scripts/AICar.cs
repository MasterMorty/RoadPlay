using UnityEngine;

namespace RoadPlay
{
    /// <summary>
    /// A virtual car that navigates the road graph and drives with awareness:
    ///   - car-following: brakes smoothly to keep a gap behind whatever is ahead
    ///     in its lane (other AI cars AND the player car);
    ///   - intersection yield: gives way at uncontrolled junctions to a car that
    ///     is closer to the shared node;
    ///   - obeys the traffic light at the centre intersection;
    ///   - hard-brakes + honks + flashes hazards when the rule engine reports
    ///     danger from the player.
    /// Body forward = +Z.
    /// </summary>
    public class AICar : MonoBehaviour, IVehicle
    {
        RoadNetwork net;
        TrafficLightController signals;

        public float maxSpeed = 0.10f;
        public float accel = 0.3f;
        public float turnSpeed = 320f; // deg/s
        public float speed;

        int fromNode, toNode, prevNode;
        int id;
        Vector3 laneExit;

        AudioSource horn;
        Renderer bodyRend;
        Color bodyColor;
        float hazardTimer;
        float forcedBrakeTimer;
        float stuckTimer;
        float breakout; // > 0: temporarily ignore intersection yield to clear a deadlock

        public Vector3 LocalPos => transform.localPosition;
        public Vector3 VLocalPos => transform.localPosition;
        public Vector3 VLocalForward => transform.localRotation * Vector3.forward;
        public int TargetNode => toNode;
        public float DistToTarget => net == null ? float.MaxValue : Vector3.Distance(transform.localPosition, net.Nodes[toNode]);
        public int VehicleId => id;

        public void Init(RoadNetwork network, TrafficLightController traffic, int startFrom, int startTo, Color color, int vehicleId, float startT)
        {
            net = network;
            signals = traffic;
            bodyColor = color;
            fromNode = prevNode = startFrom;
            toNode = startTo;
            id = vehicleId;

            BuildBody(color);
            horn = gameObject.AddComponent<AudioSource>();
            horn.playOnAwake = false;
            horn.spatialBlend = 0f;
            horn.clip = Honk.Clip();

            net.Lane(fromNode, toNode, out var entry, out laneExit);
            // spread cars out along their starting edge so they don't spawn stacked
            transform.localPosition = Vector3.Lerp(entry, laneExit, Mathf.Clamp01(startT));
            transform.localRotation = Quaternion.LookRotation((laneExit - entry).normalized, Vector3.up);

            Traffic.Register(this);
        }

        void BuildBody(Color color)
        {
            var bodyMat = Prims.MakeMaterial(color);
            var b = Prims.Box(transform, "body", new Vector3(0, 0.012f, 0), new Vector3(0.03f, 0.018f, 0.06f), bodyMat);
            bodyRend = b.GetComponent<Renderer>();

            Prims.Box(transform, "roof", new Vector3(0, 0.026f, -0.006f), new Vector3(0.026f, 0.014f, 0.03f), Prims.MakeMaterial(color * 0.7f));
            Prims.Box(transform, "windshield", new Vector3(0, 0.024f, 0.013f), new Vector3(0.024f, 0.012f, 0.006f), Prims.MakeMaterial(new Color(0.4f, 0.6f, 0.85f)));

            var wheelMat = Prims.MakeMaterial(new Color(0.05f, 0.05f, 0.05f));
            const float wx = 0.016f, wz = 0.018f;
            foreach (var sx in new[] { -wx, wx })
                foreach (var sz in new[] { -wz, wz })
                {
                    var w = Prims.Cylinder(transform, "wheel", new Vector3(sx, 0.005f, sz), new Vector3(0.012f, 0.006f, 0.012f), wheelMat);
                    w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
        }

        void Update()
        {
            if (net == null) return;

            Vector3 pos = transform.localPosition;

            if (Vector3.Distance(pos, laneExit) < 0.02f)
                AdvanceEdge();

            // steer toward the next lane point
            Vector3 toTarget = laneExit - pos; toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 1e-6f)
            {
                var want = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, want, turnSpeed * Time.deltaTime);
            }
            Vector3 fwd = transform.localRotation * Vector3.forward;

            if (breakout > 0f) breakout -= Time.deltaTime;

            float desired = maxSpeed;
            desired = Mathf.Min(desired, CarFollowing(pos, fwd));
            desired = Mathf.Min(desired, LightSpeed(pos));
            if (breakout <= 0f) desired = Mathf.Min(desired, NodeYield(pos));

            bool hardBrake = forcedBrakeTimer > 0f;
            if (hardBrake) { forcedBrakeTimer -= Time.deltaTime; desired = 0f; }

            // deadlock breaker: if stopped too long, ignore yield AND crawl forward so two
            // cars can never stay frozen against each other (nudges them apart).
            if (speed < 0.006f && desired < 0.006f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0f;
            if (stuckTimer > 2f) { breakout = 1.5f; stuckTimer = 0f; }
            if (breakout > 0f && !hardBrake && desired < 0.04f) desired = 0.045f;

            speed = Mathf.MoveTowards(speed, desired, accel * Time.deltaTime);
            transform.localPosition = pos + fwd * (speed * Time.deltaTime);

            if (hazardTimer > 0f)
            {
                hazardTimer -= Time.deltaTime;
                bool on = ((int)(hazardTimer * 8f)) % 2 == 0;
                SetBody(on ? new Color(1f, 0.85f, 0.1f) : bodyColor);
            }
        }

        void AdvanceEdge()
        {
            prevNode = fromNode;
            fromNode = toNode;
            toNode = ChooseNext(fromNode, prevNode);
            net.Lane(fromNode, toNode, out _, out laneExit);
        }

        int ChooseNext(int node, int cameFrom)
        {
            var ns = net.Neighbors(node);
            int count = 0;
            foreach (var n in ns) if (n != cameFrom) count++;
            if (count == 0) return cameFrom; // dead-end, U-turn
            int pick = Random.Range(0, count), i = 0;
            foreach (var n in ns)
            {
                if (n == cameFrom) continue;
                if (i == pick) return n;
                i++;
            }
            return cameFrom;
        }

        // brake to keep a gap behind the nearest thing ahead in my lane
        float CarFollowing(Vector3 pos, Vector3 fwd)
        {
            float minGap = float.MaxValue;
            var agents = Traffic.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var v = agents[i];
                if (ReferenceEquals(v, this)) continue;
                Vector3 rel = v.VLocalPos - pos; rel.y = 0f;
                float ahead = Vector3.Dot(rel, fwd);
                if (ahead <= 0.005f) continue;
                Vector3 lateral = rel - fwd * ahead;
                if (lateral.magnitude > net.laneWidth * 1.15f) continue;
                // only brake for cars going roughly the same way; crossing and oncoming
                // cars are handled by the intersection rules, not by car-following.
                if (Vector3.Dot(v.VLocalForward, fwd) < 0.35f) continue;
                if (ahead < minGap) minGap = ahead;
            }

            if (minGap == float.MaxValue) return maxSpeed;
            const float stopGap = 0.05f, slowGap = 0.17f;
            if (minGap <= stopGap) return 0f;
            if (minGap >= slowGap) return maxSpeed;
            return maxSpeed * ((minGap - stopGap) / (slowGap - stopGap));
        }

        float LightSpeed(Vector3 pos)
        {
            if (!net.IsSignalized(toNode)) return maxSpeed;
            Vector3 c = net.Nodes[toNode];
            Vector3 approach = c - net.Nodes[fromNode];
            bool ns = Mathf.Abs(approach.z) >= Mathf.Abs(approach.x);
            Signal sig = ns ? signals.NS : signals.EW;
            if (sig == Signal.Green) return maxSpeed;

            float dist = Vector3.Distance(pos, c);
            if (dist < net.RoadHalf) return maxSpeed; // already entering: commit and clear it
            float stopDist = net.RoadHalf + 0.03f;
            if (dist <= stopDist) return 0f;
            if (dist < stopDist + 0.12f) return maxSpeed * ((dist - stopDist) / 0.12f);
            return maxSpeed;
        }

        float NodeYield(Vector3 pos)
        {
            if (net.IsSignalized(toNode)) return maxSpeed; // a light controls this node
            float myDist = Vector3.Distance(pos, net.Nodes[toNode]);
            if (myDist < net.RoadHalf + 0.015f) return maxSpeed; // already entering: commit and clear it
            if (myDist > 0.12f) return maxSpeed; // only care when close to the junction

            var agents = Traffic.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var v = agents[i];
                if (ReferenceEquals(v, this)) continue;
                if (v.TargetNode != toNode) continue;
                if (v.DistToTarget > 0.12f) continue; // they are not at the junction yet
                // closer car has priority; ties broken by id so the two never both yield
                bool theyFirst = v.DistToTarget < myDist - 0.005f ||
                                 (Mathf.Abs(v.DistToTarget - myDist) <= 0.005f && v.VehicleId < id);
                if (theyFirst) return 0f;
            }
            return maxSpeed;
        }

        public void ForceBrake(float seconds) => forcedBrakeTimer = Mathf.Max(forcedBrakeTimer, seconds);
        public void Hazard(float seconds) => hazardTimer = Mathf.Max(hazardTimer, seconds);
        public void HonkHorn() { if (horn != null && horn.clip != null) horn.Play(); }

        void SetBody(Color c) { if (bodyRend != null) Prims.SetColor(bodyRend.material, c); }
    }
}
