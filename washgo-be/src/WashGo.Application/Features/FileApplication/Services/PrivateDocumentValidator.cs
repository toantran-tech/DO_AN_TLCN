using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using OpenMcdf;
using UglyToad.PdfPig;

namespace WashGo.Application.Features.FileApplication.Services;

public static class PrivateDocumentValidator
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public static string? ContentType(byte[] bytes, string extension)
    {
        try
        {
            if (extension == ".pdf")
            {
                using var pdf = PdfDocument.Open(bytes, ParsingOptions.LenientParsingOff);
                return pdf.NumberOfPages is > 0 and <= 2000 ? "application/pdf" : null;
            }
            if (extension == ".doc")
            {
                using var stream = new MemoryStream(bytes, false);
                using var root = RootStorage.Open(stream, StorageModeFlags.StrictValidation);
                if (root.ContainsEntry("Macros") || root.ContainsEntry("VBA") || root.ContainsEntry("_VBA_PROJECT_CUR")) return null;
                using var word = root.OpenStream("WordDocument");
                if (word.Length < 32 || word.Length > MaxBytes) return null;
                var fib = new byte[32]; word.ReadExactly(fib);
                if (BitConverter.ToUInt16(fib, 0) != 0xA5EC || (BitConverter.ToUInt16(fib, 10) & 0x0100) != 0) return null;
                using var table = root.OpenStream((BitConverter.ToUInt16(fib, 10) & 0x0200) == 0 ? "0Table" : "1Table");
                return table.Length > 0 ? "application/msword" : null;
            }
            if (extension != ".docx") return null;
            using var zip = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
            if (zip.Entries.Count > 2048 || zip.Entries.Sum(x => x.Length) > 50L * 1024 * 1024 ||
                zip.Entries.Any(x => x.FullName.Contains("..") || x.FullName.StartsWith('/') ||
                    x.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))) return null;
            var doc = ReadXml(zip.GetEntry("word/document.xml"));
            var types = ReadXml(zip.GetEntry("[Content_Types].xml"));
            if (doc?.Root?.Name.LocalName != "document" ||
                doc.Root.Name.NamespaceName is not ("http://schemas.openxmlformats.org/wordprocessingml/2006/main" or "http://purl.oclc.org/ooxml/wordprocessingml/main")) return null;
            return types?.Descendants().Any(x => x.Name.LocalName == "Override" &&
                (string?)x.Attribute("PartName") == "/word/document.xml" &&
                (string?)x.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml") == true
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document" : null;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { return null; }
    }
    private static XDocument? ReadXml(ZipArchiveEntry? entry)
    {
        if (entry == null || entry.Length > 10 * 1024 * 1024) return null;
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
}
