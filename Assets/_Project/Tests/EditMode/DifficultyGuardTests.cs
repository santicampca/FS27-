using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Cross-cutting rules of the difficulty/AI infrastructure: skill is never faked through attributes, the
    /// human assists and the team style stay separate, no real-world names, no sprint button, no cheating hooks.
    /// </summary>
    public class DifficultyGuardTests
    {
        private static readonly GoalkeeperTuning KeeperTuning = new GoalkeeperTuning();
        private static DifficultyDefinition L(DifficultyLevel l) { return DefaultDifficulties.Create(l); }
        private static readonly DifficultyLevel[] Levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));

        // Every type that belongs to the difficulty / AI infrastructure.
        private static readonly Type[] AiTypes =
        {
            typeof(DifficultyLevel), typeof(DifficultyRules), typeof(DifficultyDefinition), typeof(DecisionParameters), typeof(ReactionParameters),
            typeof(PositioningParameters), typeof(AnticipationParameters), typeof(PressureParameters), typeof(ExecutionParameters),
            typeof(CoordinationParameters), typeof(GoalkeeperParameters), typeof(DefaultDifficulties), typeof(DifficultyLibrary),
            typeof(DifficultyMetric), typeof(DifficultyMetrics), typeof(DifficultyValidator), typeof(AiDataValidationResult), typeof(AiDataIssue),
            typeof(ReactionModel), typeof(ObservationDelayBuffer<>), typeof(ReactionGate), typeof(SeededRandom), typeof(IRandomSource),
            typeof(AiErrorModel), typeof(ErrorTuning), typeof(ErrorContext), typeof(ErrorAssessment), typeof(ErrorOutcome), typeof(AiErrorKind),
            typeof(PositioningModel), typeof(PositioningTuning), typeof(IIdealPositionProvider), typeof(AnticipationModel), typeof(AnticipationTuning),
            typeof(PerceptionContext), typeof(PressureModel), typeof(PressureSituation), typeof(PressureDecision), typeof(PressureAction),
            typeof(TeamStyleDefinition), typeof(DefaultTeamStyles), typeof(TeamStyleLibrary), typeof(TeamStyleValidator), typeof(TeamAiProfile),
            typeof(AiProfileResolver), typeof(ResolvedTeamAi), typeof(PlayerPlayingProfile), typeof(PlayingProfileDefaults), typeof(PlayingProfileCatalog),
            typeof(PlayingProfileValidator), typeof(ZoneFitnessTuning), typeof(RoleExecutionModel), typeof(PitchZone), typeof(PlayerArchetype),
            typeof(AiSkillProfile), typeof(AiSkillResolver), typeof(GoalkeeperTuning), typeof(GoalkeeperSkill), typeof(GoalkeeperSkillModel),
            typeof(AssistLevel), typeof(PlayerAssistSettings), typeof(PlayerAssistOverrides), typeof(AssistTuning), typeof(AssistParameters), typeof(AssistResolver)
        };

        private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // ================= Difficulty never rewrites attributes =================

        [Test]
        public void NoPublicMember_TakesAttributesByRefOrOut_OrReturnsThem()
        {
            foreach (Type t in AiTypes)
                foreach (MethodBase m in t.GetMethods(All).Cast<MethodBase>().Concat(t.GetConstructors(All)))
                {
                    foreach (ParameterInfo p in m.GetParameters())
                    {
                        Type pt = p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType;
                        if (pt != typeof(PlayerAttributes) && pt != typeof(PlayerRuntimeState) && pt != typeof(PlayerStats) && pt != typeof(PlayerDefinition)) continue;
                        bool writable = p.ParameterType.IsByRef && !p.IsIn;     // `ref` or `out`
                        Assert.IsFalse(writable, t.Name + "." + m.Name + "(" + p.Name + ") may modify the player");
                    }
                    if (m is MethodInfo mi)
                        Assert.IsFalse(mi.ReturnType == typeof(PlayerAttributes) || mi.ReturnType == typeof(PlayerStats),
                            t.Name + "." + m.Name + " returns player attributes/stats");
                }
        }

        [Test]
        public void NoDifficultyOrAiType_StoresAPlayersAttributesOrStats()
        {
            foreach (Type t in AiTypes)
                foreach (FieldInfo f in t.GetFields(All))
                    Assert.IsFalse(f.FieldType == typeof(PlayerAttributes) || f.FieldType == typeof(PlayerStats) || f.FieldType == typeof(PlayerDefinition),
                        t.Name + "." + f.Name);
        }

        [Test]
        public void RunningTheWholeAiPipelineOnEveryDifficulty_LeavesAttributesAndPhysicalStatsExactlyAsTheyWere()
        {
            // "A player rated 87 stays 87 on every difficulty": attributes are the only source of a rating, and the pipeline never writes them.
            var a = new PlayerAttributes { Speed = 82, Acceleration = 80, Stamina = 76, BallControl = 88, Passing = 75, Shooting = 80, Defense = 60, Strength = 70, Reaction = 85 };
            var original = a;
            var tuning = new MovementTuning();
            PlayerStats physicalBefore = PlayerStats.Resolve(a, tuning);
            int ratingBefore = a.Speed + a.Acceleration + a.Stamina + a.BallControl + a.Passing + a.Shooting + a.Defense + a.Strength + a.Reaction;

            foreach (var l in Levels)
            {
                var d = L(l);
                AiSkillResolver.Resolve(d, a, 0.7f, 0.2f);
                ReactionModel.ReactionSeconds(d.Reaction, a, 0.9f);
                foreach (AiErrorKind k in Enum.GetValues(typeof(AiErrorKind)))
                    AiErrorModel.Resolve(AiErrorModel.Assess(k, d, a, ErrorContext.Calm, new ErrorTuning()), new SeededRandom(3));
                PositioningModel.ApplyError(Vec2.Zero, d.Positioning, a, 1f, new Vec2(1f, 1f), new PositioningTuning());
                AnticipationModel.Horizon(new PerceptionContext { EventPosition = new Vec2(0f, 5f) }, d.Anticipation, a, new AnticipationTuning());
                GoalkeeperSkillModel.Resolve(d.Goalkeeper, a, KeeperTuning);
                AssistResolver.Resolve(d, null);
            }

            Assert.AreEqual(original, a, "attributes are unchanged");
            Assert.AreEqual(ratingBefore, a.Speed + a.Acceleration + a.Stamina + a.BallControl + a.Passing + a.Shooting + a.Defense + a.Strength + a.Reaction);
            PlayerStats after = PlayerStats.Resolve(a, tuning);
            Assert.AreEqual(physicalBefore.TopSpeed, after.TopSpeed);
            Assert.AreEqual(physicalBefore.Acceleration, after.Acceleration);
            Assert.AreEqual(physicalBefore.StaminaCapacity, after.StaminaCapacity);
        }

        [Test]
        public void PhysicalStats_HaveNoDifficultyInput_TheyAreAFunctionOfAttributesAndMovementTuningOnly()
        {
            MethodInfo resolve = typeof(PlayerStats).GetMethod(nameof(PlayerStats.Resolve));
            var parameterTypes = resolve.GetParameters().Select(p => p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType).ToArray();
            CollectionAssert.AreEqual(new[] { typeof(PlayerAttributes), typeof(MovementTuning) }, parameterTypes);
        }

        [Test]
        public void TheSamePlayer_KeepsItsLimits_OnEveryLevel_AndOnlyUsesThemBetter()
        {
            // Same attributes in every level: what changes is the quality of use, not the capability.
            var weakPasser = TestData.Attrs(70); weakPasser.Passing = 40;
            var strongPasser = TestData.Attrs(70); strongPasser.Passing = 90;
            foreach (var l in Levels)
            {
                var d = L(l);
                float weak = AiErrorModel.Assess(AiErrorKind.BadPassChoice, d, weakPasser, ErrorContext.Calm, new ErrorTuning()).Probability;
                float strong = AiErrorModel.Assess(AiErrorKind.BadPassChoice, d, strongPasser, ErrorContext.Calm, new ErrorTuning()).Probability;
                Assert.Greater(weak, strong, "at " + l + " the Passing attribute still matters");
            }
            // Elite helps a 40-Passing player choose better than Novice does, yet they stay worse than a 90-Passing player on the same level.
            float eliteWeak = AiErrorModel.Assess(AiErrorKind.BadPassChoice, L(DifficultyLevel.Elite), weakPasser, ErrorContext.Calm, new ErrorTuning()).Probability;
            float noviceWeak = AiErrorModel.Assess(AiErrorKind.BadPassChoice, L(DifficultyLevel.Novice), weakPasser, ErrorContext.Calm, new ErrorTuning()).Probability;
            Assert.Less(eliteWeak, noviceWeak);
        }

        // ================= No cheating hooks / no sprint button =================

        [Test]
        public void NoMemberName_SuggestsStatBoostsCheatingOrOmniscience()
        {
            string[] banned = { "boost", "bonus", "cheat", "rubber", "catchup", "handicap", "omniscien", "xray", "clairvoy", "magic" };
            foreach (Type t in AiTypes)
            {
                Assert.IsFalse(banned.Any(b => t.Name.ToLowerInvariant().Contains(b)), t.Name);
                foreach (MemberInfo m in t.GetMembers(All))
                    Assert.IsFalse(banned.Any(b => m.Name.ToLowerInvariant().Contains(b)), t.Name + "." + m.Name);
            }
        }

        [Test]
        public void NoDifficultyField_IsNamedAfterAPhysicalAttribute_OrAMultiplierOfOne()
        {
            string[] attributes = { "speed", "acceleration", "stamina", "ballcontrol", "passing", "shooting", "defense", "strength" };
            foreach (Type t in new[] { typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters), typeof(AnticipationParameters),
                                       typeof(PressureParameters), typeof(ExecutionParameters), typeof(CoordinationParameters), typeof(GoalkeeperParameters) })
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    string n = f.Name.ToLowerInvariant();
                    Assert.IsFalse(attributes.Any(a => n == a || n.StartsWith(a)), t.Name + "." + f.Name);
                }
        }

        [Test]
        public void NoSprintConceptExists_InTheDifficultyOrAiInfrastructure()
        {
            foreach (Type t in AiTypes)
            {
                Assert.IsFalse(t.Name.ToLowerInvariant().Contains("sprint"), t.Name);
                foreach (MemberInfo m in t.GetMembers(All))
                    Assert.IsFalse(m.Name.ToLowerInvariant().Contains("sprint"), t.Name + "." + m.Name);
                foreach (MethodInfo m in t.GetMethods(All))
                    foreach (ParameterInfo p in m.GetParameters())
                        Assert.IsFalse((p.Name ?? "").ToLowerInvariant().Contains("sprint"), t.Name + "." + m.Name + "(" + p.Name + ")");
            }
        }

        // ================= Assistance is separate from the AI =================

        private static bool Mentions(Type t, params Type[] others)
        {
            IEnumerable<Type> used = t.GetMethods(All).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Concat(new[] { m.ReturnType }))
                .Concat(t.GetFields(All).Select(f => f.FieldType))
                .Select(x => x.IsByRef ? x.GetElementType() : x);
            return used.Any(u => others.Contains(u));
        }

        [Test]
        public void AiModels_NeverMentionHumanAssistTypes()
        {
            Type[] assist = { typeof(PlayerAssistSettings), typeof(PlayerAssistOverrides), typeof(AssistTuning), typeof(AssistParameters), typeof(AssistLevel) };
            foreach (Type t in new[] { typeof(ReactionModel), typeof(AiErrorModel), typeof(PositioningModel), typeof(AnticipationModel), typeof(PressureModel),
                                       typeof(RoleExecutionModel), typeof(AiSkillResolver), typeof(GoalkeeperSkillModel), typeof(AiSkillProfile) })
                Assert.IsFalse(Mentions(t, assist), t.Name + " must not know about human assists");
        }

        [Test]
        public void AssistTypes_NeverMentionAiParameterGroups()
        {
            Type[] ai = { typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters), typeof(AnticipationParameters), typeof(PressureParameters),
                          typeof(ExecutionParameters), typeof(CoordinationParameters), typeof(GoalkeeperParameters), typeof(TeamStyleDefinition) };
            foreach (Type t in new[] { typeof(PlayerAssistSettings), typeof(PlayerAssistOverrides), typeof(AssistTuning), typeof(AssistResolver) })
                Assert.IsFalse(Mentions(t, ai), t.Name + " must not know about AI parameters");
        }

        // ================= Style is separate from difficulty =================

        [Test]
        public void StyleTypes_NeverMentionDifficultyParameterGroups_ExceptTheResolverThatJoinsThem()
        {
            Type[] groups = { typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters), typeof(AnticipationParameters), typeof(PressureParameters),
                              typeof(ExecutionParameters), typeof(CoordinationParameters), typeof(GoalkeeperParameters), typeof(DifficultyDefinition) };
            Assert.IsFalse(Mentions(typeof(TeamStyleDefinition), groups));
            Assert.IsFalse(Mentions(typeof(TeamStyleLibrary), groups));
            Assert.IsFalse(Mentions(typeof(TeamStyleValidator), groups));
            Assert.IsFalse(Mentions(typeof(DifficultyDefinition), typeof(TeamStyleDefinition)));
            Assert.IsFalse(Mentions(typeof(DifficultyLibrary), typeof(TeamStyleDefinition)));
        }

        // ================= Unity-friendly data =================

        [Test]
        public void ConfigurationTypes_AreSerializable_SoUnityAssetsCanWrapThem()
        {
            foreach (Type t in new[] { typeof(DifficultyDefinition), typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters),
                                       typeof(AnticipationParameters), typeof(PressureParameters), typeof(ExecutionParameters), typeof(CoordinationParameters),
                                       typeof(GoalkeeperParameters), typeof(PlayerAssistSettings), typeof(AssistTuning), typeof(ErrorTuning), typeof(PositioningTuning),
                                       typeof(AnticipationTuning), typeof(GoalkeeperTuning), typeof(ZoneFitnessTuning), typeof(TeamStyleDefinition), typeof(TeamAiProfile),
                                       typeof(PlayerPlayingProfile) })
                Assert.IsTrue(t.IsSerializable, t.Name);
        }

        // ================= Fictional universe: no real clubs, leagues, players or brands =================

        private static readonly string[] BannedTerms =
        {
            "real madrid", "barcelona", "manchester", "liverpool", "chelsea", "arsenal", "juventus", "bayern", "atletico", "paris saint", "tottenham",
            "premier league", "la liga", "laliga", "serie a", "bundesliga", "ligue 1", "champions league", "europa league", "copa libertadores",
            "uefa", "fifa", "ultimate team", "ea sports", "konami", "efootball", "pro evolution", "football manager", "fab 5",
            "messi", "ronaldo", "neymar", "mbappe", "haaland", "lewandowski", "modric", "benzema"
        };
        private static readonly Regex[] BannedWords =
        {
            new Regex(@"\bpsg\b", RegexOptions.IgnoreCase), new Regex(@"\bpes\b", RegexOptions.IgnoreCase), new Regex(@"\bfut\b", RegexOptions.IgnoreCase),
            new Regex(@"\bnba\b", RegexOptions.IgnoreCase)
        };

        internal static void AssertClean(string text, string where)
        {
            string lower = text.ToLowerInvariant();
            foreach (string term in BannedTerms)
                Assert.IsFalse(lower.Contains(term), "'" + term + "' found in " + where);
            foreach (Regex r in BannedWords)
                Assert.IsFalse(r.IsMatch(text), "'" + r + "' found in " + where);
        }

        [Test]
        public void DefaultData_ContainsNoRealWorldNames()
        {
            var texts = new List<string>();
            foreach (var d in DefaultDifficulties.CreateAll()) { texts.Add(d.Id); texts.Add(d.Name); }
            foreach (var s in TeamStyleLibrary.CreateDefault().All) { texts.Add(s.Id); texts.Add(s.Name); }
            foreach (var f in DefaultFormations.CreateAll()) { texts.Add(f.Id); texts.Add(f.Name); }
            texts.AddRange(Enum.GetNames(typeof(PlayerArchetype)));
            texts.AddRange(Enum.GetNames(typeof(PitchZone)));
            texts.AddRange(Enum.GetNames(typeof(DifficultyLevel)));
            texts.AddRange(Enum.GetNames(typeof(AiErrorKind)));
            AssertClean(string.Join("\n", texts), "default data");
        }

        [Test]
        public void EveryTypeAndMemberNameInCore_IsFreeOfRealWorldNames()
        {
            var names = new List<string>();
            foreach (Type t in typeof(PlayerDefinition).Assembly.GetTypes())
            {
                names.Add(t.Name);
                names.AddRange(t.GetMembers(All).Select(m => m.Name));
            }
            AssertClean(string.Join("\n", names), "Core type and member names");
        }

        internal static string FindCoreSourceDirectory()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, "Assets", "_Project", "Scripts", "Core");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            return null;
        }

        [Test]
        public void CoreSourceFiles_ContainNoRealWorldNames()
        {
            string core = FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            var files = Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 40, "expected the Core sources");
            foreach (string file in files)
                AssertClean(File.ReadAllText(file), Path.GetFileName(file));
        }
    }
}
