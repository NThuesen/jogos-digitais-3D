using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>Visual ball: roll rotation, blob shadow and the sink-into-hole animation.</summary>
    public sealed class BallView : MonoBehaviour
    {
        Transform sphere, shadow, contact;
        Quaternion rot = Quaternion.identity;
        Vector2 lastPos;
        float sinkT = -1f;
        float popT = -1f;
        Vector2 sinkFrom, holePos;

        public static BallView Create(Transform parent)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BallView>();
            v.Build();
            return v;
        }

        void Build()
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(s.GetComponent<Collider>());
            s.name = "Sphere";
            s.transform.SetParent(transform, false);
            s.transform.localScale = Vector3.one * Tuning.BallRadius * 2f;
            var r = s.GetComponent<MeshRenderer>();
            r.sharedMaterial = ArtFactory.Cached("ball", () => ArtFactory.Lit(Color.white, ArtFactory.Ball(), 0.55f));
            r.shadowCastingMode = ShadowCastingMode.On;
            sphere = s.transform;

            var mb = new MeshBuilder();
            float h = 0.4f;
            mb.Quad(0, new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h), Vector3.up,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            var sh = new GameObject("Blob Shadow");
            sh.transform.SetParent(transform, false);
            sh.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("BlobShadow");
            var sr = sh.AddComponent<MeshRenderer>();
            sr.sharedMaterial = ArtFactory.Cached("blob", () => ArtFactory.Transparent(new Color(0, 0, 0, 0.42f), ArtFactory.SoftDisc()));
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            shadow = sh.transform;

            // tight dark blob right under the ball grounds it; the wider blob above is the soft cast shadow
            var ct = new GameObject("Contact Shadow");
            ct.transform.SetParent(transform, false);
            ct.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("ContactShadow");
            var cr = ct.AddComponent<MeshRenderer>();
            cr.sharedMaterial = ArtFactory.Cached("contact", () => ArtFactory.Transparent(new Color(0, 0, 0, 0.55f), ArtFactory.SoftDisc()));
            cr.shadowCastingMode = ShadowCastingMode.Off;
            cr.receiveShadows = false;
            contact = ct.transform;
        }

        public void Teleport(Vector2 pos)
        {
            sinkT = -1f;
            popT = -1f;
            lastPos = pos;
            sphere.localScale = Vector3.one * Tuning.BallRadius * 2f;
            Apply(pos, Tuning.BallRadius, 1f);
            shadow.gameObject.SetActive(true);
            contact.gameObject.SetActive(true);
        }

        /// <summary>Back on the tee after a water fall: short pop-in so the jump reads as a reset, not a glitch.</summary>
        public void Reappear(Vector2 pos)
        {
            Teleport(pos);
            popT = 0f;
            sphere.localScale = Vector3.one * Tuning.BallRadius * 2f * 0.35f;
        }

        public void Show(Vector2 pos)
        {
            Vector2 d = pos - lastPos;
            float dist = d.magnitude;
            if (dist > 1e-5f && dist < 1f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, new Vector3(d.x, 0, d.y)).normalized;
                rot = Quaternion.AngleAxis(dist / Tuning.BallRadius * Mathf.Rad2Deg, axis) * rot;
                sphere.rotation = rot;
            }
            lastPos = pos;
            Apply(pos, Tuning.BallRadius, 1f);
        }

        void Apply(Vector2 pos, float y, float scale)
        {
            transform.position = new Vector3(pos.x, 0, pos.y);
            sphere.localPosition = new Vector3(0, y, 0);
            shadow.localPosition = new Vector3(0.03f, 0.007f, -0.03f);
            shadow.localScale = Vector3.one * (0.9f + y * 0.2f) * scale;
            contact.localPosition = new Vector3(0.01f, 0.009f, -0.01f);
            contact.localScale = Vector3.one * 0.5f * scale;
        }

        public void BeginSink(Vector2 hole)
        {
            sinkT = 0f;
            sinkFrom = lastPos;
            holePos = hole;
        }

        public bool Sinking => sinkT >= 0f;
        public bool SinkDone => sinkT >= 1f;

        void Update()
        {
            if (popT >= 0f && sinkT < 0f)
            {
                popT = Mathf.Min(1f, popT + Time.deltaTime / 0.25f);
                float s = Mathf.Lerp(0.35f, 1f, 1f - (1f - popT) * (1f - popT));
                sphere.localScale = Vector3.one * Tuning.BallRadius * 2f * s;
                if (popT >= 1f) popT = -1f;
            }
            if (sinkT < 0f || sinkT >= 1f) return;
            sinkT = Mathf.Min(1f, sinkT + Time.deltaTime / 0.42f);
            float e = sinkT * sinkT;
            Vector2 p = Vector2.Lerp(sinkFrom, holePos, Mathf.SmoothStep(0, 1, sinkT * 1.4f));
            float y = Mathf.Lerp(Tuning.BallRadius, -0.2f, e);
            transform.position = new Vector3(p.x, 0, p.y);
            sphere.localPosition = new Vector3(0, y, 0);
            sphere.localScale = Vector3.one * Tuning.BallRadius * 2f * Mathf.Lerp(1f, 0.55f, e);
            shadow.gameObject.SetActive(sinkT < 0.6f);
            contact.gameObject.SetActive(sinkT < 0.3f);
        }
    }
}
