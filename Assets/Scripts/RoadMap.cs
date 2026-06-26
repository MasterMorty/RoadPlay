using System.Collections.Generic;
using UnityEngine;

namespace RoadPlay
{
    /// <summary>
    /// A hand-authored road graph drawn on top of a printed street-map image
    /// (see the RoadPlay Map Editor window). Node positions are stored in
    /// normalised image space: (0,0) = top-left, (1,1) = bottom-right.
    /// At runtime the graph is anchored to the tracked map image and scaled to
    /// its physical size, so the virtual roads line up with the printed ones.
    /// </summary>
    [CreateAssetMenu(menuName = "RoadPlay/Road Map", fileName = "RoadMap")]
    public class RoadMap : ScriptableObject
    {
        [Tooltip("The printed map image (also used as the AR reference image).")]
        public Texture2D mapImage;

        [Tooltip("Fallback physical width in metres if the tracked image size is unknown.")]
        public float physicalWidthMeters = 0.3f;

        [Tooltip("Street width as a FRACTION of the map width (0 = auto). Drag the slider " +
                 "in the Map Editor until the lines cover the printed streets - the same " +
                 "fraction is used on the physical map, so editor == reality.")]
        public float streetWidthFrac = 0f;

        [Tooltip("Name of this image inside the AR Reference Image Library.")]
        public string referenceImageName = "map";

        public List<Vector2> nodes = new List<Vector2>();
        public List<int> edgeA = new List<int>();
        public List<int> edgeB = new List<int>();
        public List<int> signalNodes = new List<int>();

        public Vector3 LocalPosition(int index, float sizeX, float sizeZ)
        {
            Vector2 uv = nodes[index];
            return new Vector3((uv.x - 0.5f) * sizeX, 0f, (0.5f - uv.y) * sizeZ);
        }
    }
}
