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
    /// Cho biết AWS S3 đã được cấu hình với thông tin xác thực hợp lệ hay chưa.
    /// </summary>
    public bool IsS3Configured { get; }

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
        var regionName = _configuration["AWS:Region"] ?? "us-east-1";

        var isKeyValid = !string.IsNullOrWhiteSpace(accessKey)
                      && !accessKey.Contains("YOUR_AWS", StringComparison.OrdinalIgnoreCase)
                      && !string.IsNullOrWhiteSpace(secretKey)
                      && !secretKey.Contains("YOUR_AWS", StringComparison.OrdinalIgnoreCase);

        if (isKeyValid)
        {
            try
            {
                var region = RegionEndpoint.GetBySystemName(regionName);
                _s3Client = new AmazonS3Client(accessKey, secretKey, region);
                IsS3Configured = true;
                _logger.LogInformation("AWS S3 đã được kết nối thành công tới bucket {BucketName}.", _bucketName);
            }
            catch (Exception ex)
            {
                _s3Client = null;
                IsS3Configured = false;
                _logger.LogWarning(ex, "Khởi tạo AmazonS3Client thất bại. Chuyển sang chế độ Local Storage Fallback.");
            }
        }
        else
        {
            _s3Client = null;
            IsS3Configured = false;
            _logger.LogInformation("AWS Credentials chưa được cấu hình hoặc là placeholder. S3StorageService sẽ hoạt động ở chế độ Local Storage Fallback.");
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
        var cleanFolder = string.IsNullOrWhiteSpace(folderName) ? "attendance" : folderName.Trim().Trim('/');
        var fileName = $"{cleanFolder}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}.jpg";

        // Lưu bản sao cục bộ
        SaveLocalBytes(bytes, fileName);

        if (IsS3Configured && _s3Client != null)
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

        return fileName;
    }

    /// <summary>
    /// Tải tệp tin IFormFile trực tiếp từ HTTP Request multipart/form-data lên AWS S3 hoặc Local Storage.
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file, string folderName)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Tệp tin không được để trống.", nameof(file));
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".dat";
        var cleanFolder = string.IsNullOrWhiteSpace(folderName) ? "files" : folderName.Trim().Trim('/');
        var fileName = $"{cleanFolder}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}{ext.ToLowerInvariant()}";

        // Lưu bản sao cục bộ vào uploads/
        await SaveLocalFileAsync(file, fileName);

        if (IsS3Configured && _s3Client != null)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType
                };

                await _s3Client.PutObjectAsync(putRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải tệp tin lên AWS S3 bucket {BucketName}. Sử dụng fallback key: {FileName}", _bucketName, fileName);
            }
        }

        return fileName;
    }

    /// <summary>
    /// Tải tệp tin PDF lên AWS S3 với kiểm tra định dạng .pdf, MIME type và magic bytes (%PDF).
    /// </summary>
    public async Task<string> UploadPdfAsync(IFormFile file, string folderName)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Tệp tin PDF không được để trống.", nameof(file));
        }

        // 1. Kiểm tra dung lượng tối đa (20MB)
        const long maxSizeBytes = 20 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            throw new ArgumentException("Dung lượng tệp tin PDF vượt quá giới hạn cho phép (tối đa 20MB).", nameof(file));
        }

        // 2. Kiểm tra phần mở rộng file
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
        {
            throw new ArgumentException("Định dạng tệp không hợp lệ. Hệ thống chỉ chấp nhận tệp có phần mở rộng .pdf.", nameof(file));
        }

        // 3. Kiểm tra Content-Type
        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Content-Type của tệp tin không hợp lệ. Bắt buộc phải là 'application/pdf'.", nameof(file));
        }

        // 4. Kiểm tra magic bytes ký số tệp PDF (%PDF = 0x25, 0x50, 0x44, 0x46)
        using (var stream = file.OpenReadStream())
        {
            if (stream.Length < 4)
            {
                throw new ArgumentException("Tệp tin bị hỏng hoặc kích thước quá nhỏ để là tệp PDF hợp lệ.", nameof(file));
            }

            var header = new byte[4];
            var bytesRead = await stream.ReadAsync(header, 0, 4);
            if (bytesRead < 4 || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
            {
                throw new ArgumentException("Nội dung tệp tin không phải định dạng PDF hợp lệ (chữ ký tệp %PDF không khớp).", nameof(file));
            }
        }

        // 5. Sinh tên tệp duy nhất bảo vệ chống path traversal
        var cleanFolder = string.IsNullOrWhiteSpace(folderName) ? "documents" : folderName.Trim().Trim('/');
        var fileName = $"{cleanFolder}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}.pdf";

        // 6. Lưu trữ bản sao cục bộ
        await SaveLocalFileAsync(file, fileName);

        // 7. Tải lên S3 nếu S3 sẵn sàng
        if (IsS3Configured && _s3Client != null)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = "application/pdf"
                };

                await _s3Client.PutObjectAsync(putRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải PDF lên AWS S3 bucket {BucketName}. Sử dụng fallback key: {FileName}", _bucketName, fileName);
            }
        }

        return fileName;
    }

    /// <summary>
    /// Tạo liên kết Presigned URL có thời hạn phục vụ truy cập xem ảnh chân dung / file trực tiếp.
    /// </summary>
    public string? GetPresignedUrl(string? s3ObjectKey, int expirationMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(s3ObjectKey)) return null;

        if (IsS3Configured && _s3Client != null)
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

        // Khi chạy local fallback, trả về URL tải về qua API nội bộ thay vì URL AWS lỗi
        return $"/api/v1/files/download?key={Uri.EscapeDataString(s3ObjectKey)}";
    }

    /// <summary>
    /// Tạo liên kết Presigned URL có thời hạn phục vụ truy cập xem trực tiếp (inline) file (PDF/ảnh) trên trình duyệt.
    /// </summary>
    public string? GetPresignedViewUrl(string? s3ObjectKey, string contentType = "application/pdf", int expirationMinutes = 30)
    {
        if (string.IsNullOrWhiteSpace(s3ObjectKey)) return null;

        if (IsS3Configured && _s3Client != null)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = s3ObjectKey,
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    ResponseHeaderOverrides = new ResponseHeaderOverrides
                    {
                        ContentType = contentType,
                        ContentDisposition = "inline"
                    }
                };
                return _s3Client.GetPreSignedURL(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi sinh Presigned View URL từ S3 cho key {Key}", s3ObjectKey);
            }
        }

        // Local fallback: trả về URL API xem trực tiếp
        return $"/api/v1/files/view?key={Uri.EscapeDataString(s3ObjectKey)}";
    }

    /// <summary>
    /// Đọc Stream tệp tin từ AWS S3 (nếu đã cấu hình) hoặc từ local storage fallback uploads/.
    /// </summary>
    public async Task<(Stream? Stream, string ContentType, string FileName)?> GetFileStreamAsync(string s3ObjectKey)
    {
        if (string.IsNullOrWhiteSpace(s3ObjectKey)) return null;

        var ext = Path.GetExtension(s3ObjectKey).ToLowerInvariant();
        var mimeType = ext switch
        {
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".csv" => "text/csv; charset=utf-8",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
        var downloadFileName = Path.GetFileName(s3ObjectKey);

        // 1. Kiểm tra local storage
        var localRelPath = s3ObjectKey.Replace('/', Path.DirectorySeparatorChar);
        var possibleDirs = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "uploads"),
            Path.Combine(Directory.GetCurrentDirectory(), "API", "uploads"),
            Path.Combine(AppContext.BaseDirectory, "uploads"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "uploads")
        };

        foreach (var dir in possibleDirs)
        {
            var p = Path.Combine(dir, localRelPath);
            if (File.Exists(p))
            {
                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }
                memoryStream.Position = 0;
                return (memoryStream, mimeType, downloadFileName);
            }
        }

        // 2. Nếu S3 khả dụng, tải từ S3
        if (IsS3Configured && _s3Client != null)
        {
            try
            {
                var getRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3ObjectKey
                };
                var response = await _s3Client.GetObjectAsync(getRequest);
                var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType) ? mimeType : response.Headers.ContentType;
                return (memoryStream, contentType, downloadFileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải tệp tin từ S3 cho key {Key}", s3ObjectKey);
            }
        }

        return null;
    }

    private void SaveLocalBytes(byte[] bytes, string relativePath)
    {
        try
        {
            var localRelPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var localFullPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", localRelPath);
            var localDir = Path.GetDirectoryName(localFullPath);
            if (!string.IsNullOrEmpty(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            File.WriteAllBytes(localFullPath, bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lưu bản sao cục bộ cho file {Path}", relativePath);
        }
    }

    private async Task SaveLocalFileAsync(IFormFile file, string relativePath)
    {
        try
        {
            var localRelPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var localFullPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", localRelPath);
            var localDir = Path.GetDirectoryName(localFullPath);
            if (!string.IsNullOrEmpty(localDir))
            {
                Directory.CreateDirectory(localDir);
            }
            using var localStream = new FileStream(localFullPath, FileMode.Create);
            await file.CopyToAsync(localStream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lưu bản sao cục bộ cho file {Path}", relativePath);
        }
    }
}
