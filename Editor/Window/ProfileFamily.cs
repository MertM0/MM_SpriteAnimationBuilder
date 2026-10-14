using System;
using System.Collections.Generic;
using System.Linq;

namespace MM.SpriteAnimation
{
    internal static class ProfileFamily
    {
        internal static List<List<T>> Group<T>(IReadOnlyList<T> ordered, Func<T, T> baseOf, Func<T, string> name) where T : class
        {
            var families = new List<List<T>>();
            var byBase = new Dictionary<T, List<T>>();
            foreach (T item in ordered)
            {
                if (IsMember(item)) continue;
                var family = new List<T> { item };
                families.Add(family);
                byBase[item] = family;
            }
            foreach (T item in ordered)
            {
                if (IsMember(item)) byBase[baseOf(item)].Add(item);
            }
            foreach (List<T> family in families)
            {
                List<T> variants = family.Skip(1).OrderBy(name, StringComparer.OrdinalIgnoreCase).ToList();
                family.RemoveRange(1, family.Count - 1);
                family.AddRange(variants);
            }
            return families;

            bool IsMember(T item)
            {
                T owner = baseOf(item);
                return owner != null && owner != item && baseOf(owner) == null && ordered.Contains(owner);
            }
        }
    }
}
