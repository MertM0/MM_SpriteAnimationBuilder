using System;
using System.Collections.Generic;
using System.Linq;

namespace MM.SpriteAnimation
{
    internal static class ProfileOrder
    {
        internal static List<T> Sort<T>(IEnumerable<T> items, Func<T, string> guid, Func<T, string> name, IReadOnlyList<string> order)
        {
            List<T> remaining = items.ToList();
            var sorted = new List<T>(remaining.Count);
            foreach (string known in order)
            {
                int index = remaining.FindIndex(item => string.Equals(guid(item), known, StringComparison.Ordinal));
                if (index < 0) continue;
                sorted.Add(remaining[index]);
                remaining.RemoveAt(index);
            }
            sorted.AddRange(remaining.OrderBy(name, StringComparer.OrdinalIgnoreCase));
            return sorted;
        }

        internal static List<string> Move(IReadOnlyList<string> orderedGuids, int from, int to)
        {
            var moved = new List<string>(orderedGuids);
            string item = moved[from];
            moved.RemoveAt(from);
            moved.Insert(to, item);
            return moved;
        }
    }
}
