using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using ShoreHue.UI.AI;
using Xunit;

namespace ShoreHue.Tests;

public class AiFileReadTests
{
    private static string CreateDocx(string text)
    {
        string path = Path.Combine(Path.GetTempPath(), "shorehue-test-" + Guid.NewGuid().ToString("N") + ".docx");
        using (var fs = File.Create(path))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var ct = zip.CreateEntry("[Content_Types].xml");
            using (var w = new StreamWriter(ct.Open(), Encoding.UTF8))
                w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");

            var doc = zip.CreateEntry("word/document.xml");
            using (var w = new StreamWriter(doc.Open(), Encoding.UTF8))
                w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>" + text + "</w:t></w:r></w:p></w:body></w:document>");
        }
        return path;
    }

    [Fact]
    public void Docx_Text_Is_Extracted()
    {
        string path = CreateDocx("HELLO-DOCX-MARKER");
        try
        {
            var text = AiChatView.ReadFileAsText(path, out bool binary, out bool isDocx);
            Assert.True(isDocx);
            Assert.False(binary);
            Assert.Contains("HELLO-DOCX-MARKER", text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Txt_Is_Read_As_Text()
    {
        string path = Path.Combine(Path.GetTempPath(), "shorehue-test-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "hello text content", new UTF8Encoding(false));
        try
        {
            var text = AiChatView.ReadFileAsText(path, out bool binary, out bool isDocx);
            Assert.False(binary);
            Assert.False(isDocx);
            Assert.Contains("hello text content", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Binary_Is_Detected()
    {
        string path = Path.Combine(Path.GetTempPath(), "shorehue-test-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[] { 0, 1, 2, 0, 255, 0, 3, 0, 4, 0 });
        try
        {
            var text = AiChatView.ReadFileAsText(path, out bool binary, out bool isDocx);
            Assert.True(binary);
        }
        finally { File.Delete(path); }
    }
}