using UnityEngine;
using UnityEngine.Rendering;

namespace SlipperyZlope.RingToss.Utils
{
    /// <summary>
    /// Small helpers for assembling objects out of meshes at runtime.
    ///
    /// The scene is generated rather than authored, so this is used everywhere. It
    /// keeps the renderer defaults in one place: shadows and light probes are opt-in,
    /// because most of what this game draws is scenery that should not pay for them.
    /// </summary>
    public static class GeometryFactory
    {
        public static GameObject CreateMeshObject(
            string name,
            Transform parent,
            Mesh mesh,
            Material material,
            bool castShadows = false,
            bool receiveShadows = false)
        {
            GameObject instance = new GameObject(name);
            instance.transform.SetParent(parent, false);

            MeshFilter filter = instance.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            return instance;
        }

        /// <summary>
        /// A primitive with its collider stripped. Unity's primitives always come with
        /// one, and scenery that quietly collides with the ring is a very confusing
        /// bug to chase.
        /// </summary>
        public static GameObject CreatePrimitive(
            string name,
            Transform parent,
            PrimitiveType type,
            Material material,
            bool castShadows = false,
            bool receiveShadows = false)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);

            Collider collider = instance.GetComponent<Collider>();

            if (collider != null)
            {
                Object.Destroy(collider);
            }

            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return instance;
        }

        public static GameObject CreateEmpty(string name, Transform parent)
        {
            GameObject instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            return instance;
        }

        /// <summary>
        /// A runtime material clone. Used only for the handful of scenery colours that
        /// differ per theme; per-object colour changes during gameplay go through a
        /// MaterialPropertyBlock instead, which does not allocate a material.
        /// </summary>
        public static Material CloneMaterial(Material source, Color color, string name)
        {
            Material clone = new Material(source) { name = name };

            if (clone.HasProperty(ShaderIds.BaseColor))
            {
                clone.SetColor(ShaderIds.BaseColor, color);
            }

            return clone;
        }

        public static Material CloneEmissiveMaterial(Material source, Color color, float intensity, string name)
        {
            Material clone = CloneMaterial(source, color, name);

            if (clone.HasProperty(ShaderIds.EmissionColor))
            {
                clone.EnableKeyword("_EMISSION");
                clone.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                clone.SetColor(ShaderIds.EmissionColor, color * intensity);
            }

            return clone;
        }
    }

    /// <summary>Cached shader property ids. Shader.PropertyToID is not free in a loop.</summary>
    public static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int TopColor = Shader.PropertyToID("_TopColor");
        public static readonly int MiddleColor = Shader.PropertyToID("_MiddleColor");
        public static readonly int HorizonColor = Shader.PropertyToID("_HorizonColor");
        public static readonly int HorizonHeight = Shader.PropertyToID("_HorizonHeight");
        public static readonly int SunColor = Shader.PropertyToID("_SunColor");
        public static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
        public static readonly int SunSize = Shader.PropertyToID("_SunSize");
        public static readonly int Softness = Shader.PropertyToID("_Softness");
        public static readonly int Fill = Shader.PropertyToID("_Fill");
    }
}
