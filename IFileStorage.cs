namespace RaceDay.Api.Services;

public record StoredFile(Stream Content, string ContentType);

/// <summary>Where uploaded images live. Azure Blob Storage in production, local disk as a fallback.</summary>
public interface IFileStorage
{
    /// <summary>Saves the stream and returns the stored name, e.g. "banners/3f2a....jpg".</summary>
    Task<string> SaveAsync(Stream content, string folder, string extension, string contentType);

    /// <summary>Opens a stored file, or returns null when it does not exist.</summary>
    Task<StoredFile?> OpenAsync(string name);

    Task DeleteAsync(string name);
}
