using UnityEngine;

namespace FOVTest
{
    public enum EyeMask { Both = 0, Left = 1, Right = 2 }

    /// <summary>One disc or ring, sized in degrees, placed on the stage sphere.</summary>
    public class Spot
    {
        static readonly int ColorId = Shader.PropertyToID("_Color"), RadiusId = Shader.PropertyToID("_RadiusDeg"),
            InnerId = Shader.PropertyToID("_InnerDeg"), EdgeId = Shader.PropertyToID("_EdgeDeg"),
            TanId = Shader.PropertyToID("_TanHalf"), EyeId = Shader.PropertyToID("_EyeMask");
        static Mesh quad;

        readonly GameObject go;
        readonly MeshRenderer renderer;
        readonly Material mat;

        public Spot(string name, Transform parent, Shader shader, int queueOffset)
        {
            if (quad == null) quad = MakeQuad();
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            renderer = go.AddComponent<MeshRenderer>();
            mat = new Material(shader);
            mat.renderQueue = 3000 + queueOffset;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = false;
        }

        public bool Visible { get => renderer.enabled; set => renderer.enabled = value; }
        public EyeMask Eye { set => mat.SetFloat(EyeId, (float)value); }
        public Transform Transform => go.transform;

        /// <summary>Grey level as a framebuffer value (0..1).</summary>
        public float Brightness { set => Colour = new Color(value, value, value, 1f); }
        public Color Colour { set => mat.SetColor(ColorId, value); }

        /// <summary>Size for a viewer at the stage centre.</summary>
        public void SetShape(float outerRadiusDeg, float innerRadiusDeg, float edgeDeg, float distance)
        {
            float half = outerRadiusDeg + edgeDeg;
            float tan = Mathf.Tan(half * Mathf.Deg2Rad);
            mat.SetFloat(RadiusId, outerRadiusDeg);
            mat.SetFloat(InnerId, innerRadiusDeg);
            mat.SetFloat(EdgeId, edgeDeg);
            mat.SetFloat(TanId, tan);
            go.transform.localScale = new Vector3(2f * tan * distance, 2f * tan * distance, 1f);
        }

        /// <summary>Place at a local position, facing the given local viewpoint.</summary>
        public void Place(Vector3 localPos, Vector3 localViewpoint, Vector3 up)
        {
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(localPos - localViewpoint, up);
        }

        /// <summary>A solid bar on the plane tangent to the sphere at "dir": offset, length and width in metres,
        /// roll in degrees counter-clockwise as seen by the viewer.</summary>
        public void PlaceOnPlane(Vector3 dir, Vector3 up, float distance, float x, float y, float rollDeg, float length, float width)
        {
            mat.SetFloat(RadiusId, 45f);        // far larger than the quad, so the whole quad is filled
            mat.SetFloat(InnerId, 0f);
            mat.SetFloat(TanId, 0.01f);
            var facing = Quaternion.LookRotation(dir, up);
            go.transform.localPosition = dir * distance + facing * new Vector3(x, y, 0f);
            go.transform.localRotation = facing * Quaternion.Euler(0f, 0f, rollDeg);
            go.transform.localScale = new Vector3(length, width, 1f);
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "FOVTestQuad" };
            m.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.bounds = new Bounds(Vector3.zero, Vector3.one);
            return m;
        }
    }
}
