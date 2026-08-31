using AI_Workshop.Data;
using AI_Workshop.Models.Identity;
using AI_Workshop.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AI_Workshop.Configuration;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("The DefaultConnection connection string is missing.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(RoleNames.Student, policy => policy.RequireRole(RoleNames.Student))
    .AddPolicy(RoleNames.Lecturer, policy => policy.RequireRole(RoleNames.Lecturer));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Student", RoleNames.Student);
    options.Conventions.AuthorizeFolder("/Lecturer", RoleNames.Lecturer);
});
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<InstitutionTimeService>();
builder.Services.Configure<AttendanceOptions>(builder.Configuration.GetSection(AttendanceOptions.SectionName));
builder.Services.Configure<AttendanceImportOptions>(builder.Configuration.GetSection(AttendanceImportOptions.SectionName));
builder.Services.AddScoped<AttendanceTokenService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<IAttendanceSpreadsheetReader, AttendanceSpreadsheetReader>();
builder.Services.AddScoped<AttendanceImportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

await IdentitySeedService.SeedAsync(app.Services, app.Configuration);

app.Run();
