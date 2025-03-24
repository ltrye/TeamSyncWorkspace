using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSyncWorkspace.Data;
using TeamSyncWorkspace.Models;

namespace TeamSyncWorkspace.Services
{
    public class InvitationService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TeamService _teamService;
        private readonly NotificationService _notificationService;
        private readonly ILogger<InvitationService> _logger;

        public InvitationService(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            TeamService teamService,
            NotificationService notificationService,
            ILogger<InvitationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _teamService = teamService;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<(bool success, string message, TeamInvitation invitation)> CreateInvitationAsync(
            int teamId,
            int invitedByUserId,
            string invitedEmail,
            string role = "Member")
        {
            // Validate the inviter has permission to invite
            var team = await _context.Teams.FindAsync(teamId);
            if (team == null)
            {
                return (false, "Team not found.", null);
            }

            // Check if the inviter is a member with appropriate permissions
            var canInvite = await _teamService.CanUserPerformActionAsync(
                teamId, invitedByUserId, ActivityType.InviteMembers);

            if (!canInvite)
            {
                return (false, "You don't have permission to invite members to this team.", null);
            }

            // Check if the email has already been invited
            var existingInvitation = await _context.TeamInvitations
                .Where(i => i.TeamId == teamId &&
                            i.InvitedEmail.ToLower() == invitedEmail.ToLower() &&
                            !i.IsAccepted &&
                            !i.IsDeclined &&
                            i.ExpiryDate > DateTime.Now)
                .FirstOrDefaultAsync();

            if (existingInvitation != null)
            {
                return (false, "This email has already been invited to the team.", null);
            }

            // Check if user is already a member
            var invitedUser = await _userManager.FindByEmailAsync(invitedEmail);
            var invitedUserId = invitedUser?.Id;

            if (invitedUser != null)
            {
                var isAlreadyMember = await _context.TeamMembers
                    .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == invitedUser.Id);

                if (isAlreadyMember)
                {
                    return (false, "This user is already a member of the team.", null);
                }
            }

            // Create invitation with 7-day expiry
            var invitation = new TeamInvitation
            {
                TeamId = teamId,
                InvitedByUserId = invitedByUserId,
                InvitedUserId = invitedUserId,
                InvitedEmail = invitedEmail,
                InvitedDate = DateTime.Now,
                ExpiryDate = DateTime.Now.AddDays(7),
                IsAccepted = false,
                IsDeclined = false,
                Token = Guid.NewGuid().ToString(),
                Role = role
            };

            _context.TeamInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            // Create notification for the invited user if they're registered
            if (invitedUser != null)
            {
                await _notificationService.CreateNotificationAsync(
                    invitedUser.Id,
                    "Team Invitation",
                    $"You've been invited to join the team '{team.TeamName}'",
                    $"/Teams/Invitation?token={invitation.Token}",
                    "TeamInvitation",
                    invitation.InvitationId);
            }

            _logger.LogInformation("Invitation sent to {Email} for team {TeamId} by user {UserId}",
                invitedEmail, teamId, invitedByUserId);

            return (true, $"Invitation sent to {invitedEmail}.", invitation);
        }

        public async Task<List<TeamInvitation>> GetPendingInvitationsForUserAsync(int userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return new List<TeamInvitation>();
            }

            return await _context.TeamInvitations
                .Include(i => i.Team)
                .Include(i => i.InvitedBy)
                .Where(i => i.InvitedEmail.ToLower() == user.Email.ToLower() &&
                           !i.IsAccepted &&
                           !i.IsDeclined &&
                           i.ExpiryDate > DateTime.Now)
                .ToListAsync();
        }

        public async Task<TeamInvitation> GetInvitationByTokenAsync(string token)
        {
            return await _context.TeamInvitations
                .Include(i => i.Team)
                .Include(i => i.InvitedBy)
                .FirstOrDefaultAsync(i => i.Token == token);
        }

        public async Task<(bool success, string message)> AcceptInvitationAsync(string token, int userId)
        {
            var invitation = await GetInvitationByTokenAsync(token);

            if (invitation == null)
            {
                return (false, "Invitation not found or has expired.");
            }

            if (invitation.IsAccepted)
            {
                return (false, "This invitation has already been accepted.");
            }

            if (invitation.IsDeclined)
            {
                return (false, "This invitation has been declined.");
            }

            if (invitation.ExpiryDate < DateTime.Now)
            {
                return (false, "This invitation has expired.");
            }

            // Get the user
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Check if user's email matches the invitation
            if (!string.Equals(user.Email, invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "This invitation was not sent to your email address.");
            }

            // Add user to team
            var (addSuccess, addMessage) = await _teamService.AddMemberToTeamAsync(
                invitation.TeamId, userId, invitation.Role);

            if (!addSuccess)
            {
                return (false, addMessage);
            }

            // Update invitation
            invitation.IsAccepted = true;
            invitation.AcceptedDate = DateTime.Now;
            invitation.InvitedUserId = userId;

            await _context.SaveChangesAsync();

            // Create notification for the inviter
            await _notificationService.CreateNotificationAsync(
                invitation.InvitedByUserId,
                "Invitation Accepted",
                $"{user.FirstName} {user.LastName} accepted your invitation to join {invitation.Team.TeamName}",
                $"/Dashboard/Index?teamId={invitation.TeamId}",
                "InvitationAccepted",
                invitation.TeamId);

            _logger.LogInformation("User {UserId} accepted invitation to team {TeamId}",
                userId, invitation.TeamId);

            return (true, $"You have successfully joined the team '{invitation.Team.TeamName}'.");
        }

        public async Task<(bool success, string message)> DeclineInvitationAsync(string token, int userId)
        {
            var invitation = await GetInvitationByTokenAsync(token);

            if (invitation == null)
            {
                return (false, "Invitation not found or has expired.");
            }

            if (invitation.IsAccepted)
            {
                return (false, "This invitation has already been accepted.");
            }

            if (invitation.IsDeclined)
            {
                return (false, "This invitation has already been declined.");
            }

            if (invitation.ExpiryDate < DateTime.Now)
            {
                return (false, "This invitation has expired.");
            }

            // Get the user
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Check if user's email matches the invitation
            if (!string.Equals(user.Email, invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "This invitation was not sent to your email address.");
            }

            // Update invitation
            invitation.IsDeclined = true;
            invitation.InvitedUserId = userId;

            await _context.SaveChangesAsync();

            // Create notification for the inviter
            await _notificationService.CreateNotificationAsync(
                invitation.InvitedByUserId,
                "Invitation Declined",
                $"{user.FirstName} {user.LastName} declined your invitation to join {invitation.Team.TeamName}",
                null,
                "InvitationDeclined",
                invitation.TeamId);

            _logger.LogInformation("User {UserId} declined invitation to team {TeamId}",
                userId, invitation.TeamId);

            return (true, "You have declined the invitation.");
        }
    }
}