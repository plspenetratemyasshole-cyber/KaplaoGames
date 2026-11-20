using System;

namespace KaplaoGames.LevelScript
{
    public struct LevelScriptDiagnostic
    {
        public bool HasError;
        public int Line;
        public int Column;
        public int StartIndex;
        public int EndIndex;
        public string Message;

        public static LevelScriptDiagnostic None => default;

        public static LevelScriptDiagnostic FromException(LevelScriptParseException ex, string source)
        {
            int start = ex.StartIndex >= 0 ? ex.StartIndex : 0;
            int end = FindErrorEnd(source, start);
            return new LevelScriptDiagnostic
            {
                HasError = true,
                Line = ex.Line > 0 ? ex.Line : 1,
                Column = ex.Column > 0 ? ex.Column : 1,
                StartIndex = start,
                EndIndex = end,
                Message = ex.Message
            };
        }

        static int FindErrorEnd(string source, int start)
        {
            if (string.IsNullOrEmpty(source))
                return 0;

            start = Math.Clamp(start, 0, source.Length - 1);
            int end = start;

            if (char.IsLetter(source[start]) || source[start] == '_')
            {
                while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_'))
                    end++;
                return Math.Max(end, start + 1);
            }

            if (source[start] == '"')
            {
                end++;
                while (end < source.Length && source[end] != '"')
                {
                    if (source[end] == '\\' && end + 1 < source.Length)
                        end += 2;
                    else
                        end++;
                }

                if (end < source.Length)
                    end++;

                return end;
            }

            while (end < source.Length && !char.IsWhiteSpace(source[end]) && source[end] != ';' && source[end] != ')')
                end++;

            if (end <= start)
                end = Math.Min(start + 1, source.Length);

            return end;
        }
    }
}
