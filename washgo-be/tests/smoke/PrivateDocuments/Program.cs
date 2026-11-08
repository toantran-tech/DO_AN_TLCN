using System.IO.Compression;
using System.Text;
using OpenMcdf;
using WashGo.Application.Features.FileApplication.Services;

static byte[] Pdf()
{
    var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>", "<< /Length 0 >>\nstream\n\nendstream" };
    var text = new StringBuilder("%PDF-1.4\n"); var offsets = new List<int>();
    foreach (var obj in objects) { offsets.Add(text.Length); text.Append($"{offsets.Count} 0 obj\n{obj}\nendobj\n"); }
    var xref = text.Length; text.Append("xref\n0 5\n0000000000 65535 f \n");
    foreach (var offset in offsets) text.Append($"{offset:D10} 00000 n \n");
    text.Append($"trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    return Encoding.ASCII.GetBytes(text.ToString());
}
static byte[] Doc(bool encrypted)
{
    using var memory = new MemoryStream();
    using (var root = RootStorage.Create(memory, OpenMcdf.Version.V3, StorageModeFlags.LeaveOpen))
    {
        using (var word = root.CreateStream("WordDocument"))
        {
            var fib = new byte[32]; fib[0] = 0xEC; fib[1] = 0xA5; if (encrypted) fib[11] = 1;
            word.Write(fib);
        }
        using var table = root.CreateStream("0Table"); table.WriteByte(0);
    }
    return memory.ToArray();
}
static byte[] Docx(bool badXml = false, bool macro = false)
{
    using var memory = new MemoryStream();
    using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
    {
        using (var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open()))
            writer.Write(badXml ? "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///never'>]><x>&e;</x>" : "<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body/></w:document>");
        using (var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open()))
            writer.Write("<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/word/document.xml' ContentType='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'/></Types>");
        if (macro) zip.CreateEntry("word/vbaProject.bin");
    }
    return memory.ToArray();
}
foreach (var (bytes, extension) in new[] { (Pdf(), ".pdf"), (Doc(false), ".doc"), (Docx(), ".docx") })
    if (PrivateDocumentValidator.ContentType(bytes, extension) == null) throw new Exception($"Valid {extension} rejected.");
foreach (var (bytes, extension) in new[] { (Doc(true), ".doc"), (Docx(true), ".docx"), (Docx(macro: true), ".docx"), (Pdf(), ".docx"), (Encoding.ASCII.GetBytes("%PDF-fake"), ".pdf") })
    if (PrivateDocumentValidator.ContentType(bytes, extension) != null) throw new Exception($"Invalid {extension} accepted.");
if (args.Length == 1)
{
    Directory.CreateDirectory(args[0]);
    File.WriteAllBytes(Path.Combine(args[0], "fixture.pdf"), Pdf());
    File.WriteAllBytes(Path.Combine(args[0], "fixture.doc"), Doc(false));
    File.WriteAllBytes(Path.Combine(args[0], "fixture.docx"), Docx());
}
Console.WriteLine("PASS: real PDF/DOC/DOCX structure accepted; malformed, wrong extension, encrypted DOC, DTD and macro DOCX rejected.");
