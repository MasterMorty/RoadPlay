using System.Collections.Generic;
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
        List<AICar> ais;
        FeedbackManager fx;

        public int violations;
        public int cleanCrossings;

        bool playerInIntersection;
        bool crossingJudged;

        public void Init(RoadNetwork network, TrafficLightController traffic,
            PlayerCar p, List<AICar> cars, FeedbackManager feedback)
        {
            net = network;
            signals = traffic;
            player = p;
            ais = cars;
            fx = feedback;
        }

        void Update()
        {
            if (net == null || player == null || !player.gameObject.activeInHierarchy) return;

            float L = net.laneWidth;
            Vector3 pp = player.LocalPos;
            int sNode = NearestSignalNode(pp);
            Vector3 c = net.Nodes[sNode];
            float half = net.RoadHalf + 0.4f * L;
            bool inInt = Mathf.Abs(pp.x - c.x) < half && Mathf.Abs(pp.z - c.z) < half;

            if (!inInt && Vector3.Distance(pp, c) > half + 1.2f * L)
                crossingJudged = false;

            if (inInt && !playerInIntersection && !crossingJudged)
            {
                crossingJudged = true;
                Signal sig = signals.SignalForHeading(sNode, player.Forward);
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

            if (inInt && ais != null)
            {
                foreach (var ai in ais)
                {
                    if (ai == null) continue;
                    Vector3 ap = ai.LocalPos;
                    bool aiInInt = Mathf.Abs(ap.x - c.x) < half + 0.6f * L &&
                                   Mathf.Abs(ap.z - c.z) < half + 0.6f * L;
                    if (aiInInt && Vector3.Distance(pp, ap) < 1.8f * L)
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

        int NearestSignalNode(Vector3 pp)
        {
            int best = net.CenterNode;
            float bestDist = float.MaxValue;
            foreach (var s in net.SignalNodes)
            {
                float d = Vector3.Distance(pp, net.Nodes[s]);
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        void ReactCrossTraffic(Vector3 center)
        {
            if (ais == null) return;
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
