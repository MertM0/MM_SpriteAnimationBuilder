using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MM.SpriteAnimation
{
    internal static class SpriteFrameCollector
    {
        internal static IReadOnlyList<string> Collect(string directory, IEnumerable<string> files, string stem, string label, ICollection<string> problems, ICollection<string> warnings)
        {
            SortedDictionary<int, string> byNumber = ReadNumbered(files, stem, label, problems, warnings);
            if (byNumber.Count == 0)
            {
                problems.Add($"{label}: no '{stem}NN.png' frames in '{directory}'.");
                return Array.Empty<string>();
            }

            int expected = 1;
            foreach (int number in byNumber.Keys)
            {
                if (number != expected)
                {
                    problems.Add($"{label}: frames must run 01..{Format(byNumber.Count)} without gaps; found {Format(number)} where {Format(expected)} was expected.");
                    break;
                }
                expected++;
            }
            return byNumber.Values.ToList();
        }

        internal static string[] CollectOptional(IEnumerable<string> files, string stem, string label, int frameCount, string firstLabel, ICollection<string> problems, ICollection<string> warnings)
        {
            var frames = new string[frameCount];
            foreach (KeyValuePair<int, string> frame in ReadNumbered(files, stem, label, problems, warnings))
            {
                if (frame.Key < 1 || frame.Key > frames.Length)
                {
                    problems.Add($"{label}: frame {Format(frame.Key)} is outside the {frames.Length} frame(s) of {firstLabel}.");
                    continue;
                }
                frames[frame.Key - 1] = frame.Value;
            }
            return frames;
        }

        private static SortedDictionary<int, string> ReadNumbered(IEnumerable<string> files, string stem, string label, ICollection<string> problems, ICollection<string> warnings)
        {
            var pattern = new Regex($"^{Regex.Escape(stem)}([0-9]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var byNumber = new SortedDictionary<int, string>();
            foreach (string file in files)
            {
                string path = file.Replace('\\', '/');
                string fileName = Path.GetFileNameWithoutExtension(path);
                Match match = pattern.Match(fileName);
                if (!match.Success)
                {
                    problems.Add($"{label}: '{fileName}.png' does not match '{stem}NN'.");
                    continue;
                }
                if (!fileName.StartsWith(stem, StringComparison.Ordinal))
                {
                    warnings.Add($"{label}: '{fileName}' should be spelled '{stem}{match.Groups[1].Value}'.");
                }

                if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                {
                    problems.Add($"{label}: '{fileName}.png' has a frame number that is too large.");
                    continue;
                }
                if (byNumber.ContainsKey(number))
                {
                    problems.Add($"{label}: frame {Format(number)} exists twice.");
                    continue;
                }
                byNumber.Add(number, path);
            }
            return byNumber;
        }

        private static string Format(int number) => number.ToString("00", CultureInfo.InvariantCulture);
    }
}
