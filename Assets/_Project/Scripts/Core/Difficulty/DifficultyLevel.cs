namespace FS27.Core
{
    /// <summary>
    /// The AI difficulty levels, ordered from easiest to hardest. A level only selects a
    /// <see cref="DifficultyDefinition"/>; it never carries values itself.
    /// Spanish display names: Novato, Amateur, Profesional, Experto, Élite.
    /// </summary>
    public enum DifficultyLevel
    {
        Novice = 0,
        Amateur = 1,
        Professional = 2,
        Expert = 3,
        Elite = 4
    }
}
