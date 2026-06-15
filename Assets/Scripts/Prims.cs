using UnityEngine;

namespace RoadPlay
{
    /// <summary>
    /// Tiny helpers for building primitive visuals and URP materials at runtime.
    /// The whole prototype is generated from code so no manual scene wiring is needed.
    /// </summary>
    public static class Prims
    {
        static Material baseMaterial;

        // The base material ships as a real asset in Resources/ (URP Unlit). Because a
        // built asset is referenced, its shader AND its exact variant are kept in the
        // player build, so cloning it at runtime can never fall back to the magenta
        // error shader. Unlit also ignores lighting, so colours always show.
        static Material Base
        {
            get
            {
                if (baseMaterial != null) return baseMaterial;

                baseMaterial = Resources.Load<Material>("RoadPlayMat");

                if (baseMaterial == null)
                {
                    // editor-only fallback (Resources asset always exists in a build)
                    var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    var mr = temp.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterial != null)
                        baseMaterial = mr.sharedMaterial;
                    Object.Destroy(temp);
                }
                return baseMaterial;
            }
        }

        // 'emissive' just brightens the base colour; we avoid the _EMISSION keyword
        // because that variant is also stripped from the build and would go magenta.
        public static Material MakeMaterial(Color color, bool emissive = false, float emissionIntensity = 3f)
        {
            var m = new Material(Base);
            SetColor(m, color);
            return m;
        }

        public static void SetColor(Material m, Color color)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            m.color = color;
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // no physics, manual checks only
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
            => Primitive(PrimitiveType.Cube, parent, name, localPos, size, mat);

        public static GameObject Cylinder(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
            => Primitive(PrimitiveType.Cylinder, parent, name, localPos, size, mat);

        public static GameObject Sphere(Transform parent, string name, Vector3 localPos, float diameter, Material mat)
            => Primitive(PrimitiveType.Sphere, parent, name, localPos, Vector3.one * diameter, mat);
    }
}
