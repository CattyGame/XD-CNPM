namespace AURA.Analysis.Api.Upload;

/// <summary>
/// Adapter lưu file vào thư mục cục bộ (test và phát triển không cần Azurite). Không phải storage chính thức:
/// Sprint 2 dùng Azure Blob/Azurite qua cùng interface IPrivateFileStorage.
/// </summary>
public sealed class LocalPrivateFileStorage : IPrivateFileStorage
{
    private readonly string _root;

    public LocalPrivateFileStorage(string root) => _root = Path.GetFullPath(root);

    private string PathFor(string container, string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, container, key));
        var prefix = Path.GetFullPath(Path.Combine(_root, container)) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("Object key không hợp lệ.", nameof(key));
        return full;
    }

    public async Task PutAsync(string container, string key, Stream content, string contentType, CancellationToken ct)
    {
        var path = PathFor(container, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await content.CopyToAsync(fs, ct);
            File.Move(tmp, path, overwrite: true);   // ghi lại cùng key khi retry không tạo file mồ côi
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public Task<Stream> OpenReadAsync(string container, string key, CancellationToken ct)
    {
        var path = PathFor(container, key);
        if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file.", key);
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task DeleteAsync(string container, string key, CancellationToken ct)
    {
        var path = PathFor(container, key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<FileReadAccess> CreateReadAccessAsync(string container, string key, TimeSpan ttl, CancellationToken ct) =>
        throw new NotSupportedException("Adapter cục bộ không phát URL đọc; dùng adapter Azure Blob/Azurite.");
}
