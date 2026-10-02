using System.Collections.Generic;
using UnityEngine;
using FS27.Core;

namespace FS27.Gameplay.Creator
{
    // =====================================================================================================================
    // ⚠️ NOT COMPILED OR RUN IN UNITY. These files were written without a Unity editor available. They were only checked against a
    // hand-written stub of the few UnityEngine types they use (see Docs/CHARACTER_GENERATION.md). Treat them as a starting point that
    // needs a first compile, a first run and a visual check in Unity 6 / URP.
    //
    // Rules they follow: they only READ the plan (data) the Core produced; they never decide gameplay; they never move the player's root
    // (movement comes from Movement, not from animation); they hold no key and open no network.
    // =====================================================================================================================

    /// <summary>Loads a real asset by id (a mesh prefab, an Addressables key...). The project has no character assets yet, so the default loader returns nothing and the procedural proxy is used.</summary>
    public interface ICharacterAssetLoader
    {
        GameObject Load(string assetId);
    }

    public sealed class NoAssetLoader : ICharacterAssetLoader
    {
        public GameObject Load(string assetId) { return null; }
    }

    /// <summary>
    /// CharacterAssemblyPlan -> a GameObject hierarchy (one child per material colour, built from the Core's procedural mannequin geometry; real
    /// asset parts when an <see cref="ICharacterAssetLoader"/> can supply them). Implements the Core's ICharacterAssembler contract.
    /// </summary>
    public sealed class UnityCharacterAssembler : ICharacterAssembler<GameObject>
    {
        private readonly CreatorCatalogs catalogs;
        private readonly MaterialCatalog materials;
        private readonly ICharacterAssetLoader assets;
        private readonly Shader shader;

        public UnityCharacterAssembler(CreatorCatalogs catalogs, MaterialCatalog materials, ICharacterAssetLoader assets = null, Shader shader = null)
        {
            this.catalogs = catalogs;
            this.materials = materials;
            this.assets = assets ?? new NoAssetLoader();
            this.shader = shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit");
        }

        /// <summary>The Core contract: resolved data in, a character out (the animation reference is applied by <see cref="UnityAnimatorAdapter"/>).</summary>
        public GameObject Assemble(ResolvedCharacter character, AnimationSetReference animations)
        {
            return Assemble(CharacterAssemblyPlanner.Plan(character, 0, catalogs, materials));
        }

        public GameObject Assemble(CharacterAssemblyPlan plan)
        {
            var root = new GameObject("Character_" + plan.CharacterId);
            // procedural parts: the Core builds the triangles, Unity only wraps them
            ProceduralCharacterMesh built = ProceduralMeshBuilder.Build(plan, materials);
            foreach (MeshData data in built.Meshes)
            {
                var go = new GameObject(data.Name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = ToMesh(data);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = MakeMaterial(data);
            }
            // asset parts (none exist yet): only when a loader can provide them
            foreach (AssemblyPart part in plan.Parts)
            {
                if (part.Source != PartSource.Asset) continue;
                GameObject prefab = assets.Load(part.AssetId);
                if (prefab == null) continue;
                GameObject instance = Object.Instantiate(prefab, root.transform);
                instance.name = part.Id;
                instance.transform.localPosition = new Vector3(part.Position.X, part.Position.Y, part.Position.Z);
                instance.transform.localEulerAngles = new Vector3(part.EulerDegrees.X, part.EulerDegrees.Y, part.EulerDegrees.Z);
            }
            return root;
        }

        private static Mesh ToMesh(MeshData data)
        {
            var mesh = new Mesh { name = data.Name };
            if (data.VertexCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            var v = new Vector3[data.VertexCount];
            var n = new Vector3[data.VertexCount];
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                n[i] = new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]);
            }
            mesh.vertices = v;
            mesh.normals = n;
            mesh.triangles = data.Indices.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material MakeMaterial(MeshData data)
        {
            var m = new Material(shader) { name = data.MaterialId };
            DefaultColors.TryParse(data.ColorHex, out float r, out float g, out float b);
            m.color = new Color(r / 255f, g / 255f, b / 255f, 1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", data.Smoothness);
            return m;
        }
    }
}
