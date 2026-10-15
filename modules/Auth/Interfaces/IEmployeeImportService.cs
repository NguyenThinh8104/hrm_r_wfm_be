using Modules.Auth.DTOs;
using Shared.Common;

namespace Modules.Auth.Interfaces;

public interface IEmployeeImportService
{
    Task<ApiResponse<BulkImportResultDto>> BulkImportEmployeesAsync(BulkImportEmployeeRequestDto dto, ulong actorId, string actorRole, string? ipAddress);
    Task<(byte[] FileBytes, string ContentType, string FileName)> GenerateEmployeeImportTemplateAsync(int count = 5);
    Task<(byte[] FileBytes, string ContentType, string FileName)> GenerateImportErrorReportAsync(List<ImportRowErrorDto> errors, string? sourceFileName);
}
