using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Baslangic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ShortName = table.Column<string>(type: "text", nullable: false),
                    EnglishName = table.Column<string>(type: "text", nullable: false),
                    ShortEnglishName = table.Column<string>(type: "text", nullable: false),
                    OtherName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TermType = table.Column<int>(type: "integer", nullable: false),
                    TermStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TermEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OverrideStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OverrideEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActivated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    YoksisStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IconName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    PasswordChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConstraintType = table.Column<int>(type: "integer", nullable: false),
                    TargetEventType = table.Column<int>(type: "integer", nullable: true),
                    TargetChainCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ParameterJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsHard = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarConstraints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ChainCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ItemsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Classrooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    BuildingName = table.Column<string>(type: "text", nullable: false),
                    Floor = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classrooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConstraintDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Parameters = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriticalityLevel = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumCourseOpeningReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AllowsAdvisorGrading = table.Column<bool>(type: "boolean", nullable: false),
                    BypassScheduleConflict = table.Column<bool>(type: "boolean", nullable: false),
                    BypassMandatoryAttendance = table.Column<bool>(type: "boolean", nullable: false),
                    IsExcludedFromGpa = table.Column<bool>(type: "boolean", nullable: false),
                    IsCreditFree = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsCustomGradingScale = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumCourseOpeningReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElectiveSubtypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RequiresFaculty = table.Column<bool>(type: "boolean", nullable: false),
                    ForbidsFaculty = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    ForbidsDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectiveSubtypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    QuestionType = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faculties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faculties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedCalendarDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AcademicYear = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EventsJson = table.Column<string>(type: "jsonb", nullable: false),
                    HolidaysJson = table.Column<string>(type: "jsonb", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    PreviousSnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    QualityScore = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedCalendarDrafts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EnglishName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FixedMonth = table.Column<int>(type: "integer", nullable: true),
                    FixedDay = table.Column<int>(type: "integer", nullable: true),
                    HijriMonth = table.Column<int>(type: "integer", nullable: true),
                    HijriDay = table.Column<int>(type: "integer", nullable: true),
                    DurationDays = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    IsHalfDayStart = table.Column<bool>(type: "boolean", nullable: false),
                    IsOfficialHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    BlocksAcademicEvents = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OverridesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KatalogAdimlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ad = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Calisti = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KatalogAdimlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KatalogKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tur = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Anahtar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IlkYazilma = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KatalogKayitlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MenuModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedRolesCsv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuModules_MenuModules_ParentId",
                        column: x => x.ParentId,
                        principalTable: "MenuModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EntityRef = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    GroupKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionBundles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionBundles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportAccessGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScopeKind = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<int>(type: "integer", nullable: false),
                    SubjectKind = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Effect = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportAccessGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportUserGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportUserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebModuleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleKey = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebModuleSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationPeriods_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TermAcademicCalendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    RegistrationStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddDropStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddDropEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WithdrawalDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GradeEntryStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GradeEntryEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationOverride = table.Column<bool>(type: "boolean", nullable: true),
                    AddDropOverride = table.Column<bool>(type: "boolean", nullable: true),
                    WithdrawalOverride = table.Column<bool>(type: "boolean", nullable: true),
                    GradeEntryOverride = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermAcademicCalendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermAcademicCalendars_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TermCalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    IsGenerated = table.Column<bool>(type: "boolean", nullable: false),
                    ChainCode = table.Column<string>(type: "text", nullable: true),
                    TemplateItemKey = table.Column<string>(type: "text", nullable: true),
                    GeneratedDraftId = table.Column<int>(type: "integer", nullable: true),
                    BlocksAcademicEvents = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermCalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermCalendarEvents_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    SlotOrder = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    AllowedMode = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeSlots_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissionDelegations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DelegatorUserId = table.Column<int>(type: "integer", nullable: false),
                    DelegateeUserId = table.Column<int>(type: "integer", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RevokeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionDelegations_AspNetUsers_DelegateeUserId",
                        column: x => x.DelegateeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermissionDelegations_AspNetUsers_DelegatorUserId",
                        column: x => x.DelegatorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FavoritesJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstraintExceptionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConstraintDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InstructorId = table.Column<int>(type: "integer", nullable: true),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: true),
                    TermId = table.Column<int>(type: "integer", nullable: true),
                    IsExempt = table.Column<bool>(type: "boolean", nullable: false),
                    ParameterOverride = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintExceptionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptionRequests_ConstraintDefinitions_Constrain~",
                        column: x => x.ConstraintDefinitionId,
                        principalTable: "ConstraintDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassroomTermAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomTermAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassroomTermAssignments_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomTermAssignments_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomTermAssignments_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: false),
                    IsServiceUnit = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FacultyScheduleApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacultyId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacultyScheduleApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacultyScheduleApprovals_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FacultyScheduleApprovals_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    RowsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleImportSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleImportSessions_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleImportSessions_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacultyId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    TotalProcessed = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalPenalty = table.Column<double>(type: "double precision", nullable: false),
                    ElapsedSeconds = table.Column<double>(type: "double precision", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleRuns_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleRuns_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserScopes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserScopes", x => x.Id);
                    table.CheckConstraint("CK_UserScopes_Kind_Faculty", "(\"Kind\" = 1 AND \"FacultyId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserScopes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserScopes_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuModules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "MenuModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PermissionBundleItems",
                columns: table => new
                {
                    BundleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionBundleItems", x => new { x.BundleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_PermissionBundleItems_PermissionBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "PermissionBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PermissionBundleItems_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false),
                    SourceBundleId = table.Column<int>(type: "integer", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_PermissionBundles_SourceBundleId",
                        column: x => x.SourceBundleId,
                        principalTable: "PermissionBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportDefinitions_ReportCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ReportCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportUserGroupMembers",
                columns: table => new
                {
                    GroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportUserGroupMembers", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ReportUserGroupMembers_ReportUserGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ReportUserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationPeriodQuestions",
                columns: table => new
                {
                    EvaluationPeriodId = table.Column<int>(type: "integer", nullable: false),
                    EvaluationQuestionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationPeriodQuestions", x => new { x.EvaluationPeriodId, x.EvaluationQuestionId });
                    table.ForeignKey(
                        name: "FK_EvaluationPeriodQuestions_EvaluationPeriods_EvaluationPerio~",
                        column: x => x.EvaluationPeriodId,
                        principalTable: "EvaluationPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluationPeriodQuestions_EvaluationQuestions_EvaluationQue~",
                        column: x => x.EvaluationQuestionId,
                        principalTable: "EvaluationQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<int>(type: "integer", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    RequiredRole = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecidedByUserId = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ConstraintExceptionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ConstraintExceptionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcademicPrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ExternalSystemCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    DegreeLevel = table.Column<int>(type: "integer", nullable: false),
                    EducationType = table.Column<int>(type: "integer", nullable: false),
                    MaxStudyDurationSemesters = table.Column<int>(type: "integer", nullable: false),
                    TotalRequiredEcts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicPrograms_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    EnglishName = table.Column<string>(type: "text", nullable: true),
                    ExamType = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    Ects = table.Column<int>(type: "integer", nullable: false),
                    TheoryHours = table.Column<int>(type: "integer", nullable: false),
                    LabHours = table.Column<int>(type: "integer", nullable: false),
                    ApplicationHours = table.Column<int>(type: "integer", nullable: true),
                    AlternativeName = table.Column<string>(type: "text", nullable: true),
                    ShowInInfoPackage = table.Column<bool>(type: "boolean", nullable: false),
                    OpenType = table.Column<int>(type: "integer", nullable: true),
                    TheoryClassroomType = table.Column<int>(type: "integer", nullable: true),
                    LabClassroomType = table.Column<int>(type: "integer", nullable: true),
                    IsNonGpa = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    TeachingMethod = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    NationalCredit = table.Column<int>(type: "integer", nullable: false),
                    MinCreditsRequired = table.Column<int>(type: "integer", nullable: true),
                    MinEctsRequired = table.Column<int>(type: "integer", nullable: true),
                    RequiresNoFailedLowerCourses = table.Column<bool>(type: "boolean", nullable: false),
                    CanBeTakenStandalone = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courses_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DistributionConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistributionType = table.Column<int>(type: "integer", nullable: false),
                    PreferenceLevel = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionConstraints_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionConstraints_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ElectiveGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ElectiveSubtypeId = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectiveGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElectiveGroups_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectiveGroups_ElectiveSubtypes_ElectiveSubtypeId",
                        column: x => x.ElectiveSubtypeId,
                        principalTable: "ElectiveSubtypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectiveGroups_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Instructors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    MaxWeeklyHours = table.Column<int>(type: "integer", nullable: false),
                    OfficeHours = table.Column<string>(type: "text", nullable: false),
                    AdvisorOptOutRequested = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailableForAdvisorAssignment = table.Column<bool>(type: "boolean", nullable: true),
                    AdvisorAssignmentCapacity = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instructors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Instructors_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Instructors_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemPermissions",
                columns: table => new
                {
                    MenuItemId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemPermissions", x => new { x.MenuItemId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_MenuItemPermissions_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Curricula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    ShortName = table.Column<string>(type: "text", nullable: true),
                    EnglishName = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionDate = table.Column<string>(type: "text", nullable: true),
                    DecisionNo = table.Column<string>(type: "text", nullable: true),
                    DecisionNote = table.Column<string>(type: "text", nullable: true),
                    GradingSystem = table.Column<string>(type: "text", nullable: true),
                    InternshipDuration = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EctsLimitBelow200 = table.Column<int>(type: "integer", nullable: true),
                    EctsLimit200To249 = table.Column<int>(type: "integer", nullable: true),
                    EctsLimitAbove250 = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Curricula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Curricula_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamRegulations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramId = table.Column<int>(type: "integer", nullable: false),
                    DegreeLevel = table.Column<int>(type: "integer", nullable: false),
                    TermType = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MidtermCount = table.Column<int>(type: "integer", nullable: false),
                    FinalCount = table.Column<int>(type: "integer", nullable: false),
                    MinMidtermWeightPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxMidtermWeightPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MinFinalWeightPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MaxFinalWeightPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    PassingGrade = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RawScoreFloor1 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    RawScoreFloor2 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    FinalExamFloor = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    BelowFloorGrade = table.Column<int>(type: "integer", nullable: false),
                    AAMin = table.Column<int>(type: "integer", nullable: false),
                    BAMin = table.Column<int>(type: "integer", nullable: false),
                    BBMin = table.Column<int>(type: "integer", nullable: false),
                    CBMin = table.Column<int>(type: "integer", nullable: false),
                    CCMin = table.Column<int>(type: "integer", nullable: false),
                    DCMin = table.Column<int>(type: "integer", nullable: false),
                    DDMin = table.Column<int>(type: "integer", nullable: false),
                    LetterRangesEditable = table.Column<bool>(type: "boolean", nullable: false),
                    EvaluationType = table.Column<int>(type: "integer", nullable: false),
                    RelativeMinStudentCount = table.Column<int>(type: "integer", nullable: false),
                    RelativeInclusionLimit = table.Column<decimal>(type: "numeric", nullable: false),
                    RelativeAbsoluteFloor = table.Column<decimal>(type: "numeric", nullable: false),
                    TAAMin = table.Column<int>(type: "integer", nullable: false),
                    TBAMin = table.Column<int>(type: "integer", nullable: false),
                    TBBMin = table.Column<int>(type: "integer", nullable: false),
                    TCBMin = table.Column<int>(type: "integer", nullable: false),
                    TCCMin = table.Column<int>(type: "integer", nullable: false),
                    TDCMin = table.Column<int>(type: "integer", nullable: false),
                    TDDMin = table.Column<int>(type: "integer", nullable: false),
                    BlockFinalIfNoMidterm = table.Column<bool>(type: "boolean", nullable: false),
                    FailIfNoFinal = table.Column<bool>(type: "boolean", nullable: false),
                    BlockMakeupIfNoFinal = table.Column<bool>(type: "boolean", nullable: false),
                    ZeroIfMissedMakeup = table.Column<bool>(type: "boolean", nullable: false),
                    MakeupReplacesFinal = table.Column<bool>(type: "boolean", nullable: false),
                    AutoTransferFailedToMakeup = table.Column<bool>(type: "boolean", nullable: false),
                    AutoTransferExcusedToMakeup = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamRegulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamRegulations_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TermAttendancePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    ProgramId = table.Column<int>(type: "integer", nullable: true),
                    TermWeekCount = table.Column<int>(type: "integer", nullable: false),
                    TheoryAbsenceLimitPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    LabAbsenceLimitPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermAttendancePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermAttendancePolicies_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TermAttendancePolicies_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseAccessPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    FacultyId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAccessPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseAccessPolicies_Faculties_FacultyId",
                        column: x => x.FacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseAccessRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AllowedFacultyId = table.Column<int>(type: "integer", nullable: true),
                    AllowedDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    AllowedProgramId = table.Column<int>(type: "integer", nullable: true),
                    MinClassYear = table.Column<int>(type: "integer", nullable: true),
                    MaxClassYear = table.Column<int>(type: "integer", nullable: true),
                    MinCgpa = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    RequiresAdvisorApproval = table.Column<bool>(type: "boolean", nullable: false),
                    IsOpenToOwnDepartment = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseAccessRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_AcademicPrograms_AllowedProgramId",
                        column: x => x.AllowedProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_Departments_AllowedDepartmentId",
                        column: x => x.AllowedDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseAccessRules_Faculties_AllowedFacultyId",
                        column: x => x.AllowedFacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CourseCrossLists",
                columns: table => new
                {
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    LinkedCourseId = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseCrossLists", x => new { x.CourseId, x.LinkedCourseId });
                    table.ForeignKey(
                        name: "FK_CourseCrossLists_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseCrossLists_Courses_LinkedCourseId",
                        column: x => x.LinkedCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseEquivalencies",
                columns: table => new
                {
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    EquivalentCourseId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEquivalencies", x => new { x.CourseId, x.EquivalentCourseId });
                    table.ForeignKey(
                        name: "FK_CourseEquivalencies_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseEquivalencies_Courses_EquivalentCourseId",
                        column: x => x.EquivalentCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CoursePrerequisites",
                columns: table => new
                {
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    RequiredCourseId = table.Column<int>(type: "integer", nullable: false),
                    IsCorequisite = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePrerequisites", x => new { x.CourseId, x.RequiredCourseId });
                    table.ForeignKey(
                        name: "FK_CoursePrerequisites_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoursePrerequisites_Courses_RequiredCourseId",
                        column: x => x.RequiredCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseSectionApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    RecommendedSectionCount = table.Column<int>(type: "integer", nullable: false),
                    ApprovedSectionCount = table.Column<int>(type: "integer", nullable: true),
                    PredictedDemand = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSectionApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseSectionApprovals_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSectionApprovals_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSectionApprovals_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ForecastCalibrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    PredictedDemand = table.Column<int>(type: "integer", nullable: false),
                    ActualEnrollment = table.Column<int>(type: "integer", nullable: false),
                    PredictedSections = table.Column<int>(type: "integer", nullable: false),
                    ActualSections = table.Column<int>(type: "integer", nullable: false),
                    AbsoluteError = table.Column<decimal>(type: "numeric", nullable: false),
                    PercentageError = table.Column<decimal>(type: "numeric", nullable: false),
                    RuleBasedWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    HistoricalWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    EligibleStudentCount = table.Column<int>(type: "integer", nullable: false),
                    ShowRate = table.Column<decimal>(type: "numeric", nullable: false),
                    CalibratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastCalibrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ForecastCalibrations_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForecastCalibrations_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ForecastCalibrations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeComponents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AcademicTermId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExamType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Other"),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ShortName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: ""),
                    Weight = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MinimumScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    RequiresAttendanceEligibility = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PrerequisiteGradeComponentId = table.Column<int>(type: "integer", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeComponents_AcademicTerms_AcademicTermId",
                        column: x => x.AcademicTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeComponents_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradeComponents_GradeComponents_PrerequisiteGradeComponentId",
                        column: x => x.PrerequisiteGradeComponentId,
                        principalTable: "GradeComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradingCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AcademicTermId = table.Column<int>(type: "integer", nullable: false),
                    CalculationMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Absolute"),
                    AAMin = table.Column<int>(type: "integer", nullable: false),
                    BAMin = table.Column<int>(type: "integer", nullable: false),
                    BBMin = table.Column<int>(type: "integer", nullable: false),
                    CBMin = table.Column<int>(type: "integer", nullable: false),
                    CCMin = table.Column<int>(type: "integer", nullable: false),
                    DCMin = table.Column<int>(type: "integer", nullable: false),
                    DDMin = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingCriteria_AcademicTerms_AcademicTermId",
                        column: x => x.AcademicTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GradingCriteria_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ElectiveGroupCourses",
                columns: table => new
                {
                    ElectiveGroupId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectiveGroupCourses", x => new { x.ElectiveGroupId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_ElectiveGroupCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ElectiveGroupCourses_ElectiveGroups_ElectiveGroupId",
                        column: x => x.ElectiveGroupId,
                        principalTable: "ElectiveGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcademicCvActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstructorId = table.Column<int>(type: "integer", nullable: false),
                    ActivityYear = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicCvActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicCvActivities_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    InstructorId = table.Column<int>(type: "integer", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    EnrolledCount = table.Column<int>(type: "integer", nullable: false),
                    InternalQuota = table.Column<int>(type: "integer", nullable: true),
                    ExternalQuota = table.Column<int>(type: "integer", nullable: true),
                    ClassroomId = table.Column<int>(type: "integer", nullable: true),
                    SectionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsFinalized = table.Column<bool>(type: "boolean", nullable: false),
                    IsExamCoordinator = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsScheduleLocked = table.Column<bool>(type: "boolean", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnrollmentSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    CrossListGroupId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseSections", x => x.Id);
                    table.CheckConstraint("CK_CourseSection_EnrolledCount_NonNegative", "\"EnrolledCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_CourseSections_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSections_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseSections_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseSections_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StudentNumber = table.Column<string>(type: "text", nullable: false),
                    NationalId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Cgpa = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    Sgpa = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    CompletedSemesters = table.Column<int>(type: "integer", nullable: false),
                    FrozenSemesterCount = table.Column<int>(type: "integer", nullable: false),
                    WithdrawalCount = table.Column<int>(type: "integer", nullable: false),
                    DepartureDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreviousStudentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsScholarshipStudent = table.Column<bool>(type: "boolean", nullable: false),
                    HasFinancialBlock = table.Column<bool>(type: "boolean", nullable: false),
                    AdvisorId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Instructors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumCourses",
                columns: table => new
                {
                    CurriculumId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    IsCompulsory = table.Column<bool>(type: "boolean", nullable: false),
                    SuggestedSemester = table.Column<int>(type: "integer", nullable: false),
                    IncludeInGpa = table.Column<bool>(type: "boolean", nullable: false),
                    ShowOnTranscript = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    OpeningReasonId = table.Column<int>(type: "integer", nullable: true),
                    IsMainGroupCourse = table.Column<bool>(type: "boolean", nullable: false),
                    IsFormationCourse = table.Column<bool>(type: "boolean", nullable: false),
                    ExcludedFromCreditTotal = table.Column<bool>(type: "boolean", nullable: false),
                    ExcludedFromTranscriptTotal = table.Column<bool>(type: "boolean", nullable: false),
                    AutoAddOnNewRegistration = table.Column<bool>(type: "boolean", nullable: false),
                    ShowAtTakenCurriculum = table.Column<bool>(type: "boolean", nullable: false),
                    ShowInstructorAtRegistration = table.Column<bool>(type: "boolean", nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNo = table.Column<string>(type: "text", nullable: true),
                    DecisionNote = table.Column<string>(type: "text", nullable: true),
                    CurriculumDescription = table.Column<string>(type: "text", nullable: true),
                    PreviousCourseId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumCourses", x => new { x.CurriculumId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_CurriculumCourses_CourseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CourseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CurriculumCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurriculumCourses_Courses_PreviousCourseId",
                        column: x => x.PreviousCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurriculumCourses_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurriculumCourses_CurriculumCourseOpeningReasons_OpeningRea~",
                        column: x => x.OpeningReasonId,
                        principalTable: "CurriculumCourseOpeningReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumElectivePools",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurriculumId = table.Column<int>(type: "integer", nullable: false),
                    ElectiveGroupId = table.Column<int>(type: "integer", nullable: false),
                    SlotName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SuggestedSemesterStart = table.Column<int>(type: "integer", nullable: false),
                    SuggestedSemesterEnd = table.Column<int>(type: "integer", nullable: false),
                    RequiredCourseCount = table.Column<int>(type: "integer", nullable: false),
                    RequiredEcts = table.Column<int>(type: "integer", nullable: false),
                    EctsPerCourse = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumElectivePools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumElectivePools_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CurriculumElectivePools_ElectiveGroups_ElectiveGroupId",
                        column: x => x.ElectiveGroupId,
                        principalTable: "ElectiveGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumTermRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CurriculumId = table.Column<int>(type: "integer", nullable: false),
                    ClassYear = table.Column<int>(type: "integer", nullable: false),
                    TermHalf = table.Column<int>(type: "integer", nullable: false),
                    RuleType = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumTermRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumTermRules_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromotionRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProgramId = table.Column<int>(type: "integer", nullable: false),
                    CurriculumId = table.Column<int>(type: "integer", nullable: true),
                    FromYear = table.Column<int>(type: "integer", nullable: false),
                    ToYear = table.Column<int>(type: "integer", nullable: false),
                    MinCgpa = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    MinEcts = table.Column<int>(type: "integer", nullable: true),
                    MinCourseCount = table.Column<int>(type: "integer", nullable: true),
                    MaxFailedCourses = table.Column<int>(type: "integer", nullable: true),
                    MinCompulsoryCoursePassed = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionRules_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromotionRules_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: false),
                    InstructorId = table.Column<int>(type: "integer", nullable: false),
                    WeekNumber = table.Column<int>(type: "integer", nullable: false),
                    SessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsQrOpen = table.Column<bool>(type: "boolean", nullable: false),
                    QrExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceSessions_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DistributionConstraintCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DistributionConstraintId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributionConstraintCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DistributionConstraintCourses_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DistributionConstraintCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DistributionConstraintCourses_DistributionConstraints_Distr~",
                        column: x => x.DistributionConstraintId,
                        principalTable: "DistributionConstraints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InternalAnnouncements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentFormat = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "plain"),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    TargetAudience = table.Column<int>(type: "integer", nullable: false),
                    TargetStudentYear = table.Column<int>(type: "integer", nullable: true),
                    TargetDepartmentId = table.Column<int>(type: "integer", nullable: true),
                    TargetFacultyId = table.Column<int>(type: "integer", nullable: true),
                    TargetSectionId = table.Column<int>(type: "integer", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsPopup = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAcknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalAnnouncements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalAnnouncements_AnnouncementCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AnnouncementCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InternalAnnouncements_CourseSections_TargetSectionId",
                        column: x => x.TargetSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InternalAnnouncements_Departments_TargetDepartmentId",
                        column: x => x.TargetDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InternalAnnouncements_Faculties_TargetFacultyId",
                        column: x => x.TargetFacultyId,
                        principalTable: "Faculties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: true),
                    InstructorId = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulingAuditLogs_CourseSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SectionExams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: false),
                    GradeComponentId = table.Column<int>(type: "integer", nullable: false),
                    ExamDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsAnnouncedToStudents = table.Column<bool>(type: "boolean", nullable: false),
                    AnnouncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionExams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionExams_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SectionExams_GradeComponents_GradeComponentId",
                        column: x => x.GradeComponentId,
                        principalTable: "GradeComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SectionSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: false),
                    TimeSlotId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: true),
                    BlockType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionSchedules_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SectionSchedules_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SectionSchedules_TimeSlots_TimeSlotId",
                        column: x => x.TimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdvisorCourseConstraints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    AdvisorId = table.Column<int>(type: "integer", nullable: false),
                    ConstraintType = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisorCourseConstraints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_Instructors_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdvisorCourseConstraints_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConstraintExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConstraintDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    InstructorId = table.Column<int>(type: "integer", nullable: true),
                    StudentId = table.Column<int>(type: "integer", nullable: true),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: true),
                    TermId = table.Column<int>(type: "integer", nullable: true),
                    IsExempt = table.Column<bool>(type: "boolean", nullable: false),
                    ParameterOverride = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstraintExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptions_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptions_ConstraintDefinitions_ConstraintDefini~",
                        column: x => x.ConstraintDefinitionId,
                        principalTable: "ConstraintDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptions_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptions_Instructors_InstructorId",
                        column: x => x.InstructorId,
                        principalTable: "Instructors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConstraintExceptions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EvaluationPeriodId = table.Column<int>(type: "integer", nullable: false),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationResponses_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationResponses_EvaluationPeriods_EvaluationPeriodId",
                        column: x => x.EvaluationPeriodId,
                        principalTable: "EvaluationPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationResponses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IssuedDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DocumentNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssuedDocuments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    TermId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdvisorNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrationPlans_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrationPlans_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "integer", nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentDocuments_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentDocuments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentPersonalInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    MobilePhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SecondaryEmail = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressProvince = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressDistrict = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressLine = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EmergencyContactRelation = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BirthDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BirthPlace = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    Nationality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BloodType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    MaritalStatus = table.Column<int>(type: "integer", nullable: true),
                    PassportNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MilitaryStatus = table.Column<int>(type: "integer", nullable: true),
                    MilitaryDeferralUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MilitaryOffice = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhotoExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPersonalInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPersonalInfos_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentPlacements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    EntranceType = table.Column<int>(type: "integer", nullable: true),
                    ScoreType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Score = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: true),
                    RankOrder = table.Column<int>(type: "integer", nullable: true),
                    QuotaType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PlacementYear = table.Column<int>(type: "integer", nullable: true),
                    PreferenceOrder = table.Column<int>(type: "integer", nullable: true),
                    PlacedProgramName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPlacements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPlacements_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentPrograms",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    ProgramId = table.Column<int>(type: "integer", nullable: false),
                    CurriculumId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    EnrollmentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPrograms", x => new { x.StudentId, x.ProgramId });
                    table.ForeignKey(
                        name: "FK_StudentPrograms_AcademicPrograms_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "AcademicPrograms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentPrograms_Curricula_CurriculumId",
                        column: x => x.CurriculumId,
                        principalTable: "Curricula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentPrograms_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentScholarshipDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    RatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Source = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentScholarshipDetails", x => x.Id);
                    table.CheckConstraint("CK_StudentScholarshipDetail_RatePercent", "\"RatePercent\" IS NULL OR (\"RatePercent\" >= 0 AND \"RatePercent\" <= 100)");
                    table.ForeignKey(
                        name: "FK_StudentScholarshipDetails_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementAttachments_InternalAnnouncements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "InternalAnnouncements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementReads",
                columns: table => new
                {
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementReads", x => new { x.AnnouncementId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AnnouncementReads_InternalAnnouncements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "InternalAnnouncements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SectionExamClassrooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionExamId = table.Column<int>(type: "integer", nullable: false),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionExamClassrooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionExamClassrooms_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SectionExamClassrooms_SectionExams_SectionExamId",
                        column: x => x.SectionExamId,
                        principalTable: "SectionExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResponseId = table.Column<int>(type: "integer", nullable: false),
                    QuestionId = table.Column<int>(type: "integer", nullable: false),
                    RatingValue = table.Column<int>(type: "integer", nullable: true),
                    TextValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationAnswers_EvaluationQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "EvaluationQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationAnswers_EvaluationResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "EvaluationResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CourseSectionId = table.Column<int>(type: "integer", nullable: false),
                    RegistrationPlanId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: true),
                    IsRepeated = table.Column<bool>(type: "boolean", nullable: false),
                    AdvisorNote = table.Column<string>(type: "text", nullable: true),
                    MandatoryAttendance = table.Column<bool>(type: "boolean", nullable: false),
                    CourseWithdrawalCount = table.Column<int>(type: "integer", nullable: false),
                    EctsSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CreditSnapshot = table.Column<int>(type: "integer", nullable: true),
                    AppliedElectivePoolId = table.Column<int>(type: "integer", nullable: true),
                    OpeningReasonIdSnapshot = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseRegistrations_CourseSections_CourseSectionId",
                        column: x => x.CourseSectionId,
                        principalTable: "CourseSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseRegistrations_CurriculumCourseOpeningReasons_OpeningR~",
                        column: x => x.OpeningReasonIdSnapshot,
                        principalTable: "CurriculumCourseOpeningReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseRegistrations_CurriculumElectivePools_AppliedElective~",
                        column: x => x.AppliedElectivePoolId,
                        principalTable: "CurriculumElectivePools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseRegistrations_RegistrationPlans_RegistrationPlanId",
                        column: x => x.RegistrationPlanId,
                        principalTable: "RegistrationPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CourseRegistrations_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceSessionId = table.Column<int>(type: "integer", nullable: false),
                    CourseRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    TotalSlotCount = table.Column<int>(type: "integer", nullable: false),
                    PresentSlotNumbersCsv = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QrSubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_AttendanceSessions_AttendanceSessionId",
                        column: x => x.AttendanceSessionId,
                        principalTable: "AttendanceSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_CourseRegistrations_CourseRegistrationId",
                        column: x => x.CourseRegistrationId,
                        principalTable: "CourseRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentGrades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    GradeComponentId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    MakeupScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ExamAttendance = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentGrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentGrades_CourseRegistrations_CourseRegistrationId",
                        column: x => x.CourseRegistrationId,
                        principalTable: "CourseRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentGrades_GradeComponents_GradeComponentId",
                        column: x => x.GradeComponentId,
                        principalTable: "GradeComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicCvActivities_InstructorId_ActivityYear",
                table: "AcademicCvActivities",
                columns: new[] { "InstructorId", "ActivityYear" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPrograms_Code",
                table: "AcademicPrograms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPrograms_DepartmentId",
                table: "AcademicPrograms",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPrograms_ExternalSystemCode",
                table: "AcademicPrograms",
                column: "ExternalSystemCode",
                unique: true,
                filter: "\"ExternalSystemCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicTerms_Year_TermType",
                table: "AcademicTerms",
                columns: new[] { "Year", "TermType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_AdvisorId",
                table: "AdvisorCourseConstraints",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_CourseId",
                table: "AdvisorCourseConstraints",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_StudentId_CourseId_TermId",
                table: "AdvisorCourseConstraints",
                columns: new[] { "StudentId", "CourseId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_StudentId_TermId_IsActive",
                table: "AdvisorCourseConstraints",
                columns: new[] { "StudentId", "TermId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisorCourseConstraints_TermId",
                table: "AdvisorCourseConstraints",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAttachments_AnnouncementId",
                table: "AnnouncementAttachments",
                column: "AnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementCategories_Name",
                table: "AnnouncementCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementCategories_SortOrder",
                table: "AnnouncementCategories",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReads_UserId",
                table: "AnnouncementReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_RequestId_StepOrder",
                table: "ApprovalSteps",
                columns: new[] { "RequestId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_AttendanceSessionId_StudentId",
                table: "AttendanceRecords",
                columns: new[] { "AttendanceSessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_CourseRegistrationId",
                table: "AttendanceRecords",
                column: "CourseRegistrationId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_StudentId",
                table: "AttendanceRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_CourseSectionId_WeekNumber",
                table: "AttendanceSessions",
                columns: new[] { "CourseSectionId", "WeekNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_InstructorId",
                table: "AttendanceSessions",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_Token",
                table: "AttendanceSessions",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp_Action",
                table: "AuditLogs",
                columns: new[] { "Timestamp", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarConstraints_AcademicYear_ConstraintType",
                table: "CalendarConstraints",
                columns: new[] { "AcademicYear", "ConstraintType" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarTemplates_ChainCode",
                table: "CalendarTemplates",
                column: "ChainCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_Name",
                table: "Classrooms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomTermAssignments_ClassroomId",
                table: "ClassroomTermAssignments",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomTermAssignments_FacultyId",
                table: "ClassroomTermAssignments",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomTermAssignments_TermId_ClassroomId",
                table: "ClassroomTermAssignments",
                columns: new[] { "TermId", "ClassroomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintDefinitions_Code",
                table: "ConstraintDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptionRequests_ConstraintDefinitionId",
                table: "ConstraintExceptionRequests",
                column: "ConstraintDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptionRequests_Status",
                table: "ConstraintExceptionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptions_ConstraintDefinitionId_InstructorId_De~",
                table: "ConstraintExceptions",
                columns: new[] { "ConstraintDefinitionId", "InstructorId", "DepartmentId", "CourseSectionId", "TermId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptions_CourseSectionId",
                table: "ConstraintExceptions",
                column: "CourseSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptions_DepartmentId",
                table: "ConstraintExceptions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptions_InstructorId",
                table: "ConstraintExceptions",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptions_StudentId",
                table: "ConstraintExceptions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstraintExceptions_TermId",
                table: "ConstraintExceptions",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_CourseId",
                table: "CourseAccessPolicies",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_DepartmentId",
                table: "CourseAccessPolicies",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_FacultyId",
                table: "CourseAccessPolicies",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessPolicies_TermId",
                table: "CourseAccessPolicies",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_AllowedDepartmentId",
                table: "CourseAccessRules",
                column: "AllowedDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_AllowedFacultyId",
                table: "CourseAccessRules",
                column: "AllowedFacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_AllowedProgramId",
                table: "CourseAccessRules",
                column: "AllowedProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseAccessRules_CourseId",
                table: "CourseAccessRules",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseCategories_Code",
                table: "CourseCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseCrossLists_LinkedCourseId",
                table: "CourseCrossLists",
                column: "LinkedCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEquivalencies_EquivalentCourseId",
                table: "CourseEquivalencies",
                column: "EquivalentCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePrerequisites_RequiredCourseId",
                table: "CoursePrerequisites",
                column: "RequiredCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_AppliedElectivePoolId",
                table: "CourseRegistrations",
                column: "AppliedElectivePoolId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_CourseSectionId",
                table: "CourseRegistrations",
                column: "CourseSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_OpeningReasonIdSnapshot",
                table: "CourseRegistrations",
                column: "OpeningReasonIdSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_RegistrationPlanId",
                table: "CourseRegistrations",
                column: "RegistrationPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_StudentId",
                table: "CourseRegistrations",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseRegistrations_StudentId_CourseSectionId",
                table: "CourseRegistrations",
                columns: new[] { "StudentId", "CourseSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_Code",
                table: "Courses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_DepartmentId",
                table: "Courses",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionApprovals_CourseId_DepartmentId_TermId",
                table: "CourseSectionApprovals",
                columns: new[] { "CourseId", "DepartmentId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionApprovals_DepartmentId",
                table: "CourseSectionApprovals",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSectionApprovals_TermId",
                table: "CourseSectionApprovals",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_ClassroomId",
                table: "CourseSections",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CourseId_TermId",
                table: "CourseSections",
                columns: new[] { "CourseId", "TermId" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CourseId_TermId_SectionNumber",
                table: "CourseSections",
                columns: new[] { "CourseId", "TermId", "SectionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_CrossListGroupId",
                table: "CourseSections",
                column: "CrossListGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_InstructorId",
                table: "CourseSections",
                column: "InstructorId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_TermId",
                table: "CourseSections",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_Curricula_ProgramId_Version",
                table: "Curricula",
                columns: new[] { "ProgramId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourseOpeningReasons_Code",
                table: "CurriculumCourseOpeningReasons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_CategoryId",
                table: "CurriculumCourses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_CourseId",
                table: "CurriculumCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_OpeningReasonId",
                table: "CurriculumCourses",
                column: "OpeningReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumCourses_PreviousCourseId",
                table: "CurriculumCourses",
                column: "PreviousCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumElectivePools_CurriculumId_ElectiveGroupId_SlotNa~",
                table: "CurriculumElectivePools",
                columns: new[] { "CurriculumId", "ElectiveGroupId", "SlotName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumElectivePools_ElectiveGroupId",
                table: "CurriculumElectivePools",
                column: "ElectiveGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumTermRules_CurriculumId_ClassYear_TermHalf_RuleType",
                table: "CurriculumTermRules",
                columns: new[] { "CurriculumId", "ClassYear", "TermHalf", "RuleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_FacultyId",
                table: "Departments",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraintCourses_CourseId",
                table: "DistributionConstraintCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraintCourses_CourseSectionId",
                table: "DistributionConstraintCourses",
                column: "CourseSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraintCourses_DistributionConstraintId_Cour~",
                table: "DistributionConstraintCourses",
                columns: new[] { "DistributionConstraintId", "CourseId", "CourseSectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraints_DepartmentId",
                table: "DistributionConstraints",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DistributionConstraints_TermId",
                table: "DistributionConstraints",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroupCourses_CourseId",
                table: "ElectiveGroupCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_Code",
                table: "ElectiveGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_DepartmentId",
                table: "ElectiveGroups",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_ElectiveSubtypeId",
                table: "ElectiveGroups",
                column: "ElectiveSubtypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveGroups_FacultyId",
                table: "ElectiveGroups",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectiveSubtypes_Code",
                table: "ElectiveSubtypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationAnswers_QuestionId",
                table: "EvaluationAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationAnswers_ResponseId",
                table: "EvaluationAnswers",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationPeriodQuestions_EvaluationQuestionId",
                table: "EvaluationPeriodQuestions",
                column: "EvaluationQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationPeriods_TermId",
                table: "EvaluationPeriods",
                column: "TermId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationQuestions_Category_SortOrder",
                table: "EvaluationQuestions",
                columns: new[] { "Category", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResponses_CourseSectionId",
                table: "EvaluationResponses",
                column: "CourseSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResponses_EvaluationPeriodId_CourseSectionId_Stud~",
                table: "EvaluationResponses",
                columns: new[] { "EvaluationPeriodId", "CourseSectionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationResponses_StudentId",
                table: "EvaluationResponses",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamRegulations_ProgramId_DegreeLevel_TermType_Year",
                table: "ExamRegulations",
                columns: new[] { "ProgramId", "DegreeLevel", "TermType", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faculties_Code",
                table: "Faculties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacultyScheduleApprovals_FacultyId_TermId",
                table: "FacultyScheduleApprovals",
                columns: new[] { "FacultyId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacultyScheduleApprovals_TermId",
                table: "FacultyScheduleApprovals",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_ForecastCalibrations_CourseId_TermId",
                table: "ForecastCalibrations",
                columns: new[] { "CourseId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ForecastCalibrations_DepartmentId",
                table: "ForecastCalibrations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ForecastCalibrations_TermId",
                table: "ForecastCalibrations",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedCalendarDrafts_AcademicYear_Status",
                table: "GeneratedCalendarDrafts",
                columns: new[] { "AcademicYear", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedCalendarDrafts_ExpiresAt",
                table: "GeneratedCalendarDrafts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_AcademicTermId",
                table: "GradeComponents",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_CourseId_AcademicTermId_Name",
                table: "GradeComponents",
                columns: new[] { "CourseId", "AcademicTermId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_PrerequisiteGradeComponentId",
                table: "GradeComponents",
                column: "PrerequisiteGradeComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingCriteria_AcademicTermId",
                table: "GradingCriteria",
                column: "AcademicTermId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingCriteria_CourseId_AcademicTermId",
                table: "GradingCriteria",
                columns: new[] { "CourseId", "AcademicTermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayDefinitions_Name",
                table: "HolidayDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instructors_DepartmentId",
                table: "Instructors",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Instructors_UserId",
                table: "Instructors",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_CategoryId",
                table: "InternalAnnouncements",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_IsActive_StartsAt_ExpiresAt",
                table: "InternalAnnouncements",
                columns: new[] { "IsActive", "StartsAt", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetAudience",
                table: "InternalAnnouncements",
                column: "TargetAudience");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetDepartmentId",
                table: "InternalAnnouncements",
                column: "TargetDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetFacultyId",
                table: "InternalAnnouncements",
                column: "TargetFacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetSectionId",
                table: "InternalAnnouncements",
                column: "TargetSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalAnnouncements_TargetStudentYear",
                table: "InternalAnnouncements",
                column: "TargetStudentYear");

            migrationBuilder.CreateIndex(
                name: "IX_IssuedDocuments_StudentId_IssuedAt",
                table: "IssuedDocuments",
                columns: new[] { "StudentId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KatalogAdimlari_Ad",
                table: "KatalogAdimlari",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KatalogKayitlari_Tur_Anahtar",
                table: "KatalogKayitlari",
                columns: new[] { "Tur", "Anahtar" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemPermissions_PermissionId",
                table: "MenuItemPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ModuleId_Route",
                table: "MenuItems",
                columns: new[] { "ModuleId", "Route" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_ModuleId_SortOrder",
                table: "MenuItems",
                columns: new[] { "ModuleId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Route",
                table: "MenuItems",
                column: "Route");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_Slug",
                table: "MenuItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuModules_ParentId",
                table: "MenuModules",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuModules_ParentId_SortOrder",
                table: "MenuModules",
                columns: new[] { "ParentId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuModules_Slug",
                table: "MenuModules",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionBundleItems_PermissionId",
                table: "PermissionBundleItems",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionBundles_Category",
                table: "PermissionBundles",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionBundles_Name",
                table: "PermissionBundles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDelegations_Delegatee_Active",
                table: "PermissionDelegations",
                columns: new[] { "DelegateeUserId", "RevokedAtUtc", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionDelegations_Delegator",
                table: "PermissionDelegations",
                columns: new[] { "DelegatorUserId", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Category",
                table: "Permissions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Key",
                table: "Permissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_CurriculumId",
                table: "PromotionRules",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionRules_ProgramId_CurriculumId_FromYear_ToYear",
                table: "PromotionRules",
                columns: new[] { "ProgramId", "CurriculumId", "FromYear", "ToYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPlans_StudentId_TermId",
                table: "RegistrationPlans",
                columns: new[] { "StudentId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationPlans_TermId",
                table: "RegistrationPlans",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportAccessGrants_ScopeKind_TargetId_SubjectKind_SubjectId",
                table: "ReportAccessGrants",
                columns: new[] { "ScopeKind", "TargetId", "SubjectKind", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportAccessGrants_SubjectKind_SubjectId",
                table: "ReportAccessGrants",
                columns: new[] { "SubjectKind", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportCategories_Code",
                table: "ReportCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_CategoryId",
                table: "ReportDefinitions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_Code",
                table: "ReportDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportUserGroupMembers_UserId",
                table: "ReportUserGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportUserGroups_Name",
                table: "ReportUserGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_bundle_unique",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId", "SourceBundleId" },
                unique: true,
                filter: "\"SourceBundleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_manual_unique",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true,
                filter: "\"SourceBundleId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_SourceBundleId",
                table: "RolePermissions",
                column: "SourceBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_ConsumedAt",
                table: "ScheduleImportSessions",
                column: "ConsumedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_CreatedByUserId_FacultyId_TermId_Exp~",
                table: "ScheduleImportSessions",
                columns: new[] { "CreatedByUserId", "FacultyId", "TermId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_FacultyId",
                table: "ScheduleImportSessions",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleImportSessions_TermId",
                table: "ScheduleImportSessions",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRuns_FacultyId_TermId_CreatedAt",
                table: "ScheduleRuns",
                columns: new[] { "FacultyId", "TermId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleRuns_TermId",
                table: "ScheduleRuns",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingAuditLogs_SectionId_CreatedAt",
                table: "SchedulingAuditLogs",
                columns: new[] { "SectionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SectionExamClassrooms_ClassroomId",
                table: "SectionExamClassrooms",
                column: "ClassroomId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionExamClassrooms_SectionExamId_ClassroomId",
                table: "SectionExamClassrooms",
                columns: new[] { "SectionExamId", "ClassroomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionExams_CourseSectionId_GradeComponentId",
                table: "SectionExams",
                columns: new[] { "CourseSectionId", "GradeComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionExams_GradeComponentId",
                table: "SectionExams",
                column: "GradeComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_Classroom_TimeSlot_Unique",
                table: "SectionSchedules",
                columns: new[] { "ClassroomId", "TimeSlotId" },
                unique: true,
                filter: "\"ClassroomId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_CourseSectionId_TimeSlotId_ClassroomId",
                table: "SectionSchedules",
                columns: new[] { "CourseSectionId", "TimeSlotId", "ClassroomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SectionSchedules_TimeSlotId",
                table: "SectionSchedules",
                column: "TimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDocuments_DocumentTypeId",
                table: "StudentDocuments",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentDocuments_StudentId_DocumentTypeId",
                table: "StudentDocuments",
                columns: new[] { "StudentId", "DocumentTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGrades_CourseRegistrationId_GradeComponentId",
                table: "StudentGrades",
                columns: new[] { "CourseRegistrationId", "GradeComponentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentGrades_GradeComponentId",
                table: "StudentGrades",
                column: "GradeComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPersonalInfos_StudentId",
                table: "StudentPersonalInfos",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPlacements_StudentId",
                table: "StudentPlacements",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPrograms_CurriculumId",
                table: "StudentPrograms",
                column: "CurriculumId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPrograms_ProgramId",
                table: "StudentPrograms",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_AdvisorId",
                table: "Students",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_NationalId",
                table: "Students",
                column: "NationalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentNumber",
                table: "Students",
                column: "StudentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserId",
                table: "Students",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentScholarshipDetails_StudentId_IsActive",
                table: "StudentScholarshipDetails",
                columns: new[] { "StudentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_Key",
                table: "SystemConfigs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TermAcademicCalendars_TermId",
                table: "TermAcademicCalendars",
                column: "TermId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TermAttendancePolicies_ProgramId",
                table: "TermAttendancePolicies",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_TermAttendancePolicies_TermId",
                table: "TermAttendancePolicies",
                column: "TermId",
                unique: true,
                filter: "\"ProgramId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TermAttendancePolicies_TermId_ProgramId",
                table: "TermAttendancePolicies",
                columns: new[] { "TermId", "ProgramId" },
                unique: true,
                filter: "\"ProgramId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TermCalendarEvents_IsGenerated_GeneratedDraftId",
                table: "TermCalendarEvents",
                columns: new[] { "IsGenerated", "GeneratedDraftId" });

            migrationBuilder.CreateIndex(
                name: "IX_TermCalendarEvents_TermId_StartDate",
                table: "TermCalendarEvents",
                columns: new[] { "TermId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_TermId_DayOfWeek_StartTime",
                table: "TimeSlots",
                columns: new[] { "TermId", "DayOfWeek", "StartTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId",
                table: "UserPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_FacultyId",
                table: "UserScopes",
                column: "FacultyId");

            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_UserId",
                table: "UserScopes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UQ_UserScopes_UserId_Kind",
                table: "UserScopes",
                columns: new[] { "UserId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebModuleSettings_ModuleKey_Key",
                table: "WebModuleSettings",
                columns: new[] { "ModuleKey", "Key" },
                unique: true);

            // ── Elle yazılmış veritabanı parçaları (EF modelinde karşılığı yok; denetim kaydının zaman-işlem dizini modelde) ─────────────
            // Eski zincirdeki migration'lardan aynen alındı; adlar korunur (AuditRetentionService
            // denetim tetikleyicisini adıyla kapatıp açıyor).

            // Denetim kaydı değiştirilemez (eski 20260411090000_AddAuditLogTamperProtection).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_audit_log_tampering()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'AuditLog tablosunda güncelleme ve silme işlemleri yasaktır. Kayıt ID: %', OLD."Id";
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_audit_log_no_update
                    BEFORE UPDATE ON "AuditLogs"
                    FOR EACH ROW
                    EXECUTE FUNCTION prevent_audit_log_tampering();

                CREATE TRIGGER trg_audit_log_no_delete
                    BEFORE DELETE ON "AuditLogs"
                    FOR EACH ROW
                    EXECUTE FUNCTION prevent_audit_log_tampering();
                """);

            // Kısıt istisnası en az bir varlığa bağlı (eski 20260416102221_AddConstraintExceptionCheckConstraint).
            migrationBuilder.Sql("""
                ALTER TABLE "ConstraintExceptions"
                ADD CONSTRAINT "CK_ConstraintException_AtLeastOneEntity"
                CHECK (
                    "InstructorId" IS NOT NULL OR
                    "StudentId" IS NOT NULL OR
                    "DepartmentId" IS NOT NULL OR
                    "CourseSectionId" IS NOT NULL OR
                    "TermId" IS NOT NULL
                );
                """);

            // Dersin bölümü zorunlu; model alanı boş bırakılabilir gösteriyor, veritabanı reddediyor
            // (eski 20260421135359_CourseDepartmentIdNotNull, aynen).
            migrationBuilder.Sql("""ALTER TABLE "Courses" ALTER COLUMN "DepartmentId" SET NOT NULL;""");

            // Çakışan etkin vekâlet engeli (eski 20260526112954_AddPermissionDelegationOverlapConstraint).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("""
                ALTER TABLE "PermissionDelegations"
                ADD CONSTRAINT "EX_PermissionDelegations_NoOverlap"
                EXCLUDE USING gist (
                    "DelegatorUserId" WITH =,
                    "DelegateeUserId" WITH =,
                    tstzrange("StartsAtUtc", "EndsAtUtc") WITH &&
                ) WHERE ("RevokedAtUtc" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicCvActivities");

            migrationBuilder.DropTable(
                name: "AdvisorCourseConstraints");

            migrationBuilder.DropTable(
                name: "AnnouncementAttachments");

            migrationBuilder.DropTable(
                name: "AnnouncementReads");

            migrationBuilder.DropTable(
                name: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "CalendarConstraints");

            migrationBuilder.DropTable(
                name: "CalendarTemplates");

            migrationBuilder.DropTable(
                name: "ClassroomTermAssignments");

            migrationBuilder.DropTable(
                name: "ConstraintExceptions");

            migrationBuilder.DropTable(
                name: "CourseAccessPolicies");

            migrationBuilder.DropTable(
                name: "CourseAccessRules");

            migrationBuilder.DropTable(
                name: "CourseCrossLists");

            migrationBuilder.DropTable(
                name: "CourseEquivalencies");

            migrationBuilder.DropTable(
                name: "CoursePrerequisites");

            migrationBuilder.DropTable(
                name: "CourseSectionApprovals");

            migrationBuilder.DropTable(
                name: "CurriculumCourses");

            migrationBuilder.DropTable(
                name: "CurriculumTermRules");

            migrationBuilder.DropTable(
                name: "DistributionConstraintCourses");

            migrationBuilder.DropTable(
                name: "ElectiveGroupCourses");

            migrationBuilder.DropTable(
                name: "EvaluationAnswers");

            migrationBuilder.DropTable(
                name: "EvaluationPeriodQuestions");

            migrationBuilder.DropTable(
                name: "ExamRegulations");

            migrationBuilder.DropTable(
                name: "FacultyScheduleApprovals");

            migrationBuilder.DropTable(
                name: "ForecastCalibrations");

            migrationBuilder.DropTable(
                name: "GeneratedCalendarDrafts");

            migrationBuilder.DropTable(
                name: "GradingCriteria");

            migrationBuilder.DropTable(
                name: "HolidayDefinitions");

            migrationBuilder.DropTable(
                name: "IssuedDocuments");

            migrationBuilder.DropTable(
                name: "KatalogAdimlari");

            migrationBuilder.DropTable(
                name: "KatalogKayitlari");

            migrationBuilder.DropTable(
                name: "MenuItemPermissions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PermissionBundleItems");

            migrationBuilder.DropTable(
                name: "PermissionDelegations");

            migrationBuilder.DropTable(
                name: "PromotionRules");

            migrationBuilder.DropTable(
                name: "ReportAccessGrants");

            migrationBuilder.DropTable(
                name: "ReportDefinitions");

            migrationBuilder.DropTable(
                name: "ReportUserGroupMembers");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ScheduleImportSessions");

            migrationBuilder.DropTable(
                name: "ScheduleRuns");

            migrationBuilder.DropTable(
                name: "SchedulingAuditLogs");

            migrationBuilder.DropTable(
                name: "SectionExamClassrooms");

            migrationBuilder.DropTable(
                name: "SectionSchedules");

            migrationBuilder.DropTable(
                name: "StudentDocuments");

            migrationBuilder.DropTable(
                name: "StudentGrades");

            migrationBuilder.DropTable(
                name: "StudentPersonalInfos");

            migrationBuilder.DropTable(
                name: "StudentPlacements");

            migrationBuilder.DropTable(
                name: "StudentPrograms");

            migrationBuilder.DropTable(
                name: "StudentScholarshipDetails");

            migrationBuilder.DropTable(
                name: "SystemConfigs");

            migrationBuilder.DropTable(
                name: "TermAcademicCalendars");

            migrationBuilder.DropTable(
                name: "TermAttendancePolicies");

            migrationBuilder.DropTable(
                name: "TermCalendarEvents");

            migrationBuilder.DropTable(
                name: "UserPreferences");

            migrationBuilder.DropTable(
                name: "UserScopes");

            migrationBuilder.DropTable(
                name: "WebModuleSettings");

            migrationBuilder.DropTable(
                name: "InternalAnnouncements");

            migrationBuilder.DropTable(
                name: "ConstraintExceptionRequests");

            migrationBuilder.DropTable(
                name: "AttendanceSessions");

            migrationBuilder.DropTable(
                name: "CourseCategories");

            migrationBuilder.DropTable(
                name: "DistributionConstraints");

            migrationBuilder.DropTable(
                name: "EvaluationResponses");

            migrationBuilder.DropTable(
                name: "EvaluationQuestions");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "ReportCategories");

            migrationBuilder.DropTable(
                name: "ReportUserGroups");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "PermissionBundles");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "SectionExams");

            migrationBuilder.DropTable(
                name: "TimeSlots");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "CourseRegistrations");

            migrationBuilder.DropTable(
                name: "AnnouncementCategories");

            migrationBuilder.DropTable(
                name: "ConstraintDefinitions");

            migrationBuilder.DropTable(
                name: "EvaluationPeriods");

            migrationBuilder.DropTable(
                name: "MenuModules");

            migrationBuilder.DropTable(
                name: "GradeComponents");

            migrationBuilder.DropTable(
                name: "CourseSections");

            migrationBuilder.DropTable(
                name: "CurriculumCourseOpeningReasons");

            migrationBuilder.DropTable(
                name: "CurriculumElectivePools");

            migrationBuilder.DropTable(
                name: "RegistrationPlans");

            migrationBuilder.DropTable(
                name: "Classrooms");

            migrationBuilder.DropTable(
                name: "Courses");

            migrationBuilder.DropTable(
                name: "Curricula");

            migrationBuilder.DropTable(
                name: "ElectiveGroups");

            migrationBuilder.DropTable(
                name: "AcademicTerms");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "AcademicPrograms");

            migrationBuilder.DropTable(
                name: "ElectiveSubtypes");

            migrationBuilder.DropTable(
                name: "Instructors");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Faculties");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_audit_log_tampering();");
        }
    }
}
