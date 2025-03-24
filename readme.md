# TeamSyncWorkspace

TeamSyncWorkspace is a collaborative workspace platform that facilitates team collaboration through shared workspaces, document editing, file management, and task tracking.

## Features 

- **Team Management**: Create and manage teams with different member roles (Owner, Admin, Member, Viewer)
- **Workspaces**: Each team has a dedicated workspace for collaboration
- **Google Authentication**: Support for login with Google accounts
- **Document Collaboration**: Real-time document editing with CKEditor 5
- **File Management**: Team file storage and sharing capabilities
- **Timeline**: Task management and deadline tracking
- **Notifications**: Real-time notifications for team activities and invitations
- **AI Assistance**: Integration with AI members for enhanced productivity

## Technology Stack

- **Framework**: ASP.NET Core 8.0 with Razor Pages
- **Database**: SQL Server 
- **Authentication**: ASP.NET Core Identity with Google OAuth
- **Front-end**: jQuery, CKEditor 5
- **Real-time**: SignalR (for notifications and collaboration)

## Prerequisites

- .NET 8.0 SDK or later
- SQL Server
- Visual Studio 2022 or Visual Studio Code
- Google OAuth credentials (for Google authentication)

## Getting Started

### Setting up the Database

1. Update the connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=TeamSyncWorkspace;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

2. Apply migrations to create the database:

```cmd
dotnet ef database update

```

## Running the Application

### Using Visual Studio:
1. Open the solution file TeamSyncWorkspace.sln
2. Build the solution (Ctrl+Shift+B)
3. Press F5 to run the application


### The application will be available at:
- HTTP: http://localhost:5050

## Project Structure
- **Data/**: Contains database context and configuration
- **Models/:** Domain model classes
- **Pages/:** Razor Pages for the UI
- **Account/**: User authentication and profile pages
- **Dashboard/**: Main workspace dashboard
- **Teams/**: Team management and invitations
- **Notifications/**: User notifications
- **Services/**: Business logic and service classes
- **wwwroot/**: Static files (CSS, JavaScript, images)


## Initial Setup After Running
- Register a new account
- Create a team (automatically creates a workspace)
- Invite team members using their email addresses
- Start collaborating in your workspace!  (NOT YET)


## Default Roles
The system comes with four predefined roles:

- **Owner**: Full control over the team and workspace
- **Admin**: Can manage team members and content; cannot delete the team
- **Member**: Can create and edit content
- **Viewer**: Read-only access to the workspace