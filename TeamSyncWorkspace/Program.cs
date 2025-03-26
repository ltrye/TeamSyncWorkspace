using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Hubs;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
// Add Controllers support
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    );

builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Add this to your service registrations in Program.cs
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.CallbackPath = "/ExternalLogin?handler=Callback";
    });

// Register application services
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<TeamRoleService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<TeamRoleManagementService>();
builder.Services.AddScoped<TaskService>();


// Add SignalR services
builder.Services.AddSignalR();
// Application configuration
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Initialize default roles and permissions
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleService = services.GetRequiredService<TeamRoleService>();
    await roleService.InitializeDefaultRolesAsync();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
// Add route mapping with authentication-based redirection
app.MapRazorPages();

//app.MapGet("/api/tasks", async (AppDbContext db, string workspaceId, DateTime startDate, DateTime endDate, ILogger<Program> logger) =>
//{
//    logger.LogInformation("Fetching tasks for workspaceId: {WorkspaceId}, StartDate: {StartDate}, EndDate: {EndDate}",
//                          workspaceId, startDate, endDate);

//    if (string.IsNullOrEmpty(workspaceId))
//    {
//        return Results.BadRequest("Missing workspaceId");
//    }

//    var tasks = await db.TimelineTasks
//        .Where(t => t.WorkspaceId == workspaceId && t.DueDate >= startDate && t.DueDate <= endDate)
//        .OrderBy(t => t.DueDate)
//        .ToListAsync();

//    return Results.Ok(tasks);
//}).WithName("GetTasks");


// Configure endpoint
app.MapHub<NotificationHub>("/notificationHub");
// Default route handling
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/" || context.Request.Path == "/Index")
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            // Redirect authenticated users to Teams page
            context.Response.Redirect("/Teams");
            return;
        }
        else
        {
            // Redirect unauthenticated users to Home (landing page)
            context.Response.Redirect("/Home");
            return;
        }
    }

    await next();
});

app.Run();
