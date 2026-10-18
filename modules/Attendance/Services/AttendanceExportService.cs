using ClosedXML.Excel;
using Modules.Attendance.DTOs;

namespace Modules.Attendance.Services;

public class AttendanceExportService
{
    public byte[] ExportMonthlyAttendanceToExcel(
        StoreManagerMonthlyAttendanceResponseDto data,
        string branchName,
        string managerName)
    {
        using var workbook = new XLWorkbook();

        // ══════════════════════════════════════════════════════════════════════
        // SHEET 1: BẢNG CHẤM CÔNG CHI TIẾT
        // ══════════════════════════════════════════════════════════════════════
        var wsDetail = workbook.Worksheets.Add("Chi Tiết Chấm Công");
        
        // Tiêu đề
        wsDetail.Cell("A1").Value = "BÁO CÁO ĐIỂM DANH & CHẤM CÔNG CHI NHÁNH";
        wsDetail.Range("A1:L1").Merge().Style
            .Font.SetBold()
            .Font.SetFontSize(16)
            .Font.SetFontColor(XLColor.FromHtml("#1E3A8A"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        wsDetail.Cell("A2").Value = $"Chi nhánh: {branchName} | Tháng: {data.Month:D2}/{data.Year} | Quản lý xuất file: {managerName} | Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
        wsDetail.Range("A2:L2").Merge().Style
            .Font.SetItalic()
            .Font.SetFontSize(10)
            .Font.SetFontColor(XLColor.FromHtml("#4B5563"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // Header các cột
        string[] headers = {
            "STT", "Mã NV", "Họ và Tên", "Ngày", "Ca Làm Việc",
            "Giờ Vào Ca", "Check-in", "Trạng Thái Vào",
            "Giờ Hết Ca", "Check-out", "Trạng Thái Ra", "Tổng Giờ (h)"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = wsDetail.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#1E40AF")); // Navy Blue
            cell.Style.Font.SetFontColor(XLColor.White);
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }
        wsDetail.Row(4).Height = 26;

        int row = 5;
        int stt = 1;
        foreach (var r in data.Records)
        {
            wsDetail.Cell(row, 1).Value = stt++;
            wsDetail.Cell(row, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsDetail.Cell(row, 2).Value = r.EmployeeCode;
            wsDetail.Cell(row, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsDetail.Cell(row, 3).Value = r.FullName;

            wsDetail.Cell(row, 4).Value = r.ShiftDate.ToString("dd/MM/yyyy");
            wsDetail.Cell(row, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsDetail.Cell(row, 5).Value = r.ShiftName;

            wsDetail.Cell(row, 6).Value = r.ScheduledStartTime;
            wsDetail.Cell(row, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsDetail.Cell(row, 7).Value = r.CheckInTime.HasValue ? r.CheckInTime.Value.ToString("HH:mm:ss") : "--:--";
            wsDetail.Cell(row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Cột Trạng thái vào
            var inCell = wsDetail.Cell(row, 8);
            inCell.Value = TranslateStatus(r.CheckInStatus);
            inCell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ApplyStatusColor(inCell, r.CheckInStatus);

            wsDetail.Cell(row, 9).Value = r.ScheduledEndTime;
            wsDetail.Cell(row, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsDetail.Cell(row, 10).Value = r.CheckOutTime.HasValue ? r.CheckOutTime.Value.ToString("HH:mm:ss") : "--:--";
            wsDetail.Cell(row, 10).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Cột Trạng thái ra
            var outCell = wsDetail.Cell(row, 11);
            outCell.Value = TranslateStatus(r.CheckOutStatus);
            outCell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ApplyStatusColor(outCell, r.CheckOutStatus);

            wsDetail.Cell(row, 12).Value = r.ActualWorkHours ?? 0;
            wsDetail.Cell(row, 12).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

            wsDetail.Row(row).Height = 20;
            row++;
        }

        // Đóng viền bảng Sheet 1
        if (row > 5)
        {
            var dataRange = wsDetail.Range(4, 1, row - 1, headers.Length);
            dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            dataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        }
        wsDetail.Columns().AdjustToContents();

        // ══════════════════════════════════════════════════════════════════════
        // SHEET 2: BẢNG TỔNG HỢP CÔNG THEO NHÂN VIÊN
        // ══════════════════════════════════════════════════════════════════════
        var wsSummary = workbook.Worksheets.Add("Thống Kê Tổng Hợp");

        wsSummary.Cell("A1").Value = $"THỐNG KÊ CÔNG THÁNG {data.Month:D2}/{data.Year} - {branchName}";
        wsSummary.Range("A1:H1").Merge().Style
            .Font.SetBold()
            .Font.SetFontSize(15)
            .Font.SetFontColor(XLColor.FromHtml("#065F46"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        string[] sumHeaders = {
            "Mã NV", "Họ và Tên", "Tổng Ca Đăng Ký", "Đúng Giờ", "Đi Muộn", "Về Sớm", "Vắng/Nghỉ", "Tổng Giờ Thực Tế"
        };

        for (int i = 0; i < sumHeaders.Length; i++)
        {
            var cell = wsSummary.Cell(3, i + 1);
            cell.Value = sumHeaders[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#059669")); // Emerald Green
            cell.Style.Font.SetFontColor(XLColor.White);
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }
        wsSummary.Row(3).Height = 24;

        var groupedEmployees = data.Records
            .GroupBy(r => new { r.EmployeeId, r.EmployeeCode, r.FullName })
            .OrderBy(g => g.Key.EmployeeCode);

        int sumRow = 4;
        foreach (var g in groupedEmployees)
        {
            wsSummary.Cell(sumRow, 1).Value = g.Key.EmployeeCode;
            wsSummary.Cell(sumRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsSummary.Cell(sumRow, 2).Value = g.Key.FullName;

            wsSummary.Cell(sumRow, 3).Value = g.Count();
            wsSummary.Cell(sumRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsSummary.Cell(sumRow, 4).Value = g.Count(x => x.CheckInStatus == "ON_TIME");
            wsSummary.Cell(sumRow, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsSummary.Cell(sumRow, 5).Value = g.Count(x => x.CheckInStatus == "LATE");
            wsSummary.Cell(sumRow, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsSummary.Cell(sumRow, 6).Value = g.Count(x => x.CheckOutStatus == "EARLY_LEAVE");
            wsSummary.Cell(sumRow, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsSummary.Cell(sumRow, 7).Value = g.Count(x => x.OverallStatus == "ABSENT");
            wsSummary.Cell(sumRow, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            wsSummary.Cell(sumRow, 8).Value = Math.Round(g.Sum(x => x.ActualWorkHours ?? 0), 2);
            wsSummary.Cell(sumRow, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

            wsSummary.Row(sumRow).Height = 20;
            sumRow++;
        }

        // Đóng viền Sheet 2
        if (sumRow > 4)
        {
            var sumRange = wsSummary.Range(3, 1, sumRow - 1, sumHeaders.Length);
            sumRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            sumRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        }
        wsSummary.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static string TranslateStatus(string status)
    {
        return status switch
        {
            "ON_TIME" => "Đúng giờ",
            "EARLY" => "Đến sớm",
            "LATE" => "Đi muộn",
            "EARLY_LEAVE" => "Về sớm",
            "LATE_LEAVE" => "Về muộn",
            "ABSENT" => "Vắng mặt (Nghỉ)",
            "PENDING" => "Chưa đến ca",
            "NOT_CHECKED_OUT" => "Chưa check-out",
            _ => status
        };
    }

    private static void ApplyStatusColor(IXLCell cell, string status)
    {
        switch (status)
        {
            case "ON_TIME":
            case "EARLY":
                cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#DCFCE7")); // Xanh lá pastel
                cell.Style.Font.SetFontColor(XLColor.FromHtml("#166534"));
                break;
            case "LATE":
            case "EARLY_LEAVE":
                cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEF9C3")); // Vàng pastel
                cell.Style.Font.SetFontColor(XLColor.FromHtml("#854D0E"));
                break;
            case "ABSENT":
                cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FEE2E2")); // Đỏ pastel
                cell.Style.Font.SetFontColor(XLColor.FromHtml("#991B1B"));
                break;
        }
    }
}
