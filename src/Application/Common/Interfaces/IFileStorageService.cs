using Microsoft.AspNetCore.Http;

namespace Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken);
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken);
}
