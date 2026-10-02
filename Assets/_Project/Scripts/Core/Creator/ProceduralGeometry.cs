using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FS27.Core
{
    /// <summary>One triangle mesh in character space: flat arrays, no engine types. Colour is per mesh (a material colour).</summary>
    public sealed class MeshData
    {
        public string Name = "";
        public string MaterialId = "";
        public string ColorHex = "#FFFFFF";
        public float Smoothness = 0.2f;
        public readonly List<float> Positions = new List<float>();
        public readonly List<float> Normals = new List<float>();
        public readonly List<int> Indices = new List<int>();

        public int VertexCount => Positions.Count / 3;
        public int TriangleCount => Indices.Count / 3;

        public int AddVertex(Vec3 p, Vec3 n)
        {
            Positions.Add(p.X); Positions.Add(p.Y); Positions.Add(p.Z);
            Vec3 nn = n.Normalized;
            Normals.Add(nn.X); Normals.Add(nn.Y); Normals.Add(nn.Z);
            return VertexCount - 1;
        }

        public void AddTriangle(int a, int b, int c) { Indices.Add(a); Indices.Add(b); Indices.Add(c); }
    }

    /// <summary>The whole built character: meshes grouped by material colour. Procedural mannequin geometry, NOT a final art asset.</summary>
    public sealed class ProceduralCharacterMesh
    {
        public string CharacterId = "";
        public readonly List<MeshData> Meshes = new List<MeshData>();
        public Vec3 BoundsMin, BoundsMax;

        public int Triangles
        {
            get
            {
                int n = 0;
                foreach (MeshData m in Meshes) n += m.TriangleCount;
                return n;
            }
        }

        public int Vertices
        {
            get
            {
                int n = 0;
                foreach (MeshData m in Meshes) n += m.VertexCount;
                return n;
            }
        }
    }

    /// <summary>Builds triangles from the procedural definitions of a plan. Deterministic; no randomness, no dependencies.</summary>
    public static class ProceduralMeshBuilder
    {
        public const int Slices = 16;
        public const int Stacks = 10;
        public const int Sides = 14;

        public static ProceduralCharacterMesh Build(CharacterAssemblyPlan plan, MaterialCatalog materials)
        {
            var result = new ProceduralCharacterMesh { CharacterId = plan.CharacterId };
            var byKey = new Dictionary<string, MeshData>(StringComparer.Ordinal);
            var order = new List<MeshData>();
            foreach (AssemblyPart part in plan.Parts)
            {
                if (part.Source != PartSource.Procedural) continue;
                string color = !string.IsNullOrEmpty(part.ColorOverride) ? part.ColorOverride : (plan.MaterialColors.TryGetValue(part.MaterialId, out string c) ? c : "#FF00FF");
                string key = part.MaterialId + "|" + color;
                if (!byKey.TryGetValue(key, out MeshData mesh))
                {
                    materials.TryGet(part.MaterialId, out MaterialDefinition def);
                    mesh = new MeshData { Name = part.MaterialId.Substring(part.MaterialId.IndexOf('.') + 1) + (byKey.Count > 0 && ContainsName(order, part.MaterialId) ? "_" + byKey.Count : ""), MaterialId = part.MaterialId, ColorHex = color, Smoothness = def != null ? def.Smoothness : 0.2f };
                    byKey[key] = mesh;
                    order.Add(mesh);
                }
                switch (part.Generator)
                {
                    case "ellipsoid": Ellipsoid(mesh, part); break;
                    case "segment": Segment(mesh, part); break;
                    case "box": Box(mesh, part); break;
                }
            }
            result.Meshes.AddRange(order);

            bool any = false;
            Vec3 min = Vec3.Zero, max = Vec3.Zero;
            foreach (MeshData m in order)
                for (int i = 0; i < m.Positions.Count; i += 3)
                {
                    var p = new Vec3(m.Positions[i], m.Positions[i + 1], m.Positions[i + 2]);
                    if (!any) { min = max = p; any = true; continue; }
                    min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
            result.BoundsMin = min;
            result.BoundsMax = max;
            return result;
        }

        private static bool ContainsName(List<MeshData> list, string materialId)
        {
            foreach (MeshData m in list) if (m.MaterialId == materialId) return true;
            return false;
        }

        private static void Ellipsoid(MeshData m, AssemblyPart p)
        {
            float minY = p.Parameters.TryGetValue("minY", out float my) ? my : -1f;
            int baseIndex = m.VertexCount;
            var rows = new List<int>();
            for (int i = 0; i <= Stacks; i++)
            {
                float v = i / (float)Stacks;                   // 0 top .. 1 bottom
                float cy = (float)Math.Cos(Math.PI * v);       // +1 .. -1
                if (cy < minY - 1e-4f) break;
                float sy = (float)Math.Sin(Math.PI * v);
                rows.Add(m.VertexCount);
                for (int j = 0; j <= Slices; j++)
                {
                    float u = j / (float)Slices;
                    float ax = (float)Math.Cos(2.0 * Math.PI * u), az = (float)Math.Sin(2.0 * Math.PI * u);
                    var dir = new Vec3(sy * ax, cy, sy * az);
                    var pos = new Vec3(p.Position.X + dir.X * p.Size.X, p.Position.Y + dir.Y * p.Size.Y, p.Position.Z + dir.Z * p.Size.Z);
                    var n = new Vec3(dir.X / Math.Max(1e-5f, p.Size.X), dir.Y / Math.Max(1e-5f, p.Size.Y), dir.Z / Math.Max(1e-5f, p.Size.Z));
                    m.AddVertex(pos, n);
                }
            }
            for (int i = 0; i + 1 < rows.Count; i++)
                for (int j = 0; j < Slices; j++)
                {
                    int a = rows[i] + j, b = rows[i] + j + 1, c = rows[i + 1] + j, d = rows[i + 1] + j + 1;
                    m.AddTriangle(a, c, b);
                    m.AddTriangle(b, c, d);
                }
            if (baseIndex == m.VertexCount) return;
        }

        private static void Segment(MeshData m, AssemblyPart p)
        {
            Vec3 a = p.Position;
            Vec3 b = new Vec3(p.Parameters["ex"], p.Parameters["ey"], p.Parameters["ez"]);
            float r0 = p.Parameters["r0"], r1 = p.Parameters["r1"];
            Vec3 axis = new Vec3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            float len = axis.Magnitude;
            if (len < 1e-5f) return;
            Vec3 w = axis.Normalized;
            Vec3 helper = Math.Abs(w.Y) < 0.95f ? Vec3.Up : new Vec3(1f, 0f, 0f);
            Vec3 u = Vec3.Cross(helper, w).Normalized;
            Vec3 v = Vec3.Cross(w, u);
            float slope = (r0 - r1) / len;
            int ringA = m.VertexCount;
            for (int j = 0; j <= Sides; j++)
            {
                float t = (float)(2.0 * Math.PI * j / Sides);
                Vec3 radial = new Vec3(u.X * (float)Math.Cos(t) + v.X * (float)Math.Sin(t), u.Y * (float)Math.Cos(t) + v.Y * (float)Math.Sin(t), u.Z * (float)Math.Cos(t) + v.Z * (float)Math.Sin(t));
                Vec3 n = new Vec3(radial.X + w.X * slope, radial.Y + w.Y * slope, radial.Z + w.Z * slope);
                m.AddVertex(new Vec3(a.X + radial.X * r0, a.Y + radial.Y * r0, a.Z + radial.Z * r0), n);
                m.AddVertex(new Vec3(b.X + radial.X * r1, b.Y + radial.Y * r1, b.Z + radial.Z * r1), n);
            }
            for (int j = 0; j < Sides; j++)
            {
                int a0 = ringA + 2 * j, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                m.AddTriangle(a0, b0, a1);
                m.AddTriangle(b0, b1, a1);
            }
            Cap(m, a, new Vec3(-w.X, -w.Y, -w.Z), u, v, r0, false);
            Cap(m, b, w, u, v, r1, true);
        }

        private static void Cap(MeshData m, Vec3 center, Vec3 normal, Vec3 u, Vec3 v, float radius, bool top)
        {
            int c = m.AddVertex(center, normal);
            int first = m.VertexCount;
            for (int j = 0; j <= Sides; j++)
            {
                float t = (float)(2.0 * Math.PI * j / Sides);
                Vec3 radial = new Vec3(u.X * (float)Math.Cos(t) + v.X * (float)Math.Sin(t), u.Y * (float)Math.Cos(t) + v.Y * (float)Math.Sin(t), u.Z * (float)Math.Cos(t) + v.Z * (float)Math.Sin(t));
                m.AddVertex(new Vec3(center.X + radial.X * radius, center.Y + radial.Y * radius, center.Z + radial.Z * radius), normal);
            }
            for (int j = 0; j < Sides; j++)
            {
                if (top) m.AddTriangle(c, first + j, first + j + 1);
                else m.AddTriangle(c, first + j + 1, first + j);
            }
        }

        private static void Box(MeshData m, AssemblyPart p)
        {
            Vec3 h = p.Size;
            Vec3[] corners = new Vec3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = Rotate(new Vec3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? -h.Y : h.Y, (i & 4) == 0 ? -h.Z : h.Z), p.EulerDegrees);
            int[][] faces =
            {
                new[] { 1, 3, 7, 5 }, new[] { 0, 4, 6, 2 },   // +x, -x
                new[] { 2, 6, 7, 3 }, new[] { 0, 1, 5, 4 },   // +y, -y
                new[] { 4, 5, 7, 6 }, new[] { 0, 2, 3, 1 }    // +z, -z
            };
            foreach (int[] f in faces)
            {
                Vec3 p0 = Offset(p.Position, corners[f[0]]), p1 = Offset(p.Position, corners[f[1]]), p2 = Offset(p.Position, corners[f[2]]), p3 = Offset(p.Position, corners[f[3]]);
                Vec3 n = Vec3.Cross(new Vec3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z), new Vec3(p2.X - p0.X, p2.Y - p0.Y, p2.Z - p0.Z));
                int i0 = m.AddVertex(p0, n), i1 = m.AddVertex(p1, n), i2 = m.AddVertex(p2, n), i3 = m.AddVertex(p3, n);
                m.AddTriangle(i0, i1, i2);
                m.AddTriangle(i0, i2, i3);
            }
        }

        private static Vec3 Offset(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }

        /// <summary>Rotates by Euler degrees: Z, then X, then Y (the Unity order).</summary>
        private static Vec3 Rotate(Vec3 v, Vec3 euler)
        {
            float z = euler.Z * MathUtil.Deg2Rad, x = euler.X * MathUtil.Deg2Rad, y = euler.Y * MathUtil.Deg2Rad;
            float cz = (float)Math.Cos(z), sz = (float)Math.Sin(z);
            v = new Vec3(v.X * cz - v.Y * sz, v.X * sz + v.Y * cz, v.Z);
            float cx = (float)Math.Cos(x), sx = (float)Math.Sin(x);
            v = new Vec3(v.X, v.Y * cx - v.Z * sx, v.Y * sx + v.Z * cx);
            float cy = (float)Math.Cos(y), sy = (float)Math.Sin(y);
            return new Vec3(v.X * cy + v.Z * sy, v.Y, -v.X * sy + v.Z * cy);
        }
    }

    /// <summary>Writes a glTF 2.0 binary (.glb): the standard exchange format, openable in Blender, Unity (with a glTF importer), browsers and 3D viewers.</summary>
    public static class GlbWriter
    {
        public static byte[] Write(ProceduralCharacterMesh mesh)
        {
            var bin = new MemoryStream();
            var bw = new BinaryWriter(bin);
            var json = new StringBuilder();
            var views = new StringBuilder();
            var accessors = new StringBuilder();
            var meshes = new StringBuilder();
            var materials = new StringBuilder();
            var nodes = new StringBuilder();
            int viewCount = 0, accessorCount = 0, primitiveCount = 0;
            var children = new StringBuilder();

            foreach (MeshData m in mesh.Meshes)
            {
                if (m.VertexCount == 0 || m.TriangleCount == 0) continue;
                // POSITION
                Align(bw);
                int posOffset = (int)bin.Position;
                float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
                for (int i = 0; i < m.Positions.Count; i += 3)
                {
                    float x = m.Positions[i], y = m.Positions[i + 1], z = m.Positions[i + 2];
                    bw.Write(x); bw.Write(y); bw.Write(z);
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); minZ = Math.Min(minZ, z);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); maxZ = Math.Max(maxZ, z);
                }
                int posLen = (int)bin.Position - posOffset;
                // NORMAL
                Align(bw);
                int norOffset = (int)bin.Position;
                foreach (float f in m.Normals) bw.Write(f);
                int norLen = (int)bin.Position - norOffset;
                // INDICES
                Align(bw);
                int idxOffset = (int)bin.Position;
                bool wide = m.VertexCount > 65535;
                foreach (int ix in m.Indices) { if (wide) bw.Write((uint)ix); else bw.Write((ushort)ix); }
                int idxLen = (int)bin.Position - idxOffset;

                int vPos = viewCount++, vNor = viewCount++, vIdx = viewCount++;
                if (views.Length > 0) views.Append(',');
                views.Append("{\"buffer\":0,\"byteOffset\":").Append(posOffset).Append(",\"byteLength\":").Append(posLen).Append(",\"target\":34962}");
                views.Append(",{\"buffer\":0,\"byteOffset\":").Append(norOffset).Append(",\"byteLength\":").Append(norLen).Append(",\"target\":34962}");
                views.Append(",{\"buffer\":0,\"byteOffset\":").Append(idxOffset).Append(",\"byteLength\":").Append(idxLen).Append(",\"target\":34963}");

                int aPos = accessorCount++, aNor = accessorCount++, aIdx = accessorCount++;
                if (accessors.Length > 0) accessors.Append(',');
                accessors.Append("{\"bufferView\":").Append(vPos).Append(",\"componentType\":5126,\"count\":").Append(m.VertexCount).Append(",\"type\":\"VEC3\",\"min\":[")
                         .Append(F(minX)).Append(',').Append(F(minY)).Append(',').Append(F(minZ)).Append("],\"max\":[").Append(F(maxX)).Append(',').Append(F(maxY)).Append(',').Append(F(maxZ)).Append("]}");
                accessors.Append(",{\"bufferView\":").Append(vNor).Append(",\"componentType\":5126,\"count\":").Append(m.VertexCount).Append(",\"type\":\"VEC3\"}");
                accessors.Append(",{\"bufferView\":").Append(vIdx).Append(",\"componentType\":").Append(wide ? 5125 : 5123).Append(",\"count\":").Append(m.Indices.Count).Append(",\"type\":\"SCALAR\"}");

                if (materials.Length > 0) materials.Append(',');
                DefaultColors.TryParse(m.ColorHex, out float r, out float g, out float b);
                materials.Append("{\"name\":\"").Append(Esc(m.MaterialId)).Append("\",\"pbrMetallicRoughness\":{\"baseColorFactor\":[")
                         .Append(F(Linear(r / 255f))).Append(',').Append(F(Linear(g / 255f))).Append(',').Append(F(Linear(b / 255f))).Append(",1],\"metallicFactor\":0,\"roughnessFactor\":")
                         .Append(F(1f - m.Smoothness)).Append("}}");

                if (meshes.Length > 0) meshes.Append(',');
                meshes.Append("{\"name\":\"").Append(Esc(m.Name)).Append("\",\"primitives\":[{\"attributes\":{\"POSITION\":").Append(aPos).Append(",\"NORMAL\":").Append(aNor)
                      .Append("},\"indices\":").Append(aIdx).Append(",\"material\":").Append(primitiveCount).Append(",\"mode\":4}]}");
                if (nodes.Length > 0) nodes.Append(',');
                nodes.Append("{\"name\":\"").Append(Esc(m.Name)).Append("\",\"mesh\":").Append(primitiveCount).Append('}');
                if (children.Length > 0) children.Append(',');
                children.Append(primitiveCount + 1);
                primitiveCount++;
            }
            Align(bw);
            bw.Flush();
            byte[] binBytes = bin.ToArray();

            json.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"FS27 Creator Engine procedural mannequin\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],");
            json.Append("\"nodes\":[{\"name\":\"").Append(Esc(mesh.CharacterId)).Append("\",\"children\":[").Append(children).Append("]}");
            if (nodes.Length > 0) json.Append(',').Append(nodes);
            json.Append("],\"meshes\":[").Append(meshes).Append("],\"materials\":[").Append(materials).Append("],\"accessors\":[").Append(accessors)
                .Append("],\"bufferViews\":[").Append(views).Append("],\"buffers\":[{\"byteLength\":").Append(binBytes.Length).Append("}]}");

            byte[] jsonBytes = Encoding.UTF8.GetBytes(json.ToString());
            int jsonPad = (4 - jsonBytes.Length % 4) % 4;
            int total = 12 + 8 + jsonBytes.Length + jsonPad + 8 + binBytes.Length;
            using (var ms = new MemoryStream(total))
            using (var w = new BinaryWriter(ms))
            {
                w.Write(0x46546C67u);   // "glTF"
                w.Write(2u);
                w.Write((uint)total);
                w.Write((uint)(jsonBytes.Length + jsonPad));
                w.Write(0x4E4F534Au);   // "JSON"
                w.Write(jsonBytes);
                for (int i = 0; i < jsonPad; i++) w.Write((byte)0x20);
                w.Write((uint)binBytes.Length);
                w.Write(0x004E4942u);   // "BIN\0"
                w.Write(binBytes);
                w.Flush();
                return ms.ToArray();
            }
        }

        private static void Align(BinaryWriter bw)
        {
            while (bw.BaseStream.Position % 4 != 0) bw.Write((byte)0);
        }

        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }

        private static float Linear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        private static string Esc(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    /// <summary>A tiny orthographic software rasteriser: flat-shaded, z-buffered, with 2x2 supersampling. It exists so the proportions of a character can be SEEN without Unity or a GPU.</summary>
    public static class SoftwareRenderer
    {
        public const float LightX = -0.45f, LightY = 0.75f, LightZ = 0.55f;

        /// <summary>RGB bytes (3 per pixel), top row first. <paramref name="yawDegrees"/> 0 = from the front, 90 = from the character's right side, 180 = from behind.</summary>
        public static byte[] Render(ProceduralCharacterMesh mesh, int width, int height, float yawDegrees, float margin = 0.06f)
        {
            const int ss = 2;
            int W = width * ss, Hh = height * ss;
            var color = new float[W * Hh * 3];
            var depth = new float[W * Hh];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.NegativeInfinity;
            for (int i = 0; i < W * Hh; i++) { color[i * 3] = 0.93f; color[i * 3 + 1] = 0.94f; color[i * 3 + 2] = 0.96f; }

            float yaw = yawDegrees * MathUtil.Deg2Rad;
            float cy = (float)Math.Cos(yaw), sy = (float)Math.Sin(yaw);
            float minY = mesh.BoundsMin.Y, maxY = mesh.BoundsMax.Y;
            float spanY = Math.Max(1e-4f, maxY - minY);
            float scale = (Hh * (1f - 2f * margin)) / spanY;
            float cx = W * 0.5f;
            float baseY = Hh * (1f - margin);
            float cxWorld = (mesh.BoundsMin.X + mesh.BoundsMax.X) * 0.5f, czWorld = (mesh.BoundsMin.Z + mesh.BoundsMax.Z) * 0.5f;
            Vec3 light = new Vec3(LightX, LightY, LightZ).Normalized;

            foreach (MeshData m in mesh.Meshes)
            {
                DefaultColors.TryParse(m.ColorHex, out float cr, out float cg, out float cb);
                cr /= 255f; cg /= 255f; cb /= 255f;
                int vc = m.VertexCount;
                var sx = new float[vc]; var sy2 = new float[vc]; var sz = new float[vc];
                var nx = new float[vc]; var ny = new float[vc]; var nz = new float[vc];
                for (int i = 0; i < vc; i++)
                {
                    float x = m.Positions[i * 3] - cxWorld, y = m.Positions[i * 3 + 1], z = m.Positions[i * 3 + 2] - czWorld;
                    // view space: yaw about Y; screen x = right, depth = towards the viewer
                    float vx = x * cy + z * sy, vz = -x * sy + z * cy;
                    sx[i] = cx + vx * scale;
                    sy2[i] = baseY - (y - minY) * scale;
                    sz[i] = vz;
                    float ax = m.Normals[i * 3], ay = m.Normals[i * 3 + 1], az = m.Normals[i * 3 + 2];
                    nx[i] = ax * cy + az * sy; ny[i] = ay; nz[i] = -ax * sy + az * cy;
                }
                for (int t = 0; t < m.Indices.Count; t += 3)
                {
                    int a = m.Indices[t], b = m.Indices[t + 1], c = m.Indices[t + 2];
                    float area = (sx[b] - sx[a]) * (sy2[c] - sy2[a]) - (sx[c] - sx[a]) * (sy2[b] - sy2[a]);
                    if (Math.Abs(area) < 1e-6f) continue;
                    int minPx = Math.Max(0, (int)Math.Floor(Math.Min(sx[a], Math.Min(sx[b], sx[c]))));
                    int maxPx = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(sx[a], Math.Max(sx[b], sx[c]))));
                    int minPy = Math.Max(0, (int)Math.Floor(Math.Min(sy2[a], Math.Min(sy2[b], sy2[c]))));
                    int maxPy = Math.Min(Hh - 1, (int)Math.Ceiling(Math.Max(sy2[a], Math.Max(sy2[b], sy2[c]))));
                    for (int py = minPy; py <= maxPy; py++)
                        for (int px = minPx; px <= maxPx; px++)
                        {
                            float fx = px + 0.5f, fy = py + 0.5f;
                            float w0 = ((sx[b] - fx) * (sy2[c] - fy) - (sx[c] - fx) * (sy2[b] - fy)) / area;
                            float w1 = ((sx[c] - fx) * (sy2[a] - fy) - (sx[a] - fx) * (sy2[c] - fy)) / area;
                            float w2 = 1f - w0 - w1;
                            if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                            float z = w0 * sz[a] + w1 * sz[b] + w2 * sz[c];
                            int idx = py * W + px;
                            if (z <= depth[idx]) continue;
                            float nnx = w0 * nx[a] + w1 * nx[b] + w2 * nx[c], nny = w0 * ny[a] + w1 * ny[b] + w2 * ny[c], nnz = w0 * nz[a] + w1 * nz[b] + w2 * nz[c];
                            float len = (float)Math.Sqrt(nnx * nnx + nny * nny + nnz * nnz);
                            if (len > 1e-6f) { nnx /= len; nny /= len; nnz /= len; }
                            float lambert = Math.Max(0f, nnx * light.X + nny * light.Y + nnz * light.Z);
                            float shade = 0.38f + 0.62f * lambert;
                            depth[idx] = z;
                            color[idx * 3] = cr * shade; color[idx * 3 + 1] = cg * shade; color[idx * 3 + 2] = cb * shade;
                        }
                }
            }

            var rgb = new byte[width * height * 3];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < ss; dy++)
                        for (int dx = 0; dx < ss; dx++)
                        {
                            int i = ((y * ss + dy) * W + (x * ss + dx)) * 3;
                            r += color[i]; g += color[i + 1]; b += color[i + 2];
                        }
                    int o = (y * width + x) * 3;
                    rgb[o] = To8(r / (ss * ss)); rgb[o + 1] = To8(g / (ss * ss)); rgb[o + 2] = To8(b / (ss * ss));
                }
            return rgb;
        }

        private static byte To8(float v) { return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v * 255f))); }
    }

    /// <summary>Writes a PNG with no compression (stored deflate blocks): pure C#, dependency-free, valid in every viewer.</summary>
    public static class PngWriter
    {
        public static byte[] Write(byte[] rgb, int width, int height)
        {
            var raw = new byte[(width * 3 + 1) * height];
            for (int y = 0; y < height; y++)
            {
                raw[y * (width * 3 + 1)] = 0;
                Buffer.BlockCopy(rgb, y * width * 3, raw, y * (width * 3 + 1) + 1, width * 3);
            }
            var z = new MemoryStream();
            z.WriteByte(0x78); z.WriteByte(0x01);
            int pos = 0;
            while (pos < raw.Length)
            {
                int n = Math.Min(65535, raw.Length - pos);
                bool last = pos + n >= raw.Length;
                z.WriteByte(last ? (byte)1 : (byte)0);
                z.WriteByte((byte)(n & 0xFF)); z.WriteByte((byte)(n >> 8));
                z.WriteByte((byte)(~n & 0xFF)); z.WriteByte((byte)((~n >> 8) & 0xFF));
                z.Write(raw, pos, n);
                pos += n;
            }
            uint adler = Adler32(raw);
            z.WriteByte((byte)(adler >> 24)); z.WriteByte((byte)(adler >> 16)); z.WriteByte((byte)(adler >> 8)); z.WriteByte((byte)adler);

            var png = new MemoryStream();
            png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
            var ihdr = new byte[13];
            BigEndian(ihdr, 0, (uint)width); BigEndian(ihdr, 4, (uint)height);
            ihdr[8] = 8; ihdr[9] = 2; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            Chunk(png, "IHDR", ihdr);
            Chunk(png, "IDAT", z.ToArray());
            Chunk(png, "IEND", new byte[0]);
            return png.ToArray();
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            BigEndian(len, 0, (uint)data.Length);
            s.Write(len, 0, 4);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Buffer.BlockCopy(data, 0, td, 4, data.Length);
            s.Write(td, 0, td.Length);
            var crc = new byte[4];
            BigEndian(crc, 0, Crc32(td));
            s.Write(crc, 0, 4);
        }

        private static void BigEndian(byte[] b, int at, uint v)
        {
            b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16); b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
        }

        private static uint[] crcTable;

        public static uint Crc32(byte[] data)
        {
            if (crcTable == null)
            {
                var t = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    t[n] = c;
                }
                crcTable = t;
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in data) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        public static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (byte x in data) { a = (a + x) % 65521; b = (b + a) % 65521; }
            return (b << 16) | a;
        }
    }

    /// <summary>What the preview of a character is, honestly: its proportions, counts, colours and (when asked) pictures.</summary>
    public sealed class CharacterPreviewData
    {
        public string CharacterId = "";
        public BodyProportions Proportions;
        public int Parts;
        public int Triangles;
        public readonly SortedDictionary<string, string> Colors = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public readonly List<string> Notes = new List<string>();
        /// <summary>A PNG with the front, side and back views (null until requested).</summary>
        public byte[] PngBytes;
        /// <summary>A glTF binary of the mannequin (null until requested).</summary>
        public byte[] GlbBytes;
    }

    /// <summary>Anything that can draw a character from its plan: the software renderer here, a Unity camera later.</summary>
    public interface ICharacterRenderer
    {
        string Name { get; }
        /// <summary>A PNG of the character from the front, side and back.</summary>
        byte[] RenderPng(CharacterAssemblyPlan plan, int viewWidth, int viewHeight);
    }

    /// <summary>Builds meshes from a plan with no engine: the procedural generator.</summary>
    public interface ICharacterGeometryGenerator
    {
        string Name { get; }
        ProceduralCharacterMesh Generate(CharacterAssemblyPlan plan);
    }

    public sealed class ProceduralCharacterGenerator : ICharacterGeometryGenerator, ICharacterRenderer, ICharacterAssembler<ProceduralCharacterMesh>
    {
        private readonly MaterialCatalog materials;
        private readonly CreatorCatalogs catalogs;

        public ProceduralCharacterGenerator(CreatorCatalogs catalogs, MaterialCatalog materials)
        {
            this.catalogs = catalogs;
            this.materials = materials;
        }

        public string Name => "FS27.ProceduralCharacterGenerator.v1";

        public ProceduralCharacterMesh Generate(CharacterAssemblyPlan plan)
        {
            return ProceduralMeshBuilder.Build(plan, materials);
        }

        public byte[] RenderPng(CharacterAssemblyPlan plan, int viewWidth, int viewHeight)
        {
            ProceduralCharacterMesh mesh = Generate(plan);
            byte[] front = SoftwareRenderer.Render(mesh, viewWidth, viewHeight, 0f);
            byte[] side = SoftwareRenderer.Render(mesh, viewWidth, viewHeight, 90f);
            byte[] back = SoftwareRenderer.Render(mesh, viewWidth, viewHeight, 180f);
            var all = new byte[viewWidth * 3 * viewHeight * 3];
            int rowBytes = viewWidth * 3;
            for (int y = 0; y < viewHeight; y++)
            {
                Buffer.BlockCopy(front, y * rowBytes, all, y * rowBytes * 3, rowBytes);
                Buffer.BlockCopy(side, y * rowBytes, all, y * rowBytes * 3 + rowBytes, rowBytes);
                Buffer.BlockCopy(back, y * rowBytes, all, y * rowBytes * 3 + rowBytes * 2, rowBytes);
            }
            return PngWriter.Write(all, viewWidth * 3, viewHeight);
        }

        /// <summary>Contract implementation: from resolved data (the animation reference is not used by a static mannequin).</summary>
        public ProceduralCharacterMesh Assemble(ResolvedCharacter character, AnimationSetReference animations)
        {
            return Generate(CharacterAssemblyPlanner.Plan(character, 0, catalogs, materials));
        }

        public CharacterPreviewData Preview(CharacterAssemblyPlan plan, bool withPng, bool withGlb)
        {
            ProceduralCharacterMesh mesh = Generate(plan);
            var data = new CharacterPreviewData { CharacterId = plan.CharacterId, Proportions = plan.Proportions, Parts = plan.Parts.Count, Triangles = mesh.Triangles };
            foreach (KeyValuePair<string, string> kv in plan.MaterialColors) data.Colors[kv.Key] = kv.Value;
            data.Notes.Add("Procedural mannequin: correct proportions and colours, simple shapes. It is not final art, not rigged and not animated.");
            if (withPng) data.PngBytes = RenderPng(plan, 240, 420);
            if (withGlb) data.GlbBytes = GlbWriter.Write(mesh);
            return data;
        }
    }
}
