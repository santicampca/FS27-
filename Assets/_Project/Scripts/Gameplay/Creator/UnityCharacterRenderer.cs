using UnityEngine;
using FS27.Core;

namespace FS27.Gameplay.Creator
{
    /// <summary>
    /// ⚠️ NOT COMPILED OR RUN IN UNITY (see UnityCharacterAssembler.cs).
    /// Draws a character with a Unity camera (the engine-side counterpart of the Core's software renderer) and returns a PNG. Meant for editor
    /// tooling: a contact sheet of generated characters.
    /// </summary>
    public sealed class UnityCharacterRenderer : ICharacterRenderer
    {
        private readonly UnityCharacterAssembler assembler;

        public UnityCharacterRenderer(UnityCharacterAssembler assembler)
        {
            this.assembler = assembler;
        }

        public string Name => "FS27.UnityCharacterRenderer";

        public byte[] RenderPng(CharacterAssemblyPlan plan, int viewWidth, int viewHeight)
        {
            GameObject character = assembler.Assemble(plan);
            var camGo = new GameObject("PreviewCamera");
            Camera cam = camGo.AddComponent<Camera>();
            var rt = new RenderTexture(viewWidth * 3, viewHeight, 24);
            var tex = new Texture2D(viewWidth * 3, viewHeight, TextureFormat.RGB24, false);
            try
            {
                cam.orthographic = true;
                cam.orthographicSize = plan.Proportions.HeightMeters * 0.55f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.93f, 0.94f, 0.96f);
                cam.targetTexture = rt;
                float yaw = 0f;
                for (int view = 0; view < 3; view++, yaw += 90f)
                {
                    character.transform.rotation = Quaternion.Euler(0f, view == 2 ? 180f : view == 1 ? 90f : 0f, 0f);
                    camGo.transform.position = new Vector3(0f, plan.Proportions.HeightMeters * 0.5f, 3f);
                    camGo.transform.LookAt(new Vector3(0f, plan.Proportions.HeightMeters * 0.5f, 0f));
                    cam.pixelRect = new Rect(view * viewWidth, 0, viewWidth, viewHeight);
                    cam.Render();
                }
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, viewWidth * 3, viewHeight), 0, 0);
                tex.Apply();
                return tex.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(character);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
        }
    }
}
