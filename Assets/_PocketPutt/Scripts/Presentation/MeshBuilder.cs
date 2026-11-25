using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>Accumulates triangles into submeshes. One instance builds one Mesh.</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] tris;

        public MeshBuilder(int submeshes = 1)
        {
            tris = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
        }

        public int VertexCount => verts.Count;

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            verts.Add(p); normals.Add(n); uvs.Add(uv);
            return verts.Count - 1;
        }

        /// <summary>Adds a triangle, flipping winding if needed so it faces along the vertex normals.</summary>
        public void AddTri(int sub, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (face.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(face, normals[a] + normals[b] + normals[c]) < 0f) { int t = b; b = c; c = t; }
            tris[sub].Add(a); tris[sub].Add(b); tris[sub].Add(c);
        }

        public void AddQuad(int sub, int a, int b, int c, int d)
        {
            AddTri(sub, a, b, c);
            AddTri(sub, a, c, d);
        }

        /// <summary>Flat quad given 4 corners in clockwise order as seen from the normal side.</summary>
        public void Quad(int sub, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int a = AddVertex(p0, n, uv0), b = AddVertex(p1, n, uv1), c = AddVertex(p2, n, uv2), d = AddVertex(p3, n, uv3);
            AddQuad(sub, a, b, c, d);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) m.SetTriangles(tris[i], i);
            m.RecalculateBounds();
            return ArtFactory.Track(m);
        }
    }
}
