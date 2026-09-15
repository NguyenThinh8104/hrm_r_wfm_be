using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

/// <summary>
/// Dịch vụ thực thi quản lý lưu trữ và truy xuất tệp tin hình ảnh điểm danh trên AWS S3.
/// </summary>
public class S3StorageService : IS3StorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<S3StorageService> _logger;
    private readonly string _bucketName;
    private readonly IAmazonS3? _s3Client;

    /// <summary>
    /// Khởi tạo S3StorageService và cấu hình AmazonS3Client từ IConfiguration.
    /// </summary>
    public S3StorageService(IConfiguration configuration, ILogger<S3StorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _bucketName = _configuration["AWS:S3BucketName"] ?? "hrm-r-wfm";

        var accessKey = _configuration["AWS:AccessKeyId"];
        var secretKey = _configuration["AWS:SecretAccessKey"];
        var regionName = _configuration["AWS:Region"] ?? "ap-southeast-1";

        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            var region = RegionEndpoint.GetBySystemName(regionName);
            _s3Client = new AmazonS3Client(accessKey, secretKey, region);
        }
        else
        {
            _logger.LogWarning("AWS Credentials not fully configured. S3StorageService will run in fallback mode.");
        }
    }

    /// <summary>
    /// Tải chuỗi ảnh dạng Base64 lên AWS S3 và trả về đối tượng key tương ứng.
    /// </summary>
    public async Task<string> UploadBase64ImageAsync(string base64Image, string folderName)
    {
        if (string.IsNullOrWhiteSpace(base64Image))
        {
            throw new ArgumentException("Image content cannot be empty", nameof(base64Image));
        }

        var cleanBase64 = base64Image;
        if (cleanBase64.Contains(","))
        {
            cleanBase64 = cleanBase64.Split(',')[1];
        }

        byte[] bytes = Convert.FromBase64String(cleanBase64);
        var fileName = $"{folderName}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}.jpg";

        if (_s3Client != null)
        {
            try
            {
                using var stream = new MemoryStream(bytes);
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = "image/jpeg"
                };

                await _s3Client.PutObjectAsync(putRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải ảnh Base64 lên AWS S3 bucket {BucketName}. Sử dụng fallback key: {FileName}", _bucketName, fileName);
            }
        }
        else
        {
            _logger.LogInformation("Fallback mode: Generated mock S3 key: {FileName}", fileName);
        }

        return fileName;
    }

    /// <summary>
    /// Tải tệp tin IFormFile trực tiếp từ HTTP Request multipart/form-data lên AWS S3.
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file, string folderName)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Tệp tin không được để trống.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        var fileName = $"{folderName}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}{ext}";

        if (_s3Client != null)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType
                };

                await _s3Client.PutObjectAsync(putRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải tệp tin lên AWS S3 bucket {BucketName}. Sử dụng fallback key: {FileName}", _bucketName, fileName);
            }
        }
        else
        {
            _logger.LogInformation("Fallback mode: Generated mock S3 key: {FileName}", fileName);
        }

        return fileName;
    }

    /// <summary>
    /// Tạo liên kết Presigned URL có thời hạn phục vụ truy cập xem ảnh chân dung trực tiếp.
    /// </summary>
    public string? GetPresignedUrl(string? s3ObjectKey, int expirationMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(s3ObjectKey)) return null;

        if (_s3Client != null)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = s3ObjectKey,
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes)
                };
                return _s3Client.GetPreSignedURL(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sinh Presigned URL từ S3 cho key {Key}", s3ObjectKey);
            }
        }

        // Fallback cho môi trường phát triển nếu S3 client không kết nối trực tiếp
        return $"https://{_bucketName}.s3.amazonaws.com/{s3ObjectKey}?temp_token={Guid.NewGuid()}&expires={DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).ToUnixTimeSeconds()}";
    }
}
