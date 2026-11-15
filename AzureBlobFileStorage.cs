using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>Stores images in a private Azure Blob Storage container. Only the API talks to Azure.</summary>
public class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient _container;
    private bool _ready;

    public AzureBlobFileStorage(string connectionString, string containerName)
        => _container = new BlobContainerClient(connectionString, containerName);

    private async Task EnsureAsync()
    {
        if (_ready) return;
        await _container.CreateIfNotExistsAsync(PublicAccessType.None);
        _ready = true;
    }

    public async Task<string> SaveAsync(Stream content, string folder, string extension, string contentType)
    {
        await EnsureAsync();
        var name = $"{folder}/{Guid.NewGuid():N}{extension}";
        await _container.GetBlobClient(name).UploadAsync(content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } });
        return name;
    }

    public async Task<StoredFile?> OpenAsync(string name)
    {
        await EnsureAsync();
        var blob = _container.GetBlobClient(name);
        if (!await blob.ExistsAsync()) return null;
        var download = await blob.DownloadStreamingAsync();
        return new StoredFile(download.Value.Content, download.Value.Details.ContentType ?? "application/octet-stream");
    }

    public async Task DeleteAsync(string name)
    {
        await EnsureAsync();
        await _container.GetBlobClient(name).DeleteIfExistsAsync();
    }
}
