using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>One step of a multi-turn authoring conversation, with everything needed to explain and undo it.</summary>
    public sealed class AuthoringTurn
    {
        public int Index;
        public string Text = "";
        public SemanticProgram Program;
        public CompileResult Compiled;
        public PatchResult Applied;
        public AuthoringDraft Before;
        public AuthoringDraft After;
        /// <summary>What actually changed in the draft (a patch made by comparing before and after).</summary>
        public CharacterSpecificationPatch Changes = new CharacterSpecificationPatch();
        public bool WasUndo;
        public InterpretationReport Report;
        public readonly List<string> Notes = new List<string>();
    }

    /// <summary>
    /// A conversation with the Creator Engine about ONE character at a time: "Crea un extremo", "hazlo más rápido", "más pequeño", "cámbiale el
    /// pelo"... Each turn is interpreted, compiled to a patch and applied incrementally, so what was not mentioned is kept exactly. It
    /// remembers the previous character ("como el anterior"), and every turn can be undone. Deterministic: same session seed and same texts,
    /// same characters.
    /// </summary>
    public sealed class AuthoringSession
    {
        private readonly CreatorCatalogs catalogs;
        private readonly ISemanticInterpreter interpreter;
        private readonly SemanticCompiler compiler;
        private readonly uint sessionSeed;
        private readonly List<AuthoringTurn> history = new List<AuthoringTurn>();
        private readonly Stack<AuthoringTurn> undoStack = new Stack<AuthoringTurn>();
        private int characterCounter;

        public AuthoringDraft Current { get; private set; }
        /// <summary>The character that was current before the last "create" (what "como el anterior" refers to).</summary>
        public AuthoringDraft Previous { get; private set; }
        public IReadOnlyList<AuthoringTurn> History => history;
        public string CharacterIdPrefix = "character";
        public string StyleId = DefaultStyles.CartoonSports;

        public AuthoringSession(CreatorCatalogs catalogs, ISemanticInterpreter interpreter, SemanticCompiler compiler, uint sessionSeed = 1)
        {
            this.catalogs = catalogs;
            this.interpreter = interpreter;
            this.compiler = compiler;
            this.sessionSeed = sessionSeed;
        }

        public static AuthoringSession CreateDefault(uint sessionSeed = 1)
        {
            CreatorCatalogs cat = CreatorCatalogs.CreateDefault();
            ConceptCatalog concepts = DefaultConcepts.Create();
            return new AuthoringSession(cat, new SemanticParser(concepts, LanguagePackEs.Create(), LanguagePackEn.Create()), new SemanticCompiler(cat, concepts), sessionSeed);
        }

        /// <summary>Starts a new character (the current one becomes the previous one).</summary>
        public AuthoringDraft StartNew()
        {
            if (Current != null) Previous = Current;
            characterCounter++;
            Current = new AuthoringDraft(catalogs.NewSpecification(CharacterIdPrefix + "-" + characterCounter.ToString("000", System.Globalization.CultureInfo.InvariantCulture), StyleId));
            Current.Spec.Authoring = new AuthoringData { Source = "prompt", Generator = interpreter.Name };
            return Current;
        }

        /// <summary>Loads an existing character to keep editing it.</summary>
        public void Load(AuthoringDraft draft)
        {
            if (Current != null) Previous = Current;
            Current = draft.Clone();
            undoStack.Clear();
        }

        public AuthoringTurn Submit(string text)
        {
            return SubmitProgram(text, interpreter.Interpret(text));
        }

        /// <summary>Same as <see cref="Submit"/>, with the meaning already worked out (by a provider, an editor or a test).</summary>
        public AuthoringTurn SubmitProgram(string text, SemanticProgram program)
        {
            var turn = new AuthoringTurn { Index = history.Count + 1, Text = text, Program = program };
            uint seed = StableHash.Combine(sessionSeed, (uint)turn.Index);

            AuthoringDraft target = Current;
            CompileResult compiled = compiler.Compile(program, target, seed);
            turn.Compiled = compiled;
            turn.Report = compiled.Report;

            if (compiled.UndoRequested && !compiled.CreateRequested && program.Commands.Count == 1)
            {
                turn.Before = Current?.Clone();
                turn.WasUndo = true;
                if (!Undo()) turn.Notes.Add("Nothing to undo.");
                turn.After = Current?.Clone();
                history.Add(turn);
                return turn;
            }

            if (compiled.CreateRequested || Current == null)
            {
                AuthoringDraft previousBefore = Current;
                if (compiled.BaseReference == EntityReference.Previous && Previous != null)
                {
                    Current = Previous.Clone();
                    Previous = previousBefore ?? Previous;
                    characterCounter++;
                    Current.Spec.CharacterId = CharacterIdPrefix + "-" + characterCounter.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
                    turn.Notes.Add("Started from the previous character.");
                }
                else
                {
                    if (compiled.BaseReference == EntityReference.Previous) turn.Notes.Add("There is no previous character; started a new one.");
                    StartNew();
                }
                if (!compiled.CreateRequested) turn.Notes.Add("There was no character yet; started a new one.");
                undoStack.Clear();
                compiled = compiler.Compile(program, Current, seed);
                turn.Compiled = compiled;
                turn.Report = compiled.Report;
            }

            turn.Before = Current.Clone();
            PatchResult applied = PatchApplier.Apply(Current, compiled.Patch, catalogs);
            turn.Applied = applied;
            Current = applied.Draft;
            if (Current.Spec.Authoring == null) Current.Spec.Authoring = new AuthoringData { Source = "prompt", Generator = interpreter.Name };
            Current.Spec.Authoring.Prompt = string.IsNullOrEmpty(Current.Spec.Authoring.Prompt) ? text : Current.Spec.Authoring.Prompt + " | " + text;
            Current.Spec.Authoring.Generator = interpreter.Name;
            turn.After = Current.Clone();
            turn.Changes = DraftDiff.Create(turn.Before, turn.After, catalogs);
            foreach (string s in applied.Skipped) turn.Notes.Add("Skipped: " + s);
            foreach (string s in applied.Blocked) turn.Notes.Add("Blocked: " + s);
            foreach (string s in applied.Clamped) turn.Notes.Add("Limited: " + s);
            history.Add(turn);
            if (!turn.Changes.IsEmpty) undoStack.Push(turn);
            return turn;
        }

        /// <summary>Undoes the last change that altered the character. False when there is nothing to undo.</summary>
        public bool Undo()
        {
            if (undoStack.Count == 0 || Current == null) return false;
            AuthoringTurn last = undoStack.Pop();
            PatchResult r = PatchApplier.Apply(Current, last.Applied.Inverse, catalogs);
            Current = r.Draft;
            return true;
        }
    }
}
