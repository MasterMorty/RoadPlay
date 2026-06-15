using UnityEngine;

namespace RoadPlay
{
    /// <summary>
    /// The traffic-rule engine (Florian's workstream). Watches the player's car
    /// against the signals and the virtual traffic, classifies each intersection
    /// entry, and triggers consequence feedback:
    ///   - running a red light,
    ///   - failing to yield / near-miss with crossing traffic,
    ///   - clean crossing on green.
    /// </summary>
    public class RuleEngine : MonoBehaviour
    {
        RoadNetwork net;
        TrafficLightController signals;
        PlayerCar player;
        AICar[] ais;
        FeedbackManager fx;

        public int violations;
        public int cleanCrossings;

        bool playerInIntersection;
        bool crossingJudged;

        public void Init(RoadNetwork network, TrafficLightController traffic,
            PlayerCar p, AICar[] cars, FeedbackManager feedback)
        {
            net = network;
            signals = traffic;
            player = p;
            ais = cars;
            fx = feedback;
        }

        void Update()
        {
            if (net == null || player == null) return;

            Vector3 pp = player.LocalPos;
            Vector3 c = NearestSignal(pp);
            float half = net.RoadHalf + 0.02f;
            bool inInt = Mathf.Abs(pp.x - c.x) < half && Mathf.Abs(pp.z - c.z) < half;

            // reset judgement once the player has left the intersection again
            if (!inInt && Vector3.Distance(pp, c) > half + 0.06f)
                crossingJudged = false;

            if (inInt && !playerInIntersection && !crossingJudged)
            {
                crossingJudged = true;
                // which axis is the player crossing on?
                Vector3 f = player.Forward;
                bool ns = Mathf.Abs(f.z) >= Mathf.Abs(f.x);
                Signal sig = ns ? signals.NS : signals.EW;
                if (sig == Signal.Green)
                {
                    cleanCrossings++;
                    if (fx != null) fx.Good("NICE DRIVING");
                }
                else
                {
                    violations++;
                    if (fx != null) fx.Violation("RED LIGHT!");
                    ReactCrossTraffic(c);
                }
            }

            // near-miss: player too close to any car inside the intersection
            if (inInt && ais != null)
            {
                foreach (var ai in ais)
                {
                    if (ai == null) continue;
                    Vector3 ap = ai.LocalPos;
                    bool aiInInt = Mathf.Abs(ap.x - c.x) < half + 0.03f &&
                                   Mathf.Abs(ap.z - c.z) < half + 0.03f;
                    if (aiInInt && Vector3.Distance(pp, ap) < 0.09f)
                    {
                        ai.ForceBrake(1.6f);
                        ai.Hazard(1.6f);
                        ai.HonkHorn();
                        if (fx != null) fx.NearMiss();
                    }
                }
            }

            playerInIntersection = inInt;
        }

        Vector3 NearestSignal(Vector3 pp)
        {
            Vector3 best = net.Nodes[net.CenterNode];
            float bestDist = float.MaxValue;
            foreach (var s in net.SignalNodes)
            {
                Vector3 p = net.Nodes[s];
                float d = Vector3.Distance(pp, p);
                if (d < bestDist) { bestDist = d; best = p; }
            }
            return best;
        }

        void ReactCrossTraffic(Vector3 center)
        {
            if (ais == null) return;
            // the car closest to the intersection centre reacts
            AICar best = null;
            float bestDist = float.MaxValue;
            foreach (var ai in ais)
            {
                if (ai == null) continue;
                float d = Vector3.Distance(ai.LocalPos, center);
                if (d < bestDist) { bestDist = d; best = ai; }
            }
            if (best != null)
            {
                best.ForceBrake(1.8f);
                best.Hazard(1.8f);
                best.HonkHorn();
            }
        }
    }
}
