using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    public enum PartKind
    {
        Body = 0,
        Head,
        Face,
        Hair,
        FacialHair,
        Clothing,
        Boot,
        Glove
    }

    public enum PartSource
    {
        /// <summary>Built from a procedural definition (a generator name and numbers): no asset needed.</summary>
        Procedural = 0,
        /// <summary>A real asset (a mesh in the project): none exist yet; this is the seam for them.</summary>
        Asset
    }

    /// <summary>
    /// One piece of a character as DATA: where it attaches, where it sits and how big it is, which material it uses, and either an asset id or a
    /// procedural definition. A mesh is NEVER stored here (nor in any JSON): only the recipe to get one.
    /// </summary>
    [Serializable]
    public sealed class AssemblyPart
    {
        public string Id = "";
        public string Slot = "";
        public PartKind Kind;
        public PartSource Source;
        /// <summary>The asset to load when <see cref="Source"/> is Asset (empty for procedural parts).</summary>
        public string AssetId = "";
        /// <summary>"ellipsoid", "segment" (tapered cylinder) or "box": the procedural definition (empty for asset parts).</summary>
        public string Generator = "";
        public string MaterialId = "";
        /// <summary>Overrides the material's colour for this part only (mixed colours such as stubble).</summary>
        public string ColorOverride = "";
        public string Bone = "";
        /// <summary>Metres, character space (Y up, facing +Z, origin between the feet). The start point of a segment.</summary>
        public Vec3 Position;
        public Vec3 EulerDegrees;
        /// <summary>Ellipsoid radii, box half-extents.</summary>
        public Vec3 Size;
        /// <summary>Generator numbers: segment end point (ex, ey, ez) and radii (r0, r1); ellipsoid clip plane (minY).</summary>
        public SortedDictionary<string, float> Parameters = new SortedDictionary<string, float>(StringComparer.Ordinal);
    }

    [Serializable]
    public sealed class ProxyBone
    {
        public string Name = "";
        public string Parent = "";
        /// <summary>Rest position, character space.</summary>
        public Vec3 Position;
    }

    /// <summary>The numbers of the body, in metres, as the plan computed them (for the report and for checking proportions).</summary>
    [Serializable]
    public sealed class BodyProportions
    {
        public float HeightMeters;
        public float HeadHeightMeters;
        public float HeadsTall;
        public float ShoulderWidth;
        public float HipHeight;
        public float LegLength;
        public float ArmLength;
        public float TorsoLength;
    }

    /// <summary>Everything needed to build the visible character, as data and nothing else. Unity or the procedural generator builds from it.</summary>
    [Serializable]
    public sealed class CharacterAssemblyPlan
    {
        public const string CurrentSchema = "FS27.CharacterAssemblyPlan.v1";

        public string SchemaVersion = CurrentSchema;
        public string CharacterId = "";
        public string BaseModelId = "";
        public string RigId = "";
        public string StyleId = "";
        public uint AppearanceSeed;
        public BodyProportions Proportions = new BodyProportions();
        public List<ProxyBone> Bones = new List<ProxyBone>();
        public List<AssemblyPart> Parts = new List<AssemblyPart>();
        /// <summary>Material id to its final colour for this character.</summary>
        public SortedDictionary<string, string> MaterialColors = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public List<string> Warnings = new List<string>();

        public int ProceduralParts
        {
            get
            {
                int n = 0;
                foreach (AssemblyPart p in Parts) if (p.Source == PartSource.Procedural) n++;
                return n;
            }
        }

        public AssemblyPart Find(string id)
        {
            foreach (AssemblyPart p in Parts) if (p.Id == id) return p;
            return null;
        }
    }

    /// <summary>The colours a character gets when its specification names none.</summary>
    public static class DefaultColors
    {
        public const string Hair = "#3A2A1A";
        public const string Eyes = "#4A6FA5";
        public const string KitPrimary = "#2255CC";
        public const string KitSecondary = "#F2F2F2";
        public const string KitAccent = "#F2C500";
        public const string Boots = "#222222";
        public const string Gloves = "#F2C500";

        /// <summary>The skin ramp: three stops, light to dark, with the undertone shifting warm/cool a little. Starting values meant to be tuned.</summary>
        public static string Skin(float tone01, float undertone01)
        {
            float t = MathUtil.Clamp01(tone01);
            Vec3 light = new Vec3(244, 215, 190), mid = new Vec3(198, 142, 99), dark = new Vec3(75, 46, 30);
            Vec3 c = t < 0.5f ? Vec3.Lerp(light, mid, t * 2f) : Vec3.Lerp(mid, dark, (t - 0.5f) * 2f);
            float warm = (MathUtil.Clamp01(undertone01) - 0.5f) * 0.16f;
            return Hex(c.X * (1f + warm), c.Y, c.Z * (1f - warm));
        }

        public static string Hex(float r, float g, float b)
        {
            return "#" + Byte(r).ToString("X2", CultureInfo.InvariantCulture) + Byte(g).ToString("X2", CultureInfo.InvariantCulture) + Byte(b).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static int Byte(float v) { return (int)Math.Max(0f, Math.Min(255f, (float)Math.Round(v))); }

        public static bool TryParse(string hex, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (hex == null || hex.Length != 7 || hex[0] != '#') return false;
            return int.TryParse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int ri)
                && int.TryParse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int gi)
                && int.TryParse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int bi)
                && Set(ri, gi, bi, out r, out g, out b);
        }

        private static bool Set(int ri, int gi, int bi, out float r, out float g, out float b) { r = ri; g = gi; b = bi; return true; }

        public static string Mix(string a, string b, float t)
        {
            if (!TryParse(a, out float ar, out float ag, out float ab) || !TryParse(b, out float br, out float bg, out float bb)) return a;
            t = MathUtil.Clamp01(t);
            return Hex(ar + (br - ar) * t, ag + (bg - ag) * t, ab + (bb - ab) * t);
        }
    }

    /// <summary>
    /// ResolvedCharacter -> CharacterAssemblyPlan. Pure and deterministic. Computes the proportions of the body from the appearance parameters and
    /// the style, lays out a proxy skeleton and every part (body, head, face, hair, facial hair, kit, boots, gloves) as procedural definitions,
    /// and gives each its material and colour. The result is a MANNEQUIN-quality build: correct proportions and colours, simple shapes.
    /// </summary>
    public static class CharacterAssemblyPlanner
    {
        public static CharacterAssemblyPlan Plan(CharacterSpecification spec, CreatorCatalogs catalogs, MaterialCatalog materials)
        {
            return Plan(new CatalogAppearanceResolver(catalogs).Resolve(spec), spec.AppearanceSeed, catalogs, materials);
        }

        public static CharacterAssemblyPlan Plan(ResolvedCharacter rc, uint appearanceSeed, CreatorCatalogs catalogs, MaterialCatalog materials)
        {
            var plan = new CharacterAssemblyPlan
            {
                CharacterId = rc.CharacterId, BaseModelId = rc.BaseModelId, RigId = rc.RigId ?? "", StyleId = rc.StyleId, AppearanceSeed = appearanceSeed
            };
            plan.Warnings.AddRange(rc.Warnings);

            float headHeights = 6.5f;
            if (catalogs.Appearance.TryGetBaseModel(rc.BaseModelId, out BaseModelDefinition bm)) headHeights = bm.HeadHeights;
            if (catalogs.Styles.TryGet(rc.StyleId, out StylePreset style)) headHeights = style.HeadHeights;

            // pass 1 with a unit height finds how tall the figure comes out; pass 2 scales it to the requested height
            float desired = 1.78f * S(rc, "body.height");
            var probe = new Builder(rc, headHeights, 1f);
            probe.Build();
            float height = desired / probe.TopY;
            var b = new Builder(rc, headHeights, height);
            b.Build();

            plan.Parts.AddRange(b.Parts);
            plan.Bones.AddRange(b.Bones);
            plan.Proportions = new BodyProportions
            {
                HeightMeters = b.TopY, HeadHeightMeters = b.HeadH, HeadsTall = b.TopY / Math.Max(1e-4f, b.HeadH), ShoulderWidth = 2f * b.ShoulderHalf, HipHeight = b.HipY,
                LegLength = b.HipY - b.AnkleY, ArmLength = b.UpperArm + b.ForeArm, TorsoLength = b.ShoulderY - b.HipY
            };

            // colours
            string Col(string slot, string fallback) { return rc.Colors.TryGetValue(slot, out string c) ? c : fallback; }
            float sat = style != null ? style.Saturation : 1f;
            plan.MaterialColors["material.skin_toon"] = DefaultColors.Skin(S01(rc, "skin.tone"), S01(rc, "skin.undertone"));
            plan.MaterialColors["material.hair_toon"] = Col("hair.color", DefaultColors.Hair);
            plan.MaterialColors["material.brow_toon"] = Col("hair.color", DefaultColors.Hair);
            plan.MaterialColors["material.eye_white"] = "#F5F5F0";
            plan.MaterialColors["material.eye_iris"] = Col("eyes.color", DefaultColors.Eyes);
            plan.MaterialColors["material.mouth_dark"] = "#6A2A2A";
            plan.MaterialColors["material.shirt_cloth"] = Col("kit.primary", DefaultColors.KitPrimary);
            plan.MaterialColors["material.shorts_cloth"] = Col("kit.secondary", DefaultColors.KitSecondary);
            plan.MaterialColors["material.socks_cloth"] = Col("kit.accent", DefaultColors.KitAccent);
            plan.MaterialColors["material.boots_gloss"] = Col("kit.boots", DefaultColors.Boots);
            plan.MaterialColors["material.gloves_grip"] = Col("kit.gloves", DefaultColors.Gloves);
            if (Math.Abs(sat - 1f) > 1e-3f)
            {
                var keys = new List<string>(plan.MaterialColors.Keys);
                foreach (string k in keys) plan.MaterialColors[k] = Saturate(plan.MaterialColors[k], sat);
            }

            foreach (AssemblyPart p in plan.Parts)
                if (p.ColorOverride == "stubble") p.ColorOverride = DefaultColors.Mix(plan.MaterialColors["material.skin_toon"], plan.MaterialColors["material.hair_toon"], 0.5f);

            foreach (AssemblyPart p in plan.Parts)
                if (!materials.Contains(p.MaterialId)) plan.Warnings.Add("Part '" + p.Id + "' uses the unknown material '" + p.MaterialId + "'.");
            return plan;
        }

        private static string Saturate(string hex, float k)
        {
            if (!DefaultColors.TryParse(hex, out float r, out float g, out float bl)) return hex;
            float l = 0.299f * r + 0.587f * g + 0.114f * bl;
            return DefaultColors.Hex(l + (r - l) * k, l + (g - l) * k, l + (bl - l) * k);
        }

        private static float S(ResolvedCharacter rc, string id) { return rc.Scales.TryGetValue(id, out float v) ? v : 1f; }
        private static float S01(ResolvedCharacter rc, string id) { return rc.Scales.TryGetValue(id, out float v) ? v : 0.5f; }

        private sealed class Builder
        {
            private readonly ResolvedCharacter rc;
            private readonly float headHeights;
            private readonly float H;

            public readonly List<AssemblyPart> Parts = new List<AssemblyPart>();
            public readonly List<ProxyBone> Bones = new List<ProxyBone>();
            public float TopY, HeadH, ShoulderHalf, HipY, AnkleY, ShoulderY, UpperArm, ForeArm;

            public Builder(ResolvedCharacter rc, float headHeights, float height)
            {
                this.rc = rc;
                this.headHeights = headHeights;
                H = height;
            }

            private float S(string id) { return PlanS(rc, id); }
            private static float PlanS(ResolvedCharacter rc, string id) { return rc.Scales.TryGetValue(id, out float v) ? v : 1f; }
            private float L(string id) { return rc.Scales.TryGetValue(id, out float v) ? v : 0.5f; }

            private void Ellipsoid(string id, string slot, PartKind kind, string bone, string mat, Vec3 c, Vec3 radii, float minY = -1f, string color = "")
            {
                var p = new AssemblyPart { Id = id, Slot = slot, Kind = kind, Generator = "ellipsoid", MaterialId = mat, Bone = bone, Position = c, Size = radii, ColorOverride = color };
                if (minY > -0.999f) p.Parameters["minY"] = minY;
                Parts.Add(p);
            }

            private void Segment(string id, string slot, PartKind kind, string bone, string mat, Vec3 a, Vec3 b, float r0, float r1, string color = "")
            {
                var p = new AssemblyPart { Id = id, Slot = slot, Kind = kind, Generator = "segment", MaterialId = mat, Bone = bone, Position = a, ColorOverride = color };
                p.Parameters["ex"] = b.X; p.Parameters["ey"] = b.Y; p.Parameters["ez"] = b.Z; p.Parameters["r0"] = r0; p.Parameters["r1"] = r1;
                Parts.Add(p);
            }

            private void Box(string id, string slot, PartKind kind, string bone, string mat, Vec3 c, Vec3 half, Vec3 euler)
            {
                Parts.Add(new AssemblyPart { Id = id, Slot = slot, Kind = kind, Generator = "box", MaterialId = mat, Bone = bone, Position = c, Size = half, EulerDegrees = euler });
            }

            private void Bone(string name, string parent, Vec3 pos) { Bones.Add(new ProxyBone { Name = name, Parent = parent, Position = pos }); }

            private static Vec3 Add(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
            private static Vec3 Mul(Vec3 a, float s) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }

            public void Build()
            {
                Parts.Clear();
                Bones.Clear();
                float mass = S("body.mass");
                float musc = L("body.muscularity");
                float limb = (0.92f + 0.30f * musc) * (0.9f + 0.1f * mass);
                float thick = 0.9f + 0.1f * mass;

                // ---- vertical layout
                AnkleY = 0.04f * H;
                float legLen = 0.48f * H * S("body.legLength");
                HipY = AnkleY + legLen;
                float torsoLen = 0.30f * H * S("body.torsoLength");
                ShoulderY = HipY + torsoLen;
                float neckLen = 0.055f * H * S("body.neckLength");
                HeadH = H / headHeights * S("head.scale") * S("head.height");
                float headW = HeadH * 0.78f * S("head.width");
                float headD = HeadH * 0.88f;
                float headBottom = ShoulderY + 0.015f * H + neckLen;
                float headCy = headBottom + HeadH * 0.5f;
                TopY = headBottom + HeadH;

                // ---- horizontal layout
                ShoulderHalf = 0.13f * H * S("body.shoulderWidth") * (0.97f + 0.08f * musc);
                float chestHalf = 0.098f * H * S("body.torsoWidth") * (0.92f + 0.14f * (mass - 1f) + 0.1f * musc);
                float chestDepth = 0.075f * H * thick * (0.95f + 0.1f * musc);
                float pelvisHalf = 0.088f * H * S("body.hipWidth") * (0.95f + 0.12f * (mass - 1f));
                UpperArm = 0.19f * H * S("body.armLength");
                ForeArm = 0.165f * H * S("body.armLength");
                float thighLen = legLen * 0.5f, shinLen = legLen * 0.5f;

                // ---- skeleton (rest pose)
                Bone("root", "", Vec3.Zero);
                Bone("pelvis", "root", new Vec3(0f, HipY, 0f));
                Bone("spine", "pelvis", new Vec3(0f, HipY + torsoLen * 0.45f, 0f));
                Bone("chest", "spine", new Vec3(0f, ShoulderY - 0.04f * H, 0f));
                Bone("neck", "chest", new Vec3(0f, ShoulderY + 0.01f * H, 0f));
                Bone("head", "neck", new Vec3(0f, headBottom, 0f));

                // ---- torso, pelvis, neck, head
                Ellipsoid("body.pelvis", "body", PartKind.Body, "pelvis", "material.shorts_cloth", new Vec3(0f, HipY + 0.015f * H, 0f), new Vec3(pelvisHalf, 0.068f * H, chestDepth * 0.95f));
                Ellipsoid("body.abdomen", "body", PartKind.Body, "spine", "material.shirt_cloth", new Vec3(0f, HipY + torsoLen * 0.42f, 0f), new Vec3(chestHalf * 0.93f, torsoLen * 0.34f, chestDepth * 0.92f));
                Ellipsoid("body.chest", "body", PartKind.Body, "chest", "material.shirt_cloth", new Vec3(0f, ShoulderY - 0.07f * H, 0f), new Vec3(Math.Max(chestHalf, ShoulderHalf * 0.82f), 0.098f * H, chestDepth));
                Segment("body.neck", "body", PartKind.Body, "neck", "material.skin_toon", new Vec3(0f, ShoulderY - 0.01f * H, 0f), new Vec3(0f, headBottom + 0.01f * H, 0.005f * H), 0.034f * H * limb, 0.029f * H * limb);

                Ellipsoid("head.skull", "head", PartKind.Head, "head", "material.skin_toon", new Vec3(0f, headCy, 0f), new Vec3(headW * 0.5f, HeadH * 0.5f, headD * 0.5f));
                float jaw = L("head.jaw"), chin = L("head.chin");
                Ellipsoid("head.jaw", "head", PartKind.Head, "head", "material.skin_toon", new Vec3(0f, headCy - HeadH * 0.28f, headD * 0.04f),
                          new Vec3(headW * (0.34f + 0.1f * jaw), HeadH * (0.20f + 0.03f * chin), headD * (0.36f + 0.04f * chin)));

                // ---- face
                float eyeSize = S("face.eyeSize"), eyeSpacing = S("face.eyeSpacing");
                float eyeY = headCy + HeadH * 0.04f;
                float eyeX = headW * 0.19f * eyeSpacing;
                float eyeR = HeadH * 0.075f * eyeSize * (0.9f + 0.25f * L("eyes.roundness"));
                float eyeZ = headD * 0.5f - eyeR * (0.55f + 0.5f * L("eyes.depth"));
                for (int side = -1; side <= 1; side += 2)
                {
                    string s = side < 0 ? "l" : "r";
                    Ellipsoid("face.eye_" + s, "face.eyeShape", PartKind.Face, "head", "material.eye_white", new Vec3(side * eyeX, eyeY, eyeZ), new Vec3(eyeR, eyeR * 1.05f, eyeR * 0.55f));
                    Ellipsoid("face.iris_" + s, "face.eyeShape", PartKind.Face, "head", "material.eye_iris", new Vec3(side * eyeX, eyeY, eyeZ + eyeR * 0.28f), new Vec3(eyeR * 0.55f, eyeR * 0.6f, eyeR * 0.3f));
                    float browY = eyeY + eyeR * 1.55f + HeadH * 0.02f * (L("brows.angle") - 0.5f);
                    float browThick = HeadH * (0.014f + 0.022f * L("face.eyebrowThickness"));
                    Box("face.brow_" + s, "face.eyebrow", PartKind.Face, "head", "material.brow_toon", new Vec3(side * eyeX, browY, eyeZ + eyeR * 0.1f),
                        new Vec3(eyeR * 1.25f * S("brows.length"), browThick, HeadH * 0.015f), new Vec3(0f, 0f, side * (L("brows.angle") - 0.5f) * -30f));
                    Ellipsoid("face.ear_" + s, "face.ear", PartKind.Face, "head", "material.skin_toon", new Vec3(side * (headW * 0.5f + HeadH * 0.01f * L("ears.protrusion")), headCy - HeadH * 0.02f, 0f),
                              new Vec3(HeadH * 0.035f * (1f + L("ears.protrusion") * 0.5f), HeadH * 0.09f * S("face.earSize"), HeadH * 0.055f * S("face.earSize")));
                }
                float noseS = S("face.noseSize");
                Ellipsoid("face.nose", "face.nose", PartKind.Face, "head", "material.skin_toon", new Vec3(0f, eyeY - HeadH * 0.13f, headD * 0.5f + HeadH * (0.005f + 0.02f * L("nose.bridgeHeight"))),
                          new Vec3(HeadH * 0.055f * S("face.noseWidth"), HeadH * 0.07f * noseS, HeadH * (0.045f + 0.03f * L("nose.bridgeHeight")) * noseS));
                float mouthY = eyeY - HeadH * (0.27f - 0.05f * (L("face.mouthHeight") - 0.5f));
                Box("face.mouth", "face.mouth", PartKind.Face, "head", "material.mouth_dark", new Vec3(0f, mouthY, headD * 0.5f - HeadH * 0.01f),
                    new Vec3(HeadH * 0.11f * S("face.mouthWidth"), HeadH * (0.012f + 0.02f * L("mouth.fullness")), HeadH * 0.012f), Vec3.Zero);

                // ---- hair and facial hair
                Hair(headCy, headW, headD);
                FacialHair(headCy, headW, headD, jaw, chin);

                // ---- arms
                float shoulderXY = ShoulderY - 0.045f * H;
                for (int side = -1; side <= 1; side += 2)
                {
                    string s = side < 0 ? "l" : "r";
                    Vec3 sh = new Vec3(side * ShoulderHalf, shoulderXY, 0f);
                    Vec3 dirUpper = new Vec3(side * 0.34f, -0.94f, 0.02f);
                    Vec3 el = Add(sh, Mul(dirUpper, UpperArm));
                    Vec3 dirFore = new Vec3(side * 0.16f, -0.95f, 0.26f);
                    Vec3 wr = Add(el, Mul(dirFore, ForeArm));
                    float rU = 0.034f * H * limb, rF = 0.026f * H * limb;
                    Bone("shoulder_" + s, "chest", sh); Bone("elbow_" + s, "shoulder_" + s, el); Bone("wrist_" + s, "elbow_" + s, wr);
                    Ellipsoid("body.shoulder_" + s, "body", PartKind.Body, "shoulder_" + s, "material.shirt_cloth", sh, new Vec3(rU * 1.25f, rU * 1.25f, rU * 1.25f));
                    Segment("body.upperarm_" + s, "body", PartKind.Body, "shoulder_" + s, "material.shirt_cloth", sh, Add(sh, Mul(dirUpper, UpperArm * 0.62f)), rU * 1.1f, rU * 0.95f);
                    Segment("body.upperarm_skin_" + s, "body", PartKind.Body, "shoulder_" + s, "material.skin_toon", Add(sh, Mul(dirUpper, UpperArm * 0.62f)), el, rU * 0.95f, rU * 0.8f);
                    Segment("body.forearm_" + s, "body", PartKind.Body, "elbow_" + s, "material.skin_toon", el, wr, rF * 1.05f, rF * 0.8f);
                    float hand = 0.052f * H * S("body.handScale");
                    bool gloves = rc.Parts.TryGetValue("kit.gloves", out string gl) && gl != "none";
                    Ellipsoid("body.hand_" + s, gloves ? "kit.gloves" : "body", gloves ? PartKind.Glove : PartKind.Body, "wrist_" + s, gloves ? "material.gloves_grip" : "material.skin_toon",
                              Add(wr, Mul(dirFore, hand * 0.55f)), new Vec3(hand * 0.55f, hand * 0.8f, hand * 0.38f));
                }

                // ---- legs and boots
                float thighR = 0.052f * H * S("body.thighThickness") * limb, calfR = 0.037f * H * S("body.calfThickness") * limb;
                float footL = 0.15f * H * S("body.footScale"), footW = 0.04f * H * S("body.footScale"), footH = 0.04f * H;
                for (int side = -1; side <= 1; side += 2)
                {
                    string s = side < 0 ? "l" : "r";
                    Vec3 hip = new Vec3(side * pelvisHalf * 0.55f, HipY, 0f);
                    Vec3 knee = new Vec3(side * (pelvisHalf * 0.55f + 0.01f * H), HipY - thighLen, 0.012f * H);
                    Vec3 ankle = new Vec3(side * (pelvisHalf * 0.55f + 0.012f * H), AnkleY, 0f);
                    Bone("hip_" + s, "pelvis", hip); Bone("knee_" + s, "hip_" + s, knee); Bone("ankle_" + s, "knee_" + s, ankle);
                    Vec3 mid = Vec3.Lerp(hip, knee, 0.5f);
                    Segment("body.thigh_short_" + s, "body", PartKind.Body, "hip_" + s, "material.shorts_cloth", hip, mid, thighR * 1.05f, thighR * 0.95f);
                    Segment("body.thigh_skin_" + s, "body", PartKind.Body, "hip_" + s, "material.skin_toon", mid, knee, thighR * 0.95f, thighR * 0.72f);
                    Ellipsoid("body.knee_" + s, "body", PartKind.Body, "knee_" + s, "material.skin_toon", knee, new Vec3(thighR * 0.72f, thighR * 0.7f, thighR * 0.72f));
                    Segment("body.shin_sock_" + s, "body", PartKind.Body, "knee_" + s, "material.socks_cloth", knee, Vec3.Lerp(knee, ankle, 0.92f), calfR * 1.08f, calfR * 0.78f);
                    Ellipsoid("boot.boot_" + s, "kit.boots", PartKind.Boot, "ankle_" + s, "material.boots_gloss", new Vec3(ankle.X, footH * 0.55f, footL * 0.22f), new Vec3(footW, footH * 0.6f, footL * 0.5f));
                    Ellipsoid("boot.ankle_" + s, "kit.boots", PartKind.Boot, "ankle_" + s, "material.boots_gloss", new Vec3(ankle.X, AnkleY + 0.012f * H, -footL * 0.02f), new Vec3(footW * 0.8f, 0.03f * H, footW * 0.9f));
                }
            }

            private void Hair(float headCy, float headW, float headD)
            {
                float length = L("hair.length"), volume = S("hair.volume");
                // no style chosen: the hair length says which family of styles it is
                if (!rc.Parts.TryGetValue("hair.style", out string style))
                    style = length < 0.15f ? "buzz_02" : length < 0.45f ? "short_crop_01" : length < 0.7f ? "medium_wavy_03" : "long_tied_05";
                float hairline = 0.05f * (L("hair.hairline") - 0.5f);
                Vec3 radii = new Vec3(headW * 0.5f, HeadH * 0.5f, headD * 0.5f);
                string mat = "material.hair_toon";
                Vec3 center = new Vec3(0f, headCy + HeadH * 0.02f, -headD * 0.02f);
                switch (style)
                {
                    case "buzz_02":
                        Ellipsoid("hair.cap", "hair.style", PartKind.Hair, "head", mat, center, Mul(radii, 1.015f), -0.05f + hairline);
                        break;
                    case "fade_06":
                        Ellipsoid("hair.cap", "hair.style", PartKind.Hair, "head", mat, center, Mul(radii, 1.04f), 0.02f + hairline);
                        break;
                    case "afro_08":
                        Ellipsoid("hair.afro", "hair.style", PartKind.Hair, "head", mat, new Vec3(0f, headCy + HeadH * 0.02f, -headD * 0.05f), new Vec3(radii.X * 1.5f * volume, radii.Y * 1.35f * volume, radii.Z * 1.45f * volume), -0.72f);
                        break;
                    case "medium_wavy_03":
                        Ellipsoid("hair.cap", "hair.style", PartKind.Hair, "head", mat, center, new Vec3(radii.X * 1.09f * volume, radii.Y * 1.08f, radii.Z * 1.1f * volume), -0.2f + hairline);
                        Ellipsoid("hair.back", "hair.style", PartKind.Hair, "head", mat, new Vec3(0f, headCy - HeadH * 0.1f, -headD * 0.30f), new Vec3(radii.X * 0.95f * volume, HeadH * (0.25f + 0.2f * length), radii.Z * 0.5f));
                        break;
                    case "long_tied_05":
                        Ellipsoid("hair.cap", "hair.style", PartKind.Hair, "head", mat, center, Mul(radii, 1.07f), -0.1f + hairline);
                        Segment("hair.tail", "hair.style", PartKind.Hair, "head", mat, new Vec3(0f, headCy + HeadH * 0.05f, -headD * 0.5f), new Vec3(0f, headCy - HeadH * (0.5f + 0.9f * length), -headD * 0.66f), HeadH * 0.07f * volume, HeadH * 0.045f);
                        break;
                    case "short_curly_07":
                        Ellipsoid("hair.cap", "hair.style", PartKind.Hair, "head", mat, center, new Vec3(radii.X * 1.12f * volume, radii.Y * 1.1f, radii.Z * 1.12f * volume), -0.15f + hairline);
                        break;
                    case "short_textured_04":
                        Ellipsoid("hair.cap", "hair.style", PartKind.Hair, "head", mat, center, new Vec3(radii.X * 1.07f * volume, radii.Y * 1.07f, radii.Z * 1.07f * volume), -0.05f + hairline);
                        break;
                    default:
                        Ellipsoid("hair.cap", "hair.style", PartKind.Hair, "head", mat, center, new Vec3(radii.X * 1.06f * volume, radii.Y * 1.06f, radii.Z * 1.06f * volume), -0.02f + hairline);
                        break;
                }
            }

            private void FacialHair(float headCy, float headW, float headD, float jaw, float chin)
            {
                if (!rc.Parts.TryGetValue("face.beard", out string beard) || beard == "none") return;
                float density = L("facialHair.density");
                if (density < 0.01f) density = beard == "stubble" ? 0.35f : 0.8f;
                Vec3 c = new Vec3(0f, headCy - HeadH * 0.3f, headD * 0.04f);
                Vec3 r = new Vec3(headW * (0.355f + 0.1f * jaw), HeadH * (0.21f + 0.03f * chin), headD * (0.375f + 0.04f * chin));
                float grow = beard == "stubble" ? 1.02f : 1.07f;
                Ellipsoid("facial.beard", "face.beard", PartKind.FacialHair, "head", "material.hair_toon", c, Mul(r, grow), -1f, beard == "stubble" ? "stubble" : "");
            }
        }
    }
}
