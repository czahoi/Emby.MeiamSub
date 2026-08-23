using System;
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

            var sample = DecodeSample(data);
            if (mediaType?.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                mediaType?.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidDataException("Thunder returned an error document instead of a subtitle.");
            }

            var valid = format == "srt"
                ? sample.IndexOf("-->", StringComparison.Ordinal) >= 0
                : sample.IndexOf("[Script Info]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  sample.IndexOf("Dialogue:", StringComparison.OrdinalIgnoreCase) >= 0;

            if (valid)
            {
                return;
            }

            if (sample.StartsWith("<", StringComparison.Ordinal) ||
                sample.StartsWith("{", StringComparison.Ordinal) ||
                sample.StartsWith("[", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Thunder returned an error document instead of a subtitle.");
            }

            throw new InvalidDataException($"Downloaded content is not a valid {format} subtitle.");
        }

        private static string DecodeSample(byte[] data)
        {
            var length = Math.Min(data.Length, 4096);
            var encoding = DetectEncoding(data);
            return encoding.GetString(data, 0, length)
                .TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        }

        private static Encoding DetectEncoding(byte[] data)
        {
            if (data.Length >= 2)
            {
                if (data[0] == 0xFF && data[1] == 0xFE)
                {
                    return Encoding.Unicode;
                }

                if (data[0] == 0xFE && data[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode;
                }
            }

            var length = Math.Min(data.Length, 512);
            var pairs = length / 2;
            if (pairs >= 4)
            {
                // Some Thunder SRT responses are UTF-16 without a BOM. Detect the
                // characteristic null-byte pattern before falling back to UTF-8.
                var littleEndianNulls = 0;
                var bigEndianNulls = 0;
                for (var i = 0; i < pairs * 2; i += 2)
                {
                    if (data[i + 1] == 0)
                    {
                        littleEndianNulls++;
                    }

                    if (data[i] == 0)
                    {
                        bigEndianNulls++;
                    }
                }

                var threshold = Math.Max(2, pairs / 4);
                if (littleEndianNulls >= threshold && littleEndianNulls > bigEndianNulls * 2)
                {
                    return Encoding.Unicode;
                }

                if (bigEndianNulls >= threshold && bigEndianNulls > littleEndianNulls * 2)
                {
                    return Encoding.BigEndianUnicode;
                }
            }

            return new UTF8Encoding(false, false);
        }
    }
}
