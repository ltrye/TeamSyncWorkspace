export function useTeamMembers(documentId, teamId, canEdit) {
    const { ref } = Vue;

    const teamMembers = ref([]);
    const showShareModal = ref(false);

    const loadTeamMembers = async () => {
        try {
            const response = await fetch(`/api/Teams/Members?teamId=${teamId}`);
            if (response.ok) {
                const members = await response.json();
                teamMembers.value = members.map(m => ({
                    ...m,
                    permission: 'view' // Default permission
                }));

                // Get current permissions
                await loadDocumentPermissions();
                return true;
            }
            return false;
        } catch (error) {
            console.error('Error loading team members:', error);
            return false;
        }
    };

    const loadDocumentPermissions = async () => {
        try {
            const response = await fetch(`/api/Documents/${documentId}/permissions`);
            if (response.ok) {
                const permissions = await response.json();
                // Update permissions for team members
                for (const perm of permissions) {
                    const member = teamMembers.value.find(m => m.id === perm.userId);
                    if (member) {
                        member.permission = perm.canEdit ? 'edit' : 'view';
                    }
                }
                return true;
            }
            return false;
        } catch (error) {
            console.error('Error loading document permissions:', error);
            return false;
        }
    };

    const shareDocument = () => {
        if (!canEdit) return false; // Only document editors can share
        showShareModal.value = true;
        return true;
    };

    const updatePermission = async (member) => {
        if (!canEdit) return false; // Only document editors can update permissions

        try {
            const response = await fetch(`/api/Documents/${documentId}/permissions`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    userId: member.id,
                    canEdit: member.permission === 'edit'
                })
            });

            return response.ok;
        } catch (error) {
            console.error('Error updating permission:', error);
            return false;
        }
    };

    const getAvatarUrl = (member) => {
        return `https://ui-avatars.com/api/?name=${encodeURIComponent(member.firstName + ' ' + member.lastName)}&size=32&background=E5E5E5&color=707070`;
    };

    return {
        teamMembers,
        showShareModal,
        loadTeamMembers,
        shareDocument,
        updatePermission,
        getAvatarUrl
    };
}