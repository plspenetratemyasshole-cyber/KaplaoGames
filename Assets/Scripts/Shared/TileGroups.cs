using System.Collections.Generic;
using System.Text;

namespace KaplaoGames.Shared
{
    public static class TileGroups
    {
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            int[] numbers = ParseNumbers(raw);
            return Join(numbers);
        }

        public static int[] ParseNumbers(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return System.Array.Empty<int>();

            string[] parts = csv.Split(',');
            var numbers = new List<int>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseNumber(parts[i], out int value))
                    continue;

                if (!numbers.Contains(value))
                    numbers.Add(value);
            }

            numbers.Sort();
            return numbers.ToArray();
        }

        public static string Join(IEnumerable<int> numbers)
        {
            if (numbers == null)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (int number in numbers)
            {
                if (number < 0)
                    continue;

                if (builder.Length > 0)
                    builder.Append(',');

                builder.Append(number);
            }

            return builder.ToString();
        }

        public static string MergeUnion(string left, string right)
        {
            var merged = new HashSet<int>();
            AddNumbers(merged, ParseNumbers(left));
            AddNumbers(merged, ParseNumbers(right));
            return Join(merged);
        }

        public static void AddNumbers(HashSet<int> target, IEnumerable<int> numbers)
        {
            foreach (int number in numbers)
                target.Add(number);
        }

        public static bool TryParseNumber(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return int.TryParse(raw.Trim(), out value) && value >= 0;
        }
    }
}
