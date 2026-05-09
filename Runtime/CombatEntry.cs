namespace CombatLog.Runtime
{
    public enum EntryKind
    {
        Hit,
        Miss,
        Damage,
        Death,
        Info,
    }

    public sealed class CombatEntry
    {
        public EntryKind Kind { get; }
        public string Text { get; set; }

        // When non-null, a follow-up entry with the same key will be appended to this entry
        // text instead of creating a new line.
        public object? MergeKey { get; }

        public CombatEntry(EntryKind kind, string text, object? mergeKey = null)
        {
            Kind = kind;
            Text = text;
            MergeKey = mergeKey;
        }
    }
}
