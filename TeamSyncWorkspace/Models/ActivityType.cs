namespace TeamSyncWorkspace.Models;

public static class ActivityType
{
    // Team management activities
    public const string ViewTeam = "ViewTeam";
    public const string EditTeam = "EditTeam";
    public const string DeleteTeam = "DeleteTeam";
    public const string InviteMembers = "InviteMembers";
    public const string RemoveMembers = "RemoveMembers";
    public const string ManageRoles = "ManageRoles";

    // Workspace activities
    public const string CreateWorkspace = "CreateWorkspace";
    public const string EditWorkspace = "EditWorkspace";
    public const string DeleteWorkspace = "DeleteWorkspace";

    // Document activities
    public const string CreateDocument = "CreateDocument";
    public const string EditDocument = "EditDocument";
    public const string DeleteDocument = "DeleteDocument";
    public const string ShareDocument = "ShareDocument";

    // File activities
    public const string UploadFile = "UploadFile";
    public const string DownloadFile = "DownloadFile";
    public const string DeleteFile = "DeleteFile";

    // Timeline activities
    public const string CreateTask = "CreateTask";
    public const string EditTask = "EditTask";
    public const string DeleteTask = "DeleteTask";
    public const string AssignTask = "AssignTask";
    public const string CompleteTask = "CompleteTask";

    public const string ManageTeam = "ManageTeam";
}