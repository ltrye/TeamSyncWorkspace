using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Hubs;
using TeamSyncWorkspace.Hubs.Handlers;
using TeamSyncWorkspace.Models;
using TeamSyncWorkspace.Services;
using TeamSyncWorkspace.Services.ColabDocServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
// Add services to the container.
builder.Services.AddRazorPages();
// Add Controllers support
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(
    options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    // options => options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection")!)
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
        options.CallbackPath = "/signin-google";
        options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/Account/Login?errorMessage=" + context.Failure.Message);
                return Task.CompletedTask;
            };

    });

builder.Services.AddAuthorization();

// Add email sender service
var smtpServer = builder.Configuration["Smtp:Server"];
var smtpPort = int.Parse(builder.Configuration["Smtp:Port"]);
var smtpUser = builder.Configuration["Smtp:User"];
var smtpPass = builder.Configuration["Smtp:Pass"];
builder.Services.AddSingleton<IEmailSender>(new EmailSender(smtpServer, smtpPort, smtpUser, smtpPass));

// Register application services
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<TeamRoleService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<TeamRoleManagementService>();
builder.Services.AddScoped<StatisticService>(); 
// Add this line with the other service registrations
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<DocumentService>();

builder.Services.AddScoped<TempDocumentManager>();
builder.Services.AddScoped<DocumentCollaborationService>();
// Register handlers
builder.Services.AddScoped<DocumentJoinHandler>();
builder.Services.AddScoped<DocumentUpdateHandler>();
builder.Services.AddScoped<DocumentLeaveHandler>();
builder.Services.AddScoped<CursorPositionHandler>();
builder.Services.AddScoped<CommentHandler>();
builder.Services.AddScoped<ChatHandler>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<FolderService>();
builder.Services.AddScoped<FileService>();

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
app.MapHub<DocumentHub>("/hubs/document");
app.MapHub<ChatHub>("/chatHub");
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
