namespace Shared.Services;

public interface IS3StorageService
{
    Task<string> UploadBase64ImageAsync(string base64Image, string folderName);
    string? GetPresignedUrl(string? s3ObjectKey, int expirationMinutes = 30);
}
