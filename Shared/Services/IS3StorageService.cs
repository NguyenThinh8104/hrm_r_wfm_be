using Microsoft.AspNetCore.Http;

namespace Shared.Services;

/// <summary>
/// Dịch vụ quản lý lưu trữ tệp tin và ảnh chân dung điểm danh trên Amazon S3.
/// </summary>
public interface IS3StorageService
{
    /// <summary>
    /// Tải chuỗi ảnh dạng Base64 lên AWS S3 và trả về đối tượng key tương ứng.
    /// </summary>
    /// <param name="base64Image">Chuỗi văn bản mã hóa Base64 của hình ảnh</param>
    /// <param name="folderName">Tên thư mục lưu trữ (VD: attendance/checkin)</param>
    /// <returns>Chuỗi S3 Object Key (VD: attendance/checkin/2026/09/15/uuid.jpg)</returns>
    Task<string> UploadBase64ImageAsync(string base64Image, string folderName);

    /// <summary>
    /// Tải tệp tin IFormFile trực tiếp từ HTTP Request multipart/form-data lên AWS S3.
    /// </summary>
    /// <param name="file">Tệp tin hình ảnh tải lên từ客户端</param>
    /// <param name="folderName">Tên thư mục lưu trữ (VD: attendance/checkin hoặc attendance/checkout)</param>
    /// <returns>Chuỗi S3 Object Key tương ứng</returns>
    Task<string> UploadFileAsync(IFormFile file, string folderName);

    /// <summary>
    /// Tạo liên kết Presigned URL có thời hạn phục vụ truy cập xem ảnh chân dung trực tiếp.
    /// </summary>
    /// <param name="s3ObjectKey">Chuỗi S3 Object Key lưu trong cơ sở dữ liệu</param>
    /// <param name="expirationMinutes">Thời gian hết hạn của đường dẫn (mặc định 30 phút)</param>
    /// <returns>Đường dẫn URL công khai có chữ ký số hoặc URL dự phòng môi trường dev</returns>
    string? GetPresignedUrl(string? s3ObjectKey, int expirationMinutes = 30);
}

