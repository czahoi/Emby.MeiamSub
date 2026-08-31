using System.IO;
using System.Text;
using MeiamSubtitles.Shared;
using Xunit;

namespace MeiamSubtitles.Tests;

public class SubtitleContentValidatorTests
{
    [Fact]
    public void AcceptsAssWithScriptInfoHeader()
    {
        var data = Encoding.UTF8.GetBytes("[Script Info]\r\nScriptType: v4.00+\r\n[Events]\r\nDialogue: 0,0:00:01.00,0:00:02.00,Default,,0,0,0,,Hello");

        SubtitleContentValidator.Validate(data, "ass", "application/octet-stream");
    }

    [Fact]
    public void AcceptsUtf16LittleEndianSrtWithoutBom()
    {
        var data = Encoding.Unicode.GetBytes("1\r\n00:00:01,000 --> 00:00:02,000\r\nHello\r\n");

        SubtitleContentValidator.Validate(data, "srt", "application/octet-stream");
    }

    [Fact]
    public void AcceptsUtf16LittleEndianSrtWithoutBomWithLongChineseCue()
    {
        var content = "1\r\n00:00:01,000 --> 00:00:20,000\r\n" + new string('中', 300) + "\r\n";
        var data = Encoding.Unicode.GetBytes(content);

        SubtitleContentValidator.Validate(data, "srt", "application/octet-stream");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AcceptsUtf16SrtInEitherByteOrder(bool bigEndian, bool withBom)
    {
        var encoding = new UnicodeEncoding(bigEndian, withBom);
        var content = "1\r\n00:00:01,000 --> 00:00:02,000\r\n你好\r\n";
        var body = encoding.GetBytes(content);
        var data = withBom ? encoding.GetPreamble().Concat(body).ToArray() : body;

        SubtitleContentValidator.Validate(data, "srt", "application/octet-stream");
    }

    [Theory]
    [InlineData("<html><body>temporary error</body></html>", "application/octet-stream")]
    [InlineData("{\"error\":\"temporary error\"}", "application/json")]
    [InlineData("[\"temporary error\"]", "application/octet-stream")]
    public void RejectsErrorDocuments(string content, string mediaType)
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            SubtitleContentValidator.Validate(Encoding.UTF8.GetBytes(content), "srt", mediaType));

        Assert.Contains("error document", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsCompressedSubtitleResponse()
    {
        var data = new byte[] { (byte)'P', (byte)'K', 3, 4, 5, 6, 7, 8 };

        var exception = Assert.Throws<InvalidDataException>(() =>
            SubtitleContentValidator.Validate(data, "srt", "application/octet-stream"));

        Assert.Contains("Compressed", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<html><!-- service unavailable --><body>Error</body></html>", "srt")]
    [InlineData("{\"error\":\"download --> failed\"}", "srt")]
    [InlineData("<html><body>Dialogue: download failed</body></html>", "ass")]
    public void RejectsErrorDocumentsThatContainSubtitleMarkers(string content, string format)
    {
        var exception = Assert.Throws<InvalidDataException>(() =>
            SubtitleContentValidator.Validate(Encoding.UTF8.GetBytes(content), format, "application/octet-stream"));

        Assert.Contains("error document", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
