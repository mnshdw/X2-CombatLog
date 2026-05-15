using System.Collections.Generic;

namespace CombatLog.Runtime
{
    public static class CombatLogFeed
    {
        private const int MaxBufferedEntries = 1024;

        private static readonly Queue<CombatEntry> Pending = new();

        public static bool InGroundCombat { get; private set; }

        public static void Publish(CombatEntry entry)
        {
            Pending.Enqueue(entry);
            while (Pending.Count > MaxBufferedEntries)
                Pending.Dequeue();
        }

        public static bool TryDequeue(out CombatEntry entry)
        {
            if (Pending.Count == 0)
            {
                entry = null!;
                return false;
            }
            entry = Pending.Dequeue();
            return true;
        }

        public static void OnGroundCombatStarted()
        {
            // Drop any entries left over from a previous combat so the new mission starts empty.
            Pending.Clear();
            InGroundCombat = true;
        }

        public static void OnGroundCombatEnded()
        {
            InGroundCombat = false;
        }

        public static void Reset()
        {
            Pending.Clear();
            InGroundCombat = false;
        }
    }
}
