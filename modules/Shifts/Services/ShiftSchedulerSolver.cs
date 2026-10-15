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

        /// <summary>
        /// Ngày hiện tại (để phân định các ngày đã qua và hôm nay: không tạo ca mới cho các ngày này).
        /// </summary>
        public DateOnly Today { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        /// <summary>
        /// Danh sách các ca đã có của nhân viên trong tuần ở các ngày đã qua / hôm nay.
        /// Solver sẽ cố định các ca này để tính toán các ràng buộc toàn tuần (ca tối đa, nghỉ ca đêm, v.v.).
        /// </summary>
        public List<SingleAssignmentItemDto> ExistingPastAssignments { get; set; } = new List<SingleAssignmentItemDto>();
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

        // Ràng buộc 0: Cố định các ca đã có trong các ngày đã qua và hôm nay (WorkDate <= Today)
        // Tuyệt đối không thay đổi hoặc xếp mới vào các ngày này; đồng thời các ca này là căn cứ
        // để solver tính toán số ca tối đa trong tuần, nghỉ sau ca đêm, định mức Full-time.
        var pastAssignmentSet = input.ExistingPastAssignments
            .Select(a => (a.UserId, a.WorkDate, a.ShiftTemplateId))
            .ToHashSet();

        for (int d = 0; d < numDays; d++)
        {
            var workDate = input.WeekStartDate.AddDays(d);
            if (workDate <= input.Today)
            {
                for (int e = 0; e < numEmployees; e++)
                {
                    var emp = employees[e];
                    for (int s = 0; s < numShifts; s++)
                    {
                        var template = templates[s];
                        bool hasShift = pastAssignmentSet.Contains((emp.Id, workDate, template.Id));
                        if (hasShift)
                        {
                            model.Add(x[(e, d, s)] == 1);
                        }
                        else
                        {
                            model.Add(x[(e, d, s)] == 0);
                        }
                    }
                }
            }
        }

        // Ràng buộc 1: Mỗi nhân viên không được làm quá 1 ca trong 1 ngày (d) (CHỈ ÁP DỤNG CHO CÁC NGÀY TƯƠNG LAI)
        for (int e = 0; e < numEmployees; e++)
        {
            for (int d = 0; d < numDays; d++)
            {
                var workDate = input.WeekStartDate.AddDays(d);
                if (workDate <= input.Today) continue; // Ngày đã qua/hôm nay đã được cố định ở Ràng buộc 0

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

        // Ràng buộc 2: Nghỉ tối thiểu 12 giờ giữa 2 ca của 2 ngày liên tiếp
        for (int s1 = 0; s1 < numShifts; s1++)
        {
            var t1 = templates[s1];
            // Xác định thời điểm kết thúc tương đối của ca t1 (tính bằng phút từ 00:00 ngày d)
            int start1Min = t1.StartTime.Hour * 60 + t1.StartTime.Minute;
            int end1Min = t1.EndTime.Hour * 60 + t1.EndTime.Minute;
            if (t1.IsOvernight || end1Min <= start1Min)
            {
                end1Min += 24 * 60; // Ca qua đêm kết thúc vào ngày d+1
            }

            for (int s2 = 0; s2 < numShifts; s2++)
            {
                var t2 = templates[s2];
                // Thời điểm bắt đầu của ca t2 vào ngày d+1 (tính bằng phút từ 00:00 ngày d)
                int start2Min = 24 * 60 + (t2.StartTime.Hour * 60 + t2.StartTime.Minute);

                // Khoảng cách nghỉ tính bằng giờ
                double restGapHours = (start2Min - end1Min) / 60.0;

                // Nếu thời gian nghỉ giữa ca s1 (ngày d) và ca s2 (ngày d+1) nhỏ hơn 12 giờ
                if (restGapHours < 12.0)
                {
                    for (int e = 0; e < numEmployees; e++)
                    {
                        var emp = employees[e];
                        for (int d = 0; d < numDays - 1; d++)
                        {
                            var dateD = input.WeekStartDate.AddDays(d);
                            var dateNext = input.WeekStartDate.AddDays(d + 1);

                            if (dateNext <= input.Today)
                            {
                                // Cả 2 ngày đều là quá khứ: Đã cố định trong lịch sử, không đặt ràng buộc để tránh xung đột dữ liệu cũ
                                continue;
                            }
                            else if (dateD <= input.Today && dateNext > input.Today)
                            {
                                // Ngày d là quá khứ/hôm nay, ngày d+1 là tương lai:
                                // Nếu nhân viên đã làm ca s1 ở ngày d, thì ngày d+1 KHÔNG ĐƯỢC làm ca s2
                                bool workedS1 = pastAssignmentSet.Contains((emp.Id, dateD, t1.Id));
                                if (workedS1)
                                {
                                    model.Add(x[(e, d + 1, s2)] == 0);
                                }
                            }
                            else
                            {
                                // Cả 2 ngày đều là tương lai: x[e, d, s1] + x[e, d+1, s2] <= 1
                                model.Add(x[(e, d, s1)] + x[(e, d + 1, s2)] <= 1);
                            }
                        }
                    }
                }
            }
        }

        // Ràng buộc 3: Giới hạn số ca làm trong tuần (không quá MaxShiftsPerWeekPerEmployee)
        for (int e = 0; e < numEmployees; e++)
        {
            var emp = employees[e];
            int pastShiftsCount = input.ExistingPastAssignments.Count(a => a.UserId == emp.Id);

            var futureShifts = new List<BoolVar>();
            for (int d = 0; d < numDays; d++)
            {
                var workDate = input.WeekStartDate.AddDays(d);
                if (workDate <= input.Today) continue;

                for (int s = 0; s < numShifts; s++)
                {
                    futureShifts.Add(x[(e, d, s)]);
                }
            }

            if (futureShifts.Any())
            {
                int remainingAllowed = Math.Max(0, input.MaxShiftsPerWeekPerEmployee - pastShiftsCount);
                model.Add(LinearExpr.Sum(futureShifts) <= remainingAllowed);
            }
        }

        // Ràng buộc 4: Chỉ gán nhân viên vào ca theo đúng định mức vai trò (Cashier / Sales / Security)
        var schedulesByDayAndTemplate = schedules
            .GroupBy(ws => (ws.WorkDate, ws.ShiftTemplateId))
            .ToDictionary(g => g.Key, g => g.First());

        for (int d = 0; d < numDays; d++)
        {
            var workDate = input.WeekStartDate.AddDays(d);
            if (workDate <= input.Today)
            {
                // Ngày đã qua hoặc hôm nay: Đã được cố định bởi Ràng buộc 0, không cần áp định mức sinh ca mới
                continue;
            }

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
                if (workDate <= input.Today)
                {
                    // Tuyệt đối không đề xuất xếp mới cho các ngày đã qua và hôm nay
                    continue;
                }

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
