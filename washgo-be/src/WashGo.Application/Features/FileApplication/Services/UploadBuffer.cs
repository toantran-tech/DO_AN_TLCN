namespace WashGo.Application.Features.FileApplication.Services;

/// <summary>Bound upload size before sending it to storage; temporary bytes are deleted on dispose.</summary>
internal static class UploadBuffer
{
    public static async Task<FileStream> CreateAsync(ReadOnlyMemory<byte> header, Stream source, long maximum)
    {
        var output = new FileStream(Path.Combine(Path.GetTempPath(), $"washgo-upload-{Guid.NewGuid():N}"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            await output.WriteAsync(header);
            var buffer = new byte[65536];
            int count;
            while ((count = await source.ReadAsync(buffer)) > 0)
            {
                if (output.Length + count > maximum) throw new IOException("Upload exceeds the size limit.");
                await output.WriteAsync(buffer.AsMemory(0, count));
            }
            await output.FlushAsync();
            output.Position = 0;
            return output;
        }
        catch { await output.DisposeAsync(); throw; }
    }
}
