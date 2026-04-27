using BenueCommunityMapping.Authorization;
using BenueCommunityMapping.Data;
using BenueCommunityMapping.Models;
using BenueCommunityMapping.Models.Email_Services;
using BenueCommunityMapping.Services;
using BenueCommunityMapping.Services.Analytics;
using BenueCommunityMapping.Services.Export;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("DefaultConnection"),
//        sql => sql.MigrationsAssembly("BenueCommunityMapping")));
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(j => {
    j.UseMySql(connection, ServerVersion.AutoDetect(connection));
});
// ── Identity ─────────────────────────────────────────────────────────
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength         = 8;
        options.Password.RequireDigit           = true;
        options.Password.RequireUppercase       = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail         = true;
        // Coordinator & Agent accounts have EmailConfirmed=false until they
        // click the verification link.  The seeded Admin keeps EmailConfirmed=true.
        options.SignIn.RequireConfirmedEmail     = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath         = "/Account/Login";
    opts.LogoutPath        = "/Account/Logout";
    opts.AccessDeniedPath  = "/Account/AccessDenied";
    opts.SlidingExpiration = true;
    opts.ExpireTimeSpan    = TimeSpan.FromHours(8);
    opts.Cookie.HttpOnly   = true;
    opts.Cookie.SameSite   = SameSiteMode.Strict;
});

// ── Authorization policies ────────────────────────────────────────────
builder.Services.AddAppAuthorization();

// ── Application services ──────────────────────────────────────────────
// IAnalyticsService registered before ISubmissionService because
// SubmissionService resolves IAnalyticsService via IServiceScopeFactory.
builder.Services.AddScoped<IAnalyticsService,  AnalyticsService>();
builder.Services.AddScoped<IExportService,     ExportService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IUserService,       UserService>();

// Bind SMTP settings from appsettings.json → "Smtp" section
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

builder.Services.AddTransient<IEmailSending,         SmtpEmailSender>();
builder.Services.AddTransient<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddHttpContextAccessor();   // needed by UserService to build the confirmation URL

// ── Razor Pages + authorization conventions ───────────────────────────
builder.Services.AddRazorPages(opts =>
{
    // Require authentication for every page by default
    opts.Conventions.AuthorizeFolder("/");

    // Public pages (no login required)
    opts.Conventions.AllowAnonymousToPage("/Account/Login");
    opts.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    opts.Conventions.AllowAnonymousToPage("/Account/ConfirmEmail");    // email verification link
    opts.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");  // password recovery
    opts.Conventions.AllowAnonymousToPage("/Account/ResetPassword");   // reset via emailed link
    opts.Conventions.AllowAnonymousToPage("/Error");

    // Role-scoped folders
    opts.Conventions.AuthorizeFolder("/Admin",        Policies.AdminOnly);
    opts.Conventions.AuthorizeFolder("/Admin/Analytics", Policies.AdminOrCoord); // analytics visible to coordinators too
    opts.Conventions.AuthorizeFolder("/DataAnalysis", Policies.AdminOrCoord);
    opts.Conventions.AuthorizeFolder("/Coordinator",  Policies.AdminOrCoord);
    opts.Conventions.AuthorizeFolder("/Agent",        Policies.AnyAuthUser);
    opts.Conventions.AuthorizeFolder("/Questionnaire",Policies.AnyAuthUser);
    opts.Conventions.AuthorizeFolder("/Account",      Policies.AnyAuthUser);
    opts.Conventions.AuthorizeFolder("/Api",          Policies.AnyAuthUser);
});

builder.Services.AddAntiforgery(opts => opts.HeaderName = "X-CSRF-TOKEN");

// ── Pipeline ─────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// Seed reference data and default users
await DbSeeder.SeedAsync(app.Services);

app.Run();
