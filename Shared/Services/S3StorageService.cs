using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

public class S3StorageService : IS3StorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<S3StorageService> _logger;
    private readonly string _bucketName;
    private readonly IAmazonS3? _s3Client;

    public S3StorageService(IConfiguration configuration, ILogger<S3StorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _bucketName = _configuration["AWS:S3BucketName"] ?? "rwfm-kiosk-attendance";

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

    public async Task<string> UploadBase64ImageAsync(string base64Image, string folderName)
    {
        if (string.IsNullOrWhiteSpace(base64Image))
        {
            throw new ArgumentException("Image content cannot be empty", nameof(base64Image));
        }

        // Clean base64 string if it contains data prefix (e.g. data:image/jpeg;base64,...)
        var cleanBase64 = base64Image;
        if (cleanBase64.Contains(","))
        {
            cleanBase64 = cleanBase64.Split(',')[1];
        }

        byte[] bytes = Convert.FromBase64String(cleanBase64);
        var fileName = $"{folderName}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}.jpg";

        if (_s3Client != null)
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
        else
        {
            _logger.LogInformation("Fallback mode: Generated mock S3 key: {FileName}", fileName);
        }

        return fileName;
    }

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
                _logger.LogError(ex, "Error generating S3 Presigned URL for key {Key}", s3ObjectKey);
                return null;
            }
        }

        // Fallback for local development if S3 client is unconfigured
        return $"https://{_bucketName}.s3.amazonaws.com/{s3ObjectKey}?temp_token={Guid.NewGuid()}&expires={DateTimeOffset.UtcNow.AddMinutes(expirationMinutes).ToUnixTimeSeconds()}";
    }
}
