namespace HamzetWasl.Application.Common;

/// <summary>Transport-neutral upload passed from the API to application services.</summary>
public sealed class FileUpload : IDisposable
{
    public FileUpload(Stream content, string fileName, string contentType, long length)
    {
        Content = content;
        FileName = fileName;
        ContentType = contentType;
        Length = length;
    }

    public Stream Content { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long Length { get; }

    public void Dispose() => Content.Dispose();
}
