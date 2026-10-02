// A HAND-WRITTEN stub of the few UnityEngine members the Creator Unity layer uses. It exists ONLY to catch typos and wrong signatures with
// the C# compiler; it is not Unity and proves nothing about runtime behaviour.
using System;
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Quaternion { public static Quaternion Euler(float x, float y, float z) { return default; } }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; } }
    public struct Rect { public Rect(float x, float y, float w, float h) { } }
    public static class Mathf
    {
        public static float Lerp(float a, float b, float t) { return a; }
        public static float Exp(float f) { return f; }
        public static float Sin(float f) { return f; }
        public static float Clamp01(float f) { return f; }
    }
    public class Object
    {
        public string name;
        public static T Instantiate<T>(T original, Transform parent) where T : Object { return original; }
        public static void DestroyImmediate(Object o) { }
        public static implicit operator bool(Object o) { return o != null; }
    }
    public class Component : Object { public Transform transform; public GameObject gameObject; }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class SerializeField : Attribute { }
    public class Transform : Component
    {
        public Vector3 localPosition, position, localEulerAngles; public Quaternion localRotation, rotation;
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void LookAt(Vector3 v) { }
    }
    public class GameObject : Object
    {
        public Transform transform; public GameObject(string name) { }
        public T AddComponent<T>() where T : Component, new() { return new T(); }
    }
    public class Shader : Object { public static Shader Find(string n) { return null; } }
    public class Material : Object
    {
        public Color color;
        public Material(Shader s) { }
        public bool HasProperty(string n) { return false; }
        public void SetFloat(string n, float v) { }
    }
    public class Mesh : Object
    {
        public UnityEngine.Rendering.IndexFormat indexFormat; public Vector3[] vertices, normals; public int[] triangles;
        public void RecalculateBounds() { }
    }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class Renderer : Component { public Material sharedMaterial; }
    public class MeshRenderer : Renderer { }
    public class Animator : Behaviour
    {
        public bool applyRootMotion; public float speed;
        public void CrossFadeInFixedTime(string name, float t) { }
    }
    public enum CameraClearFlags { SolidColor }
    public class Camera : Behaviour
    {
        public bool orthographic; public float orthographicSize; public CameraClearFlags clearFlags; public Color backgroundColor; public RenderTexture targetTexture; public Rect pixelRect;
        public void Render() { }
    }
    public class RenderTexture : Object { public static RenderTexture active; public RenderTexture(int w, int h, int d) { } public void Release() { } }
    public enum TextureFormat { RGB24 }
    public class Texture2D : Object { public Texture2D(int w, int h, TextureFormat f, bool mip) { } public void ReadPixels(Rect r, int x, int y) { } public void Apply() { } public byte[] EncodeToPNG() { return null; } }
}
namespace UnityEngine.Rendering { public enum IndexFormat { UInt16, UInt32 } }
