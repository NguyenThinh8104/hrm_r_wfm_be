# ⚙️ Configurations — EF Core Fluent API Configurations

## Mục đích
Chứa các **IEntityTypeConfiguration<T>** classes — cấu hình mapping Entity → Database bằng Fluent API.

## Quy tắc
- Mỗi Entity **1 file Configuration riêng** → `EmployeeConfiguration.cs`, `UserConfiguration.cs`.
- Implement `IEntityTypeConfiguration<T>`.
- Cấu hình trong method `Configure(EntityTypeBuilder<T> builder)`.
- Ưu tiên Fluent API cho: table name, relationships, indexes, constraints, default values.
- Data Annotations dùng cho validation đơn giản trên Entity.
- Tất cả configurations tự động apply qua `ApplyConfigurationsFromAssembly()` trong `AppDbContext`.

## Ví dụ file trong folder này
```
Configurations/
├── UserConfiguration.cs           ← Config cho bảng Users
├── EmployeeConfiguration.cs       ← Config cho bảng Employees
├── DepartmentConfiguration.cs     ← Config cho bảng Departments
└── LeaveRequestConfiguration.cs   ← Config cho bảng LeaveRequests
```

## Namespace
```csharp
namespace hrm_r_wfm_be.Data.Configurations;
```

## Template
```csharp
// UserConfiguration.cs
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        // Table
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        // Properties
        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Employee");

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // Indexes
        builder.HasIndex(u => u.Email).IsUnique();

        // Relationships
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

## Auto-apply trong AppDbContext
```csharp
// Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Employee> Employees => Set<Employee>();
    // ... thêm DbSet cho các entity khác

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tự động apply TẤT CẢ configurations trong assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```
