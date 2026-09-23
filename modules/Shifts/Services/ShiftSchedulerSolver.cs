using Google.OrTools.Sat;
using Domain.Entities;
using Modules.Shifts.DTOs;

namespace Modules.Shifts.Services;

/// <summary>
/// Bộ giải tối ưu hóa phân bổ lịch ca tự động bằng Google OR-Tools (Constraint Programming CP-SAT Solver).
/// Áp dụng đầy đủ các Ràng buộc Cứng (Hard Constraints) và Ràng buộc Mềm (Soft Constraints) theo tiêu chuẩn công nghiệp.
/// </summary>
public class ShiftSchedulerSolver
{
    public class SolverInput
    {
        public ulong BranchId { get; set; }
        public DateOnly WeekStartDate { get; set; }
        public List<User> Employees { get; set; } = new List<User>();
        public List<ShiftTemplate> Templates { get; set; } = new List<ShiftTemplate>();
        public List<WorkSchedule> Schedules { get; set; } = new List<WorkSchedule>();
        public int MaxShiftsPerWeekPerEmployee { get; set; } = 6;
        public int MinShiftsPerWeekForFullTime { get; set; } = 5;
        /// <summary>
        /// Danh sách ngày khả dụng của từng nhân sự (xử lý giới hạn điều chuyển nhân sự).
        /// Nếu nhân sự có ngày không nằm trong tập này, solver sẽ không xếp ca cho nhân sự vào ngày đó.
        /// </summary>
        public Dictionary<ulong, HashSet<DateOnly>>? EmployeeAvailableDates { get; set; }
    }

    public class SolverResult
    {
        public bool IsSuccess { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public List<SingleAssignmentItemDto> RecommendedAssignments { get; set; } = new List<SingleAssignmentItemDto>();
        public int TotalShiftsAssigned { get; set; }
        public int DemandsSatisfied { get; set; }
        public int TotalDemands { get; set; }
    }

    /// <summary>
    /// Thực thi giải bài toán tối ưu phân bổ ca tuần bằng Google OR-Tools CP-SAT.
    /// </summary>
    public SolverResult Solve(SolverInput input)
    {
        var result = new SolverResult();

        var employees = input.Employees;
        var templates = input.Templates;
        var schedules = input.Schedules;

        if (!employees.Any() || !templates.Any() || !schedules.Any())
        {
            result.IsSuccess = false;
            result.StatusMessage = "Thiếu dữ liệu nhân sự, mẫu ca hoặc khung lịch tuần để chạy thuật toán.";
            return result;
        }

        int numEmployees = employees.Count;
        int numDays = 7;
        int numShifts = templates.Count;

        var model = new CpModel();

        // Biến quyết định nhị phân: x[e, d, s] = 1 nếu nhân viên e làm ca s vào ngày d (0..6)
        var x = new Dictionary<(int empIdx, int dayIdx, int shiftIdx), BoolVar>();

        for (int e = 0; e < numEmployees; e++)
        {
            for (int d = 0; d < numDays; d++)
            {
                for (int s = 0; s < numShifts; s++)
                {
                    x[(e, d, s)] = model.NewBoolVar($"x_e{e}_d{d}_s{s}");
                }
            }
        }

        // =========================================================================
        // 1. RÀNG BUỘC CỨNG (HARD CONSTRAINTS)
        // =========================================================================

        // Ràng buộc 1: Mỗi nhân viên không được làm quá 1 ca trong 1 ngày (d)
        for (int e = 0; e < numEmployees; e++)
        {
            for (int d = 0; d < numDays; d++)
            {
                var dailyShifts = new List<BoolVar>();
                for (int s = 0; s < numShifts; s++)
                {
                    dailyShifts.Add(x[(e, d, s)]);
                }
                model.Add(LinearExpr.Sum(dailyShifts) <= 1);
            }
        }

        // Ràng buộc 1.1: Giới hạn ngày khả dụng của nhân sự điều động (Chỉ được xếp lịch từ ngày bắt đầu đến ngày kết thúc điều chuyển)
        if (input.EmployeeAvailableDates != null)
        {
            for (int e = 0; e < numEmployees; e++)
            {
                var emp = employees[e];
                if (input.EmployeeAvailableDates.TryGetValue(emp.Id, out var availableDates))
                {
                    for (int d = 0; d < numDays; d++)
                    {
                        var workDate = input.WeekStartDate.AddDays(d);
                        if (!availableDates.Contains(workDate))
                        {
                            // Ngày này nhân sự không khả dụng tại chi nhánh (chưa đến ngày điều chuyển hoặc đã điều chuyển đi nơi khác)
                            for (int s = 0; s < numShifts; s++)
                            {
                                model.Add(x[(e, d, s)] == 0);
                            }
                        }
                    }
                }
            }
        }

        // Ràng buộc 2: Nghỉ sau ca đêm (Nhân viên làm ca đêm ngày d thì ngày d+1 bắt buộc nghỉ ca sáng)
        int nightShiftIdx = templates.FindIndex(t => t.IsOvernight || t.TemplateCode.Contains("DEM") || t.TemplateCode.Contains("NIGHT") || t.StartTime >= new TimeOnly(20, 0));
        int morningShiftIdx = templates.FindIndex(t => t.TemplateCode.Contains("SANG") || t.TemplateCode.Contains("MORNING") || t.StartTime <= new TimeOnly(8, 0));

        if (nightShiftIdx >= 0 && morningShiftIdx >= 0 && nightShiftIdx != morningShiftIdx)
        {
            for (int e = 0; e < numEmployees; e++)
            {
                for (int d = 0; d < numDays - 1; d++)
                {
                    // x[e, d, night] + x[e, d+1, morning] <= 1
                    model.Add(x[(e, d, nightShiftIdx)] + x[(e, d + 1, morningShiftIdx)] <= 1);
                }
            }
        }

        // Ràng buộc 3: Giới hạn số ca làm trong tuần (không quá 6 ca/tuần để đảm bảo ít nhất 1 ngày nghỉ)
        for (int e = 0; e < numEmployees; e++)
        {
            var weeklyShifts = new List<BoolVar>();
            for (int d = 0; d < numDays; d++)
            {
                for (int s = 0; s < numShifts; s++)
                {
                    weeklyShifts.Add(x[(e, d, s)]);
                }
            }
            model.Add(LinearExpr.Sum(weeklyShifts) <= input.MaxShiftsPerWeekPerEmployee);
        }

        // Ràng buộc 4: Chỉ gán nhân viên vào ca theo đúng định mức vai trò (Cashier / Sales / Security)
        var schedulesByDayAndTemplate = schedules
            .GroupBy(ws => (ws.WorkDate, ws.ShiftTemplateId))
            .ToDictionary(g => g.Key, g => g.First());

        for (int d = 0; d < numDays; d++)
        {
            var workDate = input.WeekStartDate.AddDays(d);

            for (int s = 0; s < numShifts; s++)
            {
                var template = templates[s];
                if (!schedulesByDayAndTemplate.TryGetValue((workDate, template.Id), out var ws))
                {
                    // Nếu ngày/ca này không có trong lịch, không được xếp ai
                    for (int e = 0; e < numEmployees; e++)
                    {
                        model.Add(x[(e, d, s)] == 0);
                    }
                    continue;
                }

                // Trưởng ca (SHIFT_LEADER - RoleId = 4)
                var leaderVars = new List<BoolVar>();
                // Thu ngân (CASHIER - RoleId = 5)
                var cashierVars = new List<BoolVar>();
                // Bán hàng (SALES_STAFF - RoleId = 6)
                var salesVars = new List<BoolVar>();
                // Bảo vệ (SECURITY_GUARD - RoleId = 7)
                var securityVars = new List<BoolVar>();

                for (int e = 0; e < numEmployees; e++)
                {
                    var emp = employees[e];
                    if (emp.RoleId == 4)
                    {
                        leaderVars.Add(x[(e, d, s)]);
                    }
                    else if (emp.RoleId == 5)
                    {
                        cashierVars.Add(x[(e, d, s)]);
                    }
                    else if (emp.RoleId == 6)
                    {
                        salesVars.Add(x[(e, d, s)]);
                    }
                    else if (emp.RoleId == 7)
                    {
                        securityVars.Add(x[(e, d, s)]);
                    }
                    else
                    {
                        // Store Manager hoặc các vai trò quản trị khác không xếp vào ca trực
                        model.Add(x[(e, d, s)] == 0);
                    }
                }

                if (leaderVars.Any())
                {
                    model.Add(LinearExpr.Sum(leaderVars) <= 1);
                }
                if (cashierVars.Any())
                {
                    model.Add(LinearExpr.Sum(cashierVars) <= ws.RequiredCashier);
                }
                if (salesVars.Any())
                {
                    model.Add(LinearExpr.Sum(salesVars) <= ws.RequiredSales);
                }
                if (securityVars.Any())
                {
                    model.Add(LinearExpr.Sum(securityVars) <= ws.RequiredSecurity);
                }
            }
        }

        // =========================================================================
        // 2. HÀM MỤC TIÊU & RÀNG BUỘC MỀM (SOFT CONSTRAINTS / OBJECTIVES)
        // =========================================================================
        // Tối đa hóa số lượng ca được gán thỏa mãn định mức
        // Ưu tiên nhân viên Full-time đạt ít nhất 5 ca/tuần
        var objectiveTerms = new List<LinearExpr>();

        for (int e = 0; e < numEmployees; e++)
        {
            var emp = employees[e];
            int weight = emp.EmploymentType == "FULL_TIME" ? 10 : 5;

            for (int d = 0; d < numDays; d++)
            {
                for (int s = 0; s < numShifts; s++)
                {
                    objectiveTerms.Add(x[(e, d, s)] * weight);
                }
            }
        }

        model.Maximize(LinearExpr.Sum(objectiveTerms));

        // =========================================================================
        // 3. GIẢI BÀI TOÁN (CP-SAT SOLVER)
        // =========================================================================
        var solver = new CpSolver();
        solver.StringParameters = "max_time_in_seconds:10.0;num_search_workers:8";

        var status = solver.Solve(model);

        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
        {
            result.IsSuccess = true;
            result.StatusMessage = status == CpSolverStatus.Optimal 
                ? "Google OR-Tools đã tìm ra phương án phân bổ lịch ca TỐI ƯU TUYỆT ĐỐI (Optimal)."
                : "Google OR-Tools đã tìm ra phương án phân bổ lịch ca KHẢ THI (Feasible).";

            for (int d = 0; d < numDays; d++)
            {
                var workDate = input.WeekStartDate.AddDays(d);

                for (int s = 0; s < numShifts; s++)
                {
                    var template = templates[s];

                    for (int e = 0; e < numEmployees; e++)
                    {
                        var emp = employees[e];
                        if (solver.Value(x[(e, d, s)]) == 1)
                        {
                            result.RecommendedAssignments.Add(new SingleAssignmentItemDto
                            {
                                UserId = emp.Id,
                                ShiftTemplateId = template.Id,
                                WorkDate = workDate,
                            });
                        }
                    }
                }
            }

            result.TotalShiftsAssigned = result.RecommendedAssignments.Count;
        }
        else
        {
            result.IsSuccess = false;
            result.StatusMessage = $"Không tìm được phương án thỏa mãn các ràng buộc (Status: {status}).";
        }

        return result;
    }
}
