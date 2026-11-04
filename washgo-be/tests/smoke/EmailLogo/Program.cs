using System.Net.Mail;
using System.Net.Mime;
using WashGo.Application.Features.AuthApplication.Templates;

// Serialize locally to .eml; never send a real email or expose a real OTP.
var directory = Path.Combine(Path.GetTempPath(), "washgo-email-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var html = AuthEmailTemplates.RenderOtpEmail("OTP smoke", "Local MIME validation", "123456");
    if (html.Contains("<svg") || !html.Contains("src=\"cid:washgo-logo\""))
        throw new Exception("HTML must reference the embedded PNG via CID.");
    using var message = new MailMessage("sender@example.test", "recipient@example.test") { Subject = "Logo smoke" };
    var view = AuthEmailTemplates.CreateOtpView(html);
    message.AlternateViews.Add(view);
    var logo = view.LinkedResources.Single();
    if (logo.ContentId != AuthEmailTemplates.LogoContentId || logo.ContentType.MediaType != "image/png")
        throw new Exception("Invalid inline image metadata.");
    var signature = new byte[8];
    logo.ContentStream.ReadExactly(signature);
    logo.ContentStream.Position = 0;
    if (!signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        throw new Exception("Embedded resource is not a PNG.");
    using var smtp = new SmtpClient
    {
        DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
        PickupDirectoryLocation = directory
    };
    smtp.Send(message);
    var mime = File.ReadAllText(Directory.GetFiles(directory, "*.eml").Single());
    if (!mime.Contains("multipart/related") || !mime.Contains("image/png") || !mime.Contains("Content-ID: <washgo-logo>"))
        throw new Exception("Serialized email is missing its related image.");
    Console.WriteLine("PASS: OTP email contains a CID-linked PNG in multipart/related MIME.");
}
finally { Directory.Delete(directory, recursive: true); }
