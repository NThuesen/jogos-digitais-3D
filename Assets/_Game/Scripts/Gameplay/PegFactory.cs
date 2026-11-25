using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Juice;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Assembles a peg out of primitives at load time.
    ///
    /// Pegs are built rather than authored as a prefab for the same reason the rest of
    /// the scene is: the shape is a handful of numbers, and numbers in code can be read
    /// and changed, where a prefab's are buried in serialized YAML.
    /// </summary>
    public static class PegFactory
    {
        /// <summary>Everything a peg needs to be drawn. Kept together so the call site stays readable.</summary>
        public struct Assets
        {
            public Material Body;
            public Material Stripe;
            public Material Cap;
            public Material Glow;
            public Mesh GlowMesh;
            public PhysicsMaterial Physics;
        }

        public static PegTarget Create(
            string name,
            Transform parent,
            PhysicsConfig config,
            JuiceConfig juiceConfig,
            in Assets assets)
        {
            float radius = config == null ? 0.09f : config.PegRadius;
            float height = config == null ? 0.92f : config.PegHeight;

            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = radius;
            // CapsuleCollider.height is the total height, caps included.
            collider.height = height;
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            collider.material = assets.Physics;

            PegTarget target = root.AddComponent<PegTarget>();
            root.AddComponent<PegPart>().SetOwner(target);

            Transform visual = GeometryFactory.CreateEmpty("Visual", root.transform).transform;

            // The post. A unit cylinder is two units tall and one across, so the scale
            // halves into the real height and doubles into the real radius.
            GameObject post = GeometryFactory.CreatePrimitive(
                "Post", visual, PrimitiveType.Cylinder, assets.Body, castShadows: true);
            post.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            post.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            // Candy stripes: thin rings slightly proud of the post so they never
            // z-fight with it.
            const int stripeCount = 3;

            for (int i = 0; i < stripeCount; i++)
            {
                float t = (i + 0.5f) / stripeCount;
                GameObject stripe = GeometryFactory.CreatePrimitive(
                    $"Stripe_{i}", visual, PrimitiveType.Cylinder, assets.Stripe);
                stripe.transform.localPosition = new Vector3(0f, height * t, 0f);
                stripe.transform.localScale =
                    new Vector3(radius * 2.14f, height * 0.055f, radius * 2.14f);
            }

            // Collar at the base, so a ring that lands cleanly has something to rest
            // against and the peg does not appear to grow straight out of the floor.
            GameObject collar = GeometryFactory.CreatePrimitive(
                "Collar", visual, PrimitiveType.Cylinder, assets.Stripe, castShadows: true);
            collar.transform.localPosition = new Vector3(0f, 0.018f, 0f);
            collar.transform.localScale = new Vector3(radius * 3.2f, 0.018f, radius * 3.2f);

            GameObject cap = GeometryFactory.CreatePrimitive(
                "Cap", visual, PrimitiveType.Sphere, assets.Cap);
            cap.transform.localPosition = new Vector3(0f, height + radius * 0.35f, 0f);
            cap.transform.localScale = Vector3.one * (radius * 2.3f);

            GameObject glow = GeometryFactory.CreateMeshObject(
                "Glow", root.transform, assets.GlowMesh, assets.Glow);
            glow.transform.localPosition = new Vector3(0f, 0.008f, 0f);
            glow.transform.localScale = Vector3.one * (radius * 7f);

            // The plinth a raised peg stands on. Built for every peg and hidden on the
            // ones at board level, because building it on demand would mean allocating
            // during a level start.
            GameObject plinth = GeometryFactory.CreatePrimitive(
                "Plinth", root.transform, PrimitiveType.Cylinder, assets.Stripe, castShadows: true);
            plinth.SetActive(false);

            target.Bind(config, visual, cap.transform, glow.transform, plinth.transform);

            PegPresenter presenter = root.AddComponent<PegPresenter>();
            presenter.Bind(
                juiceConfig,
                visual,
                cap.GetComponent<MeshRenderer>(),
                glow.GetComponent<MeshRenderer>(),
                glow.transform);

            target.SetPresenter(presenter);
            root.SetActive(false);

            return target;
        }
    }
}
