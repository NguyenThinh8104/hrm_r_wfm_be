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
    /// Kiểm tra hệ thống AWS S3 đã được cấu hình với credentials hợp lệ hay chưa (tránh trường hợp dùng key placeholder).
    /// </summary>
    bool IsS3Configured { get; }

    /// <summary>
    /// Tải tệp tin PDF lên AWS S3 với kiểm tra định dạng .pdf, MIME type và magic bytes (%PDF).
    /// </summary>
    /// <param name="file">Tệp tin PDF tải lên từ client</param>
    /// <param name="folderName">Tên thư mục lưu trữ (VD: headcount-requests hoặc documents)</param>
    /// <returns>Chuỗi S3 Object Key duy nhất (VD: headcount-requests/2026/09/23/uuid.pdf)</returns>
    Task<string> UploadPdfAsync(IFormFile file, string folderName);

    /// <summary>
    /// Tạo liên kết Presigned URL có thời hạn phục vụ truy cập xem ảnh chân dung / tải tệp tin trực tiếp.
    /// </summary>
    /// <param name="s3ObjectKey">Chuỗi S3 Object Key lưu trong cơ sở dữ liệu</param>
    /// <param name="expirationMinutes">Thời gian hết hạn của đường dẫn (mặc định 30 phút)</param>
    /// <returns>Đường dẫn URL có chữ ký số hoặc URL tải về local fallback</returns>
    string? GetPresignedUrl(string? s3ObjectKey, int expirationMinutes = 30);

    /// <summary>
    /// Tạo liên kết Presigned URL có thời hạn phục vụ truy cập xem trực tiếp (inline) file (PDF/ảnh) trên trình duyệt.
    /// </summary>
    /// <param name="s3ObjectKey">Chuỗi S3 Object Key lưu trong cơ sở dữ liệu</param>
    /// <param name="contentType">MIME type trả về (mặc định application/pdf)</param>
    /// <param name="expirationMinutes">Thời gian hết hạn của đường dẫn (mặc định 30 phút)</param>
    /// <returns>Đường dẫn URL có chữ ký số (S3) hoặc đường dẫn API xem trực tiếp (Local fallback)</returns>
    string? GetPresignedViewUrl(string? s3ObjectKey, string contentType = "application/pdf", int expirationMinutes = 30);

    /// <summary>
    /// Đọc Stream tệp tin từ AWS S3 (nếu đã cấu hình) hoặc từ local storage fallback uploads/.
    /// </summary>
    /// <param name="s3ObjectKey">Chuỗi S3 Object Key</param>
    /// <returns>Tuple chứa Stream dữ liệu, ContentType và FileName, hoặc null nếu không tìm thấy tệp</returns>
    Task<(Stream? Stream, string ContentType, string FileName)?> GetFileStreamAsync(string s3ObjectKey);
}

