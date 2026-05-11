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

        // When non-null, the entry belongs to a group of related shots.
        // Within a group, BurstIndex orders entries by their position to reflect the shot sequence.
        public object? BurstKey { get; }
        public int BurstIndex { get; }

        public CombatEntry(
            EntryKind kind,
            string text,
            object? mergeKey = null,
            object? burstKey = null,
            int burstIndex = 0
        )
        {
            Kind = kind;
            Text = text;
            MergeKey = mergeKey;
            BurstKey = burstKey;
            BurstIndex = burstIndex;
        }
    }
}
