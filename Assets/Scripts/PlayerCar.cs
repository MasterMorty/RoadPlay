using UnityEngine;
using UnityEngine.InputSystem;

namespace RoadPlay
{
    /// <summary>
    /// The child's car. On iOS this is a touch-driven stand-in for the physical
    /// RC/toy car that the Meta Quest version will track with a marker.
    /// Controls: hold anywhere to drive forward, slide the finger left/right to
    /// steer, release to brake. Body forward = +Z.
    /// </summary>
    public class PlayerCar : MonoBehaviour, IVehicle
    {
        RoadNetwork net;

        public float maxSpeed = 0.13f;
        public float accel = 0.3f;
        public float turnRate = 90f; // degrees / second
        public float speed;

        bool armed; // ignore the touch that placed the board until it is released once

        public Vector3 LocalPos => transform.localPosition;
        public Vector3 Forward => transform.localRotation * Vector3.forward;

        public Vector3 VLocalPos => transform.localPosition;
        public Vector3 VLocalForward => transform.localRotation * Vector3.forward;
        public int TargetNode => -1;
        public float DistToTarget => float.MaxValue;
        public int VehicleId => -1;

        public void Init(RoadNetwork network)
        {
            net = network;
            BuildBody();
            // start on the south edge road, in the right lane, facing the centre (north)
            Vector3 south = net.Nodes[net.SouthEntryNode];
            transform.localPosition = south + new Vector3(net.LaneOffset, 0, 0);
            transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up); // face +Z north
            Traffic.Register(this);
        }

        void BuildBody()
        {
            var bodyColor = new Color(0.9f, 0.16f, 0.16f);
            var bodyMat = Prims.MakeMaterial(bodyColor);
            Prims.Box(transform, "body", new Vector3(0, 0.012f, 0), new Vector3(0.03f, 0.018f, 0.06f), bodyMat);

            var roofMat = Prims.MakeMaterial(bodyColor * 0.7f);
            Prims.Box(transform, "roof", new Vector3(0, 0.026f, -0.006f), new Vector3(0.026f, 0.014f, 0.03f), roofMat);

            var glass = Prims.MakeMaterial(new Color(0.4f, 0.6f, 0.85f));
            Prims.Box(transform, "windshield", new Vector3(0, 0.024f, 0.013f), new Vector3(0.024f, 0.012f, 0.006f), glass);

            // bright marker so the player can always spot their own car
            var markMat = Prims.MakeMaterial(new Color(1f, 0.95f, 0.2f), true, 2f);
            Prims.Sphere(transform, "marker", new Vector3(0, 0.04f, 0), 0.012f, markMat);

            var wheelMat = Prims.MakeMaterial(new Color(0.05f, 0.05f, 0.05f));
            const float wx = 0.016f, wz = 0.018f;
            foreach (var sx in new[] { -wx, wx })
                foreach (var sz in new[] { -wz, wz })
                {
                    var w = Prims.Cylinder(transform, "wheel", new Vector3(sx, 0.005f, sz),
                        new Vector3(0.012f, 0.006f, 0.012f), wheelMat);
                    w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
        }

        void Update()
        {
            if (net == null) return;

            var pointer = Pointer.current;
            bool pressed = pointer != null && pointer.press.isPressed;

            if (!armed)
            {
                if (!pressed) armed = true; // wait for the placement tap to lift
                speed = Mathf.MoveTowards(speed, 0f, accel * 2f * Time.deltaTime);
                Integrate(0f);
                return;
            }

            float steer = 0f;
            if (pressed)
            {
                float nx = pointer.position.ReadValue().x / Mathf.Max(1f, Screen.width); // 0..1
                steer = Mathf.Clamp((nx - 0.5f) * 2f, -1f, 1f);
            }

            float target = pressed ? maxSpeed : 0f;
            speed = Mathf.MoveTowards(speed, target, accel * (pressed ? 1f : 2f) * Time.deltaTime);
            Integrate(steer);
        }

        void Integrate(float steer)
        {
            if (Mathf.Abs(steer) > 0.001f && speed > 0.001f)
                transform.localRotation *= Quaternion.Euler(0, steer * turnRate * Time.deltaTime, 0);

            var p = transform.localPosition + Forward * (speed * Time.deltaTime);
            float lim = net.BoardHalf;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -lim, lim);
            p.y = 0f;
            transform.localPosition = p;
        }
    }
}
