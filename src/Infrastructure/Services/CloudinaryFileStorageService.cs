using Application.Common.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public class CloudinaryFileStorageService : IFileStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly int _maxFileSizeBytes = 5 * 1024 * 1024; // 5MB

    public CloudinaryFileStorageService(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"] 
            ?? throw new InvalidOperationException("Cloudinary:CloudName is not configured");
        var apiKey = configuration["Cloudinary:ApiKey"] 
            ?? throw new InvalidOperationException("Cloudinary:ApiKey is not configured");
        var apiSecret = configuration["Cloudinary:ApiSecret"] 
            ?? throw new InvalidOperationException("Cloudinary:ApiSecret is not configured");

        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("File is required", nameof(file));

        // Validate file size
        if (file.Length > _maxFileSizeBytes)
            throw new InvalidOperationException($"File size exceeds maximum allowed ({_maxFileSizeBytes / 1024 / 1024}MB)");

        // Validate content type and file signature
        var isValidImage = await IsValidImageFileAsync(file, cancellationToken);
        if (!isValidImage)
            throw new InvalidOperationException("Only image files (PNG, JPG, WEBP) are allowed");

        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder,
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
        
        if (uploadResult.Error is not null)
            throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");

        return uploadResult.SecureUrl.AbsoluteUri;
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return;

        try
        {
            // Extract public ID from URL
            var uri = new Uri(fileUrl);
            var path = uri.AbsolutePath;
            var parts = path.Split('/');
            var fileName = parts.Last();
            var publicId = string.Join("/", parts.Skip(1).Take(parts.Length - 2)) + "/" + fileName.Split('.')[0];

            var deleteParams = new DeletionParams(publicId);
            await _cloudinary.DestroyAsync(deleteParams);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - deletion failure shouldn't break the flow
            Console.WriteLine($"Failed to delete file from Cloudinary: {ex.Message}");
        }
    }

    private async Task<bool> IsValidImageFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var allowedContentTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp" };
        if (!allowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return false;

        // Check file signature (magic bytes)
        using var stream = file.OpenReadStream();
        var buffer = new byte[12];
        var bytesRead = await stream.ReadAsync(buffer, 0, 12, cancellationToken);
        
        if (bytesRead < 12)
            return false;

        // PNG: 89 50 4E 47
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            return true;

        // JPEG: FF D8 FF
        if (buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF)
            return true;

        // WEBP: 52 49 46 46 (RIFF) and 57 45 42 50 (WEBP) at offset 8
        if (buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46 &&
            buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50)
            return true;

        return false;
    }
}
