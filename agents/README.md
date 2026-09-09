# 🤖 Agents — Chuẩn hóa AI Workflow cho Team Backend

## Mục đích

Thư mục `agents/` là **trung tâm quản lý cách team sử dụng AI** (Gemini, Copilot, ChatGPT,...) trong quá trình phát triển **Backend (.NET 8)**.  
Mục tiêu là đảm bảo **mọi thành viên đều sử dụng AI theo cùng một chuẩn**, tránh tình trạng code không đồng nhất.

## Cấu trúc thư mục

```
agents/
├── README.md                   ← Bạn đang đọc file này
├── rules/                      ← Quy tắc bắt buộc AI tuân thủ khi sinh code
│   └── coding-standards.md
├── prompts/                    ← Prompt template theo từng loại task
│   ├── create-controller.md
│   ├── create-service.md
│   ├── create-repository.md
│   ├── create-dto.md
│   ├── create-entity.md
│   ├── fix-bug.md
│   └── refactor.md
└── context/                    ← Ngữ cảnh dự án để AI hiểu codebase
    └── project-overview.md
```

## Cách sử dụng

### Bước 1: Đọc quy tắc
Trước khi dùng AI, đọc file `rules/coding-standards.md` để nắm rõ các quy tắc code mà AI phải tuân thủ.

### Bước 2: Chọn prompt phù hợp
Vào thư mục `prompts/`, chọn file `.md` tương ứng với task bạn đang làm:
- Tạo Controller API → `create-controller.md`
- Viết Service + Interface → `create-service.md`
- Viết Repository + Interface → `create-repository.md`
- Tạo DTO (Request/Response) → `create-dto.md`
- Tạo Entity/Model + EF Config → `create-entity.md`
- Fix bug → `fix-bug.md`
- Refactor code → `refactor.md`

### Bước 3: Đính kèm context
Copy nội dung từ `context/project-overview.md` và đính kèm vào đầu prompt để AI hiểu codebase.

### Bước 4: Điền thông tin và gửi
Thay thế các `[PLACEHOLDER]` trong prompt template bằng thông tin thực tế, rồi gửi cho AI.

### Bước 5: Lưu lại prompt hiệu quả
Nếu bạn tạo được prompt mới mà hiệu quả, hãy thêm vào thư mục `prompts/` để team cùng tái sử dụng.

## Quy tắc quan trọng

> ⚠️ **KHÔNG** tự ý dùng AI mà không tham khảo `rules/coding-standards.md`.  
> ⚠️ **LUÔN** đính kèm `context/project-overview.md` khi prompt lần đầu trong 1 session.  
> ⚠️ **LUÔN** review lại code do AI sinh ra trước khi commit.  
> ⚠️ **LUÔN** đảm bảo code có Interface → dùng DI → có thể test được.
