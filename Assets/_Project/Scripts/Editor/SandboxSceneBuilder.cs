using System.IO;
using FS27.Cameras;
using FS27.Gameplay;
using FS27.Infrastructure;
using FS27.Input;
using FS27.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FS27.EditorTools
{
    /// <summary>
    /// One-click builder for the Phase 1 Sandbox: creates the tuning/camera assets, the Player and Ball
    /// prefabs, the placeholder pitch, the HUD (joystick + stamina bar) and saves Assets/_Project/Scenes/Sandbox.unity.
    /// Safe to run again: it rebuilds the scene and prefabs but keeps existing tuning assets (your edits survive).
    /// </summary>
    public static class SandboxSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ScenePath = Root + "/Scenes/Sandbox.unity";

        private const float FieldLength = 40f; // world X
        private const float FieldWidth = 25f;  // world Z

        [MenuItem("FS27/1. Build Sandbox Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Data/Tuning");
            EnsureFolder(Root + "/Data/Cameras");
            EnsureFolder(Root + "/Art/Materials");

            TuningProfile tuning = GetOrCreateAsset<TuningProfile>(Root + "/Data/Tuning/DefaultTuning.asset");
            CameraProfile tvProfile = GetOrCreateAsset<CameraProfile>(Root + "/Data/Cameras/TvCamera.asset");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material grassA = GetOrCreateMaterial("Grass_A", new Color(0.20f, 0.52f, 0.22f));
            Material grassB = GetOrCreateMaterial("Grass_B", new Color(0.24f, 0.58f, 0.25f));
            Material lineMat = GetOrCreateMaterial("Line", new Color(0.95f, 0.95f, 0.95f));
            Material wallMat = GetOrCreateMaterial("Wall", new Color(0.35f, 0.38f, 0.42f));
            Material bodyMat = GetOrCreateMaterial("Player_Body", new Color(0.15f, 0.45f, 0.95f));
            Material noseMat = GetOrCreateMaterial("Player_Nose", new Color(1f, 1f, 1f));
            Material ballMat = GetOrCreateMaterial("Ball", new Color(0.97f, 0.97f, 0.97f));

            GameObject playerPrefab = BuildPlayerPrefab(tuning, bodyMat, noseMat);
            GameObject ballPrefab = BuildBallPrefab(ballMat);

            // ---- Environment ----
            SetupLightingAndAmbient();
            FieldBounds bounds = BuildField(grassA, grassB, lineMat, wallMat);

            // ---- Actors ----
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 0f, -4f);
            var ball = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab);
            ball.transform.position = new Vector3(0f, 0.2f, 3f);

            // ---- Camera ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.16f, 0.24f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.fieldOfView = tvProfile.FieldOfView;
            camGo.AddComponent<AudioListener>();
            var cameraManager = camGo.AddComponent<CameraManager>();
            SetRef(cameraManager, "target", player.transform);
            SetRef(cameraManager, "profile", tvProfile);
            camGo.transform.position = new Vector3(0f, tvProfile.Height, -4f - tvProfile.Distance);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -4f) - camGo.transform.position);

            // ---- HUD ----
            VirtualJoystick joystick = BuildHud(player.GetComponent<PlayerEntity>());

            // ---- Wiring player <-> scene ----
            SetRef(player.GetComponent<PlayerMovement>(), "bounds", bounds);
            SetRef(player.GetComponent<BallInteractor>(), "ball", ball.GetComponent<BallController>());
            SetRefArray(player.GetComponent<PlayerIntentProvider>(), "sourceBehaviours",
                new Object[] { player.GetComponent<KeyboardGamepadIntentSource>(), joystick });

            // ---- Systems ----
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(tuning);
            Debug.Log("[FS27] Sandbox scene built: " + ScenePath + ". Press Play. Tune feel in " + Root + "/Data/Tuning/DefaultTuning.asset");
        }

        [MenuItem("FS27/2. Apply Mobile Settings (Android)")]
        public static void ApplyMobileSettings()
        {
            PlayerSettings.productName = "FS27";
            PlayerSettings.companyName = "FS27";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            var android = UnityEditor.Build.NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, "com.fs27.game");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            AssetDatabase.SaveAssets();
            Debug.Log("[FS27] Mobile settings applied: landscape only, IL2CPP, ARM64, id com.fs27.game.");
        }

        // ------------------------------------------------------------------ prefabs

        private static GameObject BuildPlayerPrefab(TuningProfile tuning, Material bodyMat, Material noseMat)
        {
            var root = new GameObject("Player");

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.radius = 0.35f;
            capsule.height = 1.8f;

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var entity = root.AddComponent<PlayerEntity>();
            SetRef(entity, "tuning", tuning);

            var provider = root.AddComponent<PlayerIntentProvider>();
            var keyboard = root.AddComponent<KeyboardGamepadIntentSource>();
            SetRefArray(provider, "sourceBehaviours", new Object[] { keyboard });

            root.AddComponent<PlayerMovement>();
            root.AddComponent<BallInteractor>();
            var anim = root.AddComponent<PlayerAnimation>();
            SetRef(anim, "entity", entity);

            // Visual is a separate child: swap its contents for a real character later, logic is untouched.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(visual.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Small "nose" so the facing direction is visible on a capsule.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(visual.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.3f, 0.38f);
            nose.transform.localScale = new Vector3(0.25f, 0.2f, 0.35f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = noseMat;

            string path = Root + "/Prefabs/Player.prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return asset;
        }

        private static GameObject BuildBallPrefab(Material ballMat)
        {
            var root = new GameObject("Ball");
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.2f;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.43f;

            var controller = root.AddComponent<BallController>();
            root.AddComponent<BallPhysics>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.4f;
            visual.GetComponent<MeshRenderer>().sharedMaterial = ballMat;
            SetRef(controller, "visual", visual.transform);

            string path = Root + "/Prefabs/Ball.prefab";
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return asset;
        }

        // ------------------------------------------------------------------ environment

        private static void SetupLightingAndAmbient()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
        }

        private static FieldBounds BuildField(Material grassA, Material grassB, Material lineMat, Material wallMat)
        {
            var field = new GameObject("Field");
            var bounds = field.AddComponent<FieldBounds>();
            bounds.SetHalfExtents(new Vector2(FieldLength * 0.5f, FieldWidth * 0.5f));

            // Ground (collider top surface is at y = 0).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(field.transform, false);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.transform.localScale = new Vector3(FieldLength + 4f, 0.2f, FieldWidth + 4f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = grassA;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic);

            // Mowing stripes: help the eye perceive speed. Purely visual (no colliders).
            const int stripes = 10;
            float stripeLength = FieldLength / stripes;
            for (int i = 1; i < stripes; i += 2)
            {
                float x = -FieldLength * 0.5f + stripeLength * (i + 0.5f);
                CreateFlat("Stripe_" + i, field.transform, new Vector3(x, 0.01f, 0f), new Vector3(stripeLength, 0.02f, FieldWidth), grassB);
            }

            // Pitch lines.
            const float t = 0.12f;
            CreateFlat("Line_Halfway", field.transform, new Vector3(0f, 0.015f, 0f), new Vector3(t, 0.02f, FieldWidth), lineMat);
            CreateFlat("Line_Top", field.transform, new Vector3(0f, 0.015f, FieldWidth * 0.5f - t * 0.5f), new Vector3(FieldLength, 0.02f, t), lineMat);
            CreateFlat("Line_Bottom", field.transform, new Vector3(0f, 0.015f, -FieldWidth * 0.5f + t * 0.5f), new Vector3(FieldLength, 0.02f, t), lineMat);
            CreateFlat("Line_Left", field.transform, new Vector3(-FieldLength * 0.5f + t * 0.5f, 0.015f, 0f), new Vector3(t, 0.02f, FieldWidth), lineMat);
            CreateFlat("Line_Right", field.transform, new Vector3(FieldLength * 0.5f - t * 0.5f, 0.015f, 0f), new Vector3(t, 0.02f, FieldWidth), lineMat);

            // Low boundary walls: they keep the ball in the sandbox (inner faces sit on the field edge).
            const float wallH = 1.2f;
            const float wallT = 0.5f;
            CreateWall("Wall_Left", field.transform, new Vector3(-FieldLength * 0.5f - wallT * 0.5f, wallH * 0.5f, 0f), new Vector3(wallT, wallH, FieldWidth + wallT * 2f), wallMat);
            CreateWall("Wall_Right", field.transform, new Vector3(FieldLength * 0.5f + wallT * 0.5f, wallH * 0.5f, 0f), new Vector3(wallT, wallH, FieldWidth + wallT * 2f), wallMat);
            CreateWall("Wall_Top", field.transform, new Vector3(0f, wallH * 0.5f, FieldWidth * 0.5f + wallT * 0.5f), new Vector3(FieldLength, wallH, wallT), wallMat);
            CreateWall("Wall_Bottom", field.transform, new Vector3(0f, wallH * 0.5f, -FieldWidth * 0.5f - wallT * 0.5f), new Vector3(FieldLength, wallH, wallT), wallMat);

            return bounds;
        }

        private static void CreateFlat(string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static void CreateWall(string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        // ------------------------------------------------------------------ HUD

        private static VirtualJoystick BuildHud(PlayerEntity player)
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform safe = CreateRect("SafeArea", canvasGo.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Joystick: the zone (left half of the screen) receives touches; base + knob are only visuals.
            RectTransform zone = CreateRect("JoystickZone", safe);
            zone.anchorMin = new Vector2(0f, 0f);
            zone.anchorMax = new Vector2(0.5f, 1f);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
            var zoneImage = zone.gameObject.AddComponent<Image>();
            zoneImage.color = new Color(1f, 1f, 1f, 0f);
            zoneImage.raycastTarget = true;
            var joystick = zone.gameObject.AddComponent<VirtualJoystick>();

            const float radius = 150f;
            RectTransform stickBase = CreateRect("Base", zone);
            stickBase.anchorMin = Vector2.zero;
            stickBase.anchorMax = Vector2.zero;
            stickBase.pivot = new Vector2(0.5f, 0.5f);
            stickBase.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            stickBase.anchoredPosition = new Vector2(280f, 260f);
            var baseImage = stickBase.gameObject.AddComponent<Image>();
            baseImage.sprite = circle;
            baseImage.color = new Color(1f, 1f, 1f, 0.35f);
            baseImage.raycastTarget = false;
            var group = stickBase.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            RectTransform knob = CreateRect("Knob", stickBase);
            knob.anchorMin = new Vector2(0.5f, 0.5f);
            knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(130f, 130f);
            knob.anchoredPosition = Vector2.zero;
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = circle;
            knobImage.color = new Color(1f, 1f, 1f, 0.75f);
            knobImage.raycastTarget = false;

            SetRef(joystick, "zone", zone);
            SetRef(joystick, "stickBase", stickBase);
            SetRef(joystick, "knob", knob);
            SetRef(joystick, "visuals", group);
            SetFloat(joystick, "radius", radius);
            SetVector2(joystick, "restPosition", new Vector2(280f, 260f));

            // Stamina bar (top-left).
            RectTransform bar = CreateRect("StaminaBar", safe);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.sizeDelta = new Vector2(420f, 34f);
            bar.anchoredPosition = new Vector2(40f, -40f);
            var barBg = bar.gameObject.AddComponent<Image>();
            barBg.color = new Color(0f, 0f, 0f, 0.55f);
            barBg.raycastTarget = false;

            RectTransform fill = CreateRect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.25f, 0.85f, 0.35f);
            fillImage.raycastTarget = false;

            var staminaBar = bar.gameObject.AddComponent<StaminaBar>();
            SetRef(staminaBar, "player", player);
            SetRef(staminaBar, "fill", fill);
            SetRef(staminaBar, "fillImage", fillImage);

            // Development readout (speed / mode / stamina %).
            RectTransform readout = CreateRect("DebugReadout", safe);
            readout.anchorMin = new Vector2(0f, 1f);
            readout.anchorMax = new Vector2(0f, 1f);
            readout.pivot = new Vector2(0f, 1f);
            readout.sizeDelta = new Vector2(700f, 90f);
            readout.anchoredPosition = new Vector2(40f, -84f);
            var label = readout.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 30;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            label.text = "";
            var debug = readout.gameObject.AddComponent<DebugReadout>();
            SetRef(debug, "player", player);
            SetRef(debug, "label", label);

            return joystick;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ asset helpers

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Glossiness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------------ serialized-field helpers

        private static SerializedProperty Prop(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogError("[FS27] Builder: field '" + field + "' not found on " + so.targetObject.GetType().Name);
            return p;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, field);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, field);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Object target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Prop(so, field);
            if (p == null) return;
            p.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
