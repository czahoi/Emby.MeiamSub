using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MeiamSubtitles.Shared
{
    internal static class SubtitleContentValidator
    {
        public static void Validate(byte[] data, string format, string mediaType)
        {
            if (data == null || data.Length < 8)
            {
                throw new InvalidDataException("Subtitle response is empty.");
            }

            if ((data[0] == (byte)'P' && data[1] == (byte)'K') ||
                (data.Length >= 7 && Encoding.ASCII.GetString(data, 0, 7) == "Rar!\u001a\u0007"))
            {
                throw new InvalidDataException("Compressed subtitle responses are not supported by Thunder.");
            }

            if (mediaType?.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                mediaType?.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidDataException("Thunder returned an error document instead of a subtitle.");
            }

            var errorDocument = false;
            foreach (var sample in DecodeSamples(data))
            {
                if (sample.StartsWith("<", StringComparison.Ordinal) ||
                    sample.StartsWith("{", StringComparison.Ordinal))
                {
                    errorDocument = true;
                    continue;
                }

                if (IsValidSubtitle(sample, format))
                {
                    return;
                }

                if (sample.StartsWith("[", StringComparison.Ordinal))
                {
                    errorDocument = true;
                }
            }

            if (errorDocument)
            {
                throw new InvalidDataException("Thunder returned an error document instead of a subtitle.");
            }

            throw new InvalidDataException($"Downloaded content is not a valid {format} subtitle.");
        }

        private static bool IsValidSubtitle(string sample, string format)
        {
            return format == "srt"
                ? sample.IndexOf("-->", StringComparison.Ordinal) >= 0
                : sample.IndexOf("[Script Info]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  sample.IndexOf("Dialogue:", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IEnumerable<string> DecodeSamples(byte[] data)
        {
            var length = Math.Min(data.Length, 4096);

            if (data.Length >= 2)
            {
                if (data[0] == 0xFF && data[1] == 0xFE)
                {
                    yield return DecodeSample(data, length, Encoding.Unicode);
                    yield break;
                }

                if (data[0] == 0xFE && data[1] == 0xFF)
                {
                    yield return DecodeSample(data, length, Encoding.BigEndianUnicode);
                    yield break;
                }
            }

            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                yield return DecodeSample(data, length, Encoding.UTF8);
                yield break;
            }

            // Thunder may return UTF-16 subtitles without a BOM. Try the common
            // single-byte/UTF-8 representation and both UTF-16 byte orders, then
            // accept only a candidate that contains the expected subtitle syntax.
            yield return DecodeSample(data, length, new UTF8Encoding(false, false));
            yield return DecodeSample(data, length, Encoding.Unicode);
            yield return DecodeSample(data, length, Encoding.BigEndianUnicode);
        }

        private static string DecodeSample(byte[] data, int length, Encoding encoding)
        {
            return encoding.GetString(data, 0, length)
                .TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        }
    }
}
