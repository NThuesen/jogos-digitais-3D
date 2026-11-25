using System.Collections.Generic;
using UnityEngine;

namespace SlipperyZlope.RingToss.Utils
{
    /// <summary>
    /// Procedural meshes for the shapes Unity has no primitive for.
    ///
    /// The whole game is built from untextured generated geometry, which keeps the
    /// project free of imported art, keeps the draw call count low through shared
    /// materials, and means the art direction lives in code that can be reviewed.
    /// </summary>
    public static class MeshFactory
    {
        /// <summary>
        /// A torus lying in the XZ plane, its axis along local Y, matching the
        /// orientation the ring's compound collider is built in.
        /// </summary>
        public static Mesh Torus(float outerRadius, float tubeRadius, int radialSegments, int tubeSegments)
        {
            radialSegments = Mathf.Max(3, radialSegments);
            tubeSegments = Mathf.Max(3, tubeSegments);

            int vertexCount = (radialSegments + 1) * (tubeSegments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];

            for (int i = 0; i <= radialSegments; i++)
            {
                float u = (float)i / radialSegments;
                float theta = u * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta));
                Vector3 centre = outward * outerRadius;

                for (int j = 0; j <= tubeSegments; j++)
                {
                    float v = (float)j / tubeSegments;
                    float phi = v * Mathf.PI * 2f;

                    Vector3 normal = outward * Mathf.Cos(phi) + Vector3.up * Mathf.Sin(phi);
                    int index = i * (tubeSegments + 1) + j;

                    vertices[index] = centre + normal * tubeRadius;
                    normals[index] = normal;
                    uv[index] = new Vector2(u, v);
                }
            }

            int[] triangles = new int[radialSegments * tubeSegments * 6];
            int t = 0;

            for (int i = 0; i < radialSegments; i++)
            {
                for (int j = 0; j < tubeSegments; j++)
                {
                    int current = i * (tubeSegments + 1) + j;
                    int next = (i + 1) * (tubeSegments + 1) + j;

                    triangles[t++] = current;
                    triangles[t++] = next + 1;
                    triangles[t++] = next;

                    triangles[t++] = current;
                    triangles[t++] = current + 1;
                    triangles[t++] = next + 1;
                }
            }

            return Build("Torus", vertices, normals, uv, triangles);
        }

        /// <summary>A filled disc in the XZ plane, facing up.</summary>
        public static Mesh Disc(float radius, int segments)
        {
            segments = Mathf.Max(3, segments);

            Vector3[] vertices = new Vector3[segments + 1];
            Vector3[] normals = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[segments + 1];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);

                vertices[i + 1] = new Vector3(x * radius, 0f, z * radius);
                normals[i + 1] = Vector3.up;
                uv[i + 1] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
            }

            int[] triangles = new int[segments * 3];

            for (int i = 0; i < segments; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            return Build("Disc", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// A flat trapezoid in the XZ plane, facing +Y. The board.
        ///
        /// A rectangle is the obvious shape for a board and the wrong one for a
        /// portrait camera. The frustum is a wedge — 1.09 m across at the board's
        /// front and 1.83 m at its back — so a five-metre slab runs off both edges of
        /// the screen at every depth, and the player never sees where the play area
        /// ends. Given straight sides that follow the frustum, the whole board is on
        /// screen, its edges are visible the whole way down, and the shape it makes is
        /// the shape the camera was going to show anyway.
        ///
        /// Straight sides are exact rather than an approximation: the visible
        /// half-width is linear in depth, so four vertices describe the wedge with no
        /// error at all.
        /// </summary>
        public static Mesh Wedge(float frontZ, float frontHalfWidth, float backZ, float backHalfWidth)
        {
            Vector3[] vertices =
            {
                new Vector3(-frontHalfWidth, 0f, frontZ),
                new Vector3(-backHalfWidth, 0f, backZ),
                new Vector3(backHalfWidth, 0f, backZ),
                new Vector3(frontHalfWidth, 0f, frontZ),
            };

            Vector3[] normals = { Vector3.up, Vector3.up, Vector3.up, Vector3.up };

            // UVs run across the wedge rather than across its bounding box, so a
            // pattern laid on it does not skew towards the narrow end.
            Vector2[] uv =
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f),
            };

            // Wound so the face points +Y. With the vertices in the XZ plane the
            // winding decides visibility and the normal array does not — Unity culls
            // on the winding, which is why a flat shape built the other way round is
            // simply not drawn and looks like a material bug.
            int[] triangles = { 0, 1, 2, 0, 2, 3 };

            return Build("Wedge", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// The outline of a <see cref="Wedge"/> as a closed band, facing +Y.
        ///
        /// This exists because "light the edge of the board" and "light a plate that is
        /// bigger than the board" are different things, and the second one was shipped.
        /// The board's trim is a wedge 0.17 m larger on every side, so the only part of
        /// it the camera ever sees is the strip *behind* the deck — which is precisely
        /// where the owner did not want a glow. A band inset into the deck traces the
        /// area a ring can actually land in, which is what was asked for both times.
        ///
        /// The inset is applied to the half-widths and the depths directly rather than
        /// perpendicular to each edge. For a band this thin against sides this shallow
        /// the difference is well under a millimetre, and the front and back edges —
        /// the ones the player reads as the limit — are exact either way.
        /// </summary>
        public static Mesh WedgeBorder(
            float frontZ, float frontHalfWidth, float backZ, float backHalfWidth, float width)
        {
            width = Mathf.Max(0.005f, width);

            float innerFrontZ = frontZ + width;
            float innerBackZ = backZ - width;
            float innerFrontHalf = Mathf.Max(0.01f, frontHalfWidth - width);
            float innerBackHalf = Mathf.Max(0.01f, backHalfWidth - width);

            Vector3[] vertices =
            {
                new Vector3(-frontHalfWidth, 0f, frontZ),
                new Vector3(-backHalfWidth, 0f, backZ),
                new Vector3(backHalfWidth, 0f, backZ),
                new Vector3(frontHalfWidth, 0f, frontZ),

                new Vector3(-innerFrontHalf, 0f, innerFrontZ),
                new Vector3(-innerBackHalf, 0f, innerBackZ),
                new Vector3(innerBackHalf, 0f, innerBackZ),
                new Vector3(innerFrontHalf, 0f, innerFrontZ),
            };

            Vector3[] normals = new Vector3[8];

            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            // u crosses the band, outer edge to inner, so a shader can fade it inwards.
            Vector2[] uv =
            {
                new Vector2(0f, 0f), new Vector2(0f, 0.25f), new Vector2(0f, 0.5f), new Vector2(0f, 0.75f),
                new Vector2(1f, 0f), new Vector2(1f, 0.25f), new Vector2(1f, 0.5f), new Vector2(1f, 0.75f),
            };

            int[] triangles = new int[24];

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                int t = i * 6;

                // Same winding as Wedge: the face has to point at the camera, and with
                // flat geometry in the XZ plane the winding is the only thing that says so.
                triangles[t + 0] = i;
                triangles[t + 1] = j;
                triangles[t + 2] = 4 + j;

                triangles[t + 3] = i;
                triangles[t + 4] = 4 + j;
                triangles[t + 5] = 4 + i;
            }

            return Build("Wedge Border", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// A flat annulus in the XZ plane, used for the expanding shockwave. UV.x runs
        /// from 0 at the inner edge to 1 at the outer one so a shader can fade across it.
        /// </summary>
        public static Mesh Annulus(float innerRadius, float outerRadius, int segments)
        {
            segments = Mathf.Max(3, segments);

            int vertexCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = u * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices[i * 2] = outward * innerRadius;
                vertices[i * 2 + 1] = outward * outerRadius;
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
                uv[i * 2] = new Vector2(0f, u);
                uv[i * 2 + 1] = new Vector2(1f, u);
            }

            int[] triangles = new int[segments * 6];
            int t = 0;

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = (i + 1) * 2;
                int d = (i + 1) * 2 + 1;

                triangles[t++] = a;
                triangles[t++] = d;
                triangles[t++] = b;

                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = d;
            }

            return Build("Annulus", vertices, normals, uv, triangles);
        }

        /// <summary>A quad in the XY plane, centred, facing -Z.</summary>
        public static Mesh Quad(float width, float height)
        {
            Vector3[] vertices =
            {
                new Vector3(-width * 0.5f, -height * 0.5f, 0f),
                new Vector3(width * 0.5f, -height * 0.5f, 0f),
                new Vector3(-width * 0.5f, height * 0.5f, 0f),
                new Vector3(width * 0.5f, height * 0.5f, 0f)
            };

            Vector3[] normals = { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            Vector2[] uv = { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            int[] triangles = { 0, 2, 1, 2, 3, 1 };

            return Build("Quad", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// A triangular bunting flag hanging from a point, pointing down. Used to
        /// string decorative flags across the top of the booth.
        /// </summary>
        public static Mesh Pennant(float width, float height)
        {
            Vector3[] vertices =
            {
                new Vector3(-width * 0.5f, 0f, 0f),
                new Vector3(width * 0.5f, 0f, 0f),
                new Vector3(0f, -height, 0f)
            };

            Vector3[] normals = { Vector3.back, Vector3.back, Vector3.back };
            Vector2[] uv = { Vector2.zero, Vector2.right, new Vector2(0.5f, 1f) };
            int[] triangles = { 0, 1, 2 };

            return Build("Pennant", vertices, normals, uv, triangles);
        }

        /// <summary>
        /// A convex polygon in the XY plane, from points given counter-clockwise around
        /// the origin. Used for the flat silhouette scenery on the backdrop.
        ///
        /// Faces -Z, which is the side the camera looks from, so scenery built with it
        /// needs no extra rotation.
        /// </summary>
        public static Mesh Fan(IReadOnlyList<Vector2> outline)
        {
            int count = outline.Count;
            Vector3[] vertices = new Vector3[count];
            Vector3[] normals = new Vector3[count];
            Vector2[] uv = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(outline[i].x, outline[i].y, 0f);
                normals[i] = Vector3.back;
                uv[i] = outline[i];
            }

            int[] triangles = new int[(count - 2) * 3];
            int t = 0;

            for (int i = 1; i < count - 1; i++)
            {
                triangles[t++] = 0;
                triangles[t++] = i + 1;
                triangles[t++] = i;
            }

            return Build("Fan", vertices, normals, uv, triangles);
        }

        private static Mesh Build(string name, Vector3[] vertices, Vector3[] normals, Vector2[] uv, int[] triangles)
        {
            Mesh mesh = new Mesh { name = name };

            if (vertices.Length > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
