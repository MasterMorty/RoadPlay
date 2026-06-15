using System.Collections.Generic;
using UnityEngine;

namespace RoadPlay
{
    /// <summary>
    /// Anything the awareness system needs to reason about (AI cars + the player
    /// car). Positions/headings are in the road root's local space so they can be
    /// compared directly.
    /// </summary>
    public interface IVehicle
    {
        Vector3 VLocalPos { get; }
        Vector3 VLocalForward { get; }
        int TargetNode { get; }    // graph node a car is driving toward (-1 = none, e.g. player)
        float DistToTarget { get; } // distance to that node (infinity = none)
        int VehicleId { get; }      // stable id, used to break yield ties (-1 = player)
    }

    /// <summary>Global registry of vehicles so each car can "see" the others.</summary>
    public static class Traffic
    {
        public static readonly List<IVehicle> Agents = new List<IVehicle>();

        public static void Clear() => Agents.Clear();

        public static void Register(IVehicle v)
        {
            if (!Agents.Contains(v)) Agents.Add(v);
        }
    }
}
