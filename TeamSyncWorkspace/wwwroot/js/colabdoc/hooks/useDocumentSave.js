export function useDocumentSave(documentId, canEdit) {
    const { ref } = Vue;

    const isSaving = ref(false);
    const lastSaved = ref(null);
    const saveInterval = ref(null);
    const titleChanged = ref(false);

    const saveDocument = async (getContent) => {
        if (isSaving.value || !canEdit) return false;

        isSaving.value = true;
        const content = getContent();

        try {
            const response = await fetch(`/api/Documents/${documentId}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    content: content
                })
            });

            // if (response.ok) {
            lastSaved.value = new Date();
            return true;
            // } else {
            //     console.error('Error saving document:', await response.text());
            //     return false;
            // }
        } catch (error) {
            console.error('Error saving document:', error);
            return false;
        } finally {
            isSaving.value = false;
        }
    };

    const updateDocumentTitle = async (title) => {
        if (!titleChanged.value || !canEdit) return false;

        try {
            const response = await fetch(`/api/Documents/${documentId}/title`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({
                    title: title
                })
            });

            if (response.ok) {
                document.title = title;
                titleChanged.value = false;
                return true;
            } else {
                console.error('Error updating document title:', await response.text());
                return false;
            }
        } catch (error) {
            console.error('Error updating document title:', error);
            return false;
        }
    };

    const setupAutosave = (saveHandler) => {
        if (canEdit) {
            saveInterval.value = setInterval(() => {
                saveHandler();
            }, 30000);
        }
    };

    const cleanup = () => {
        if (saveInterval.value) {
            clearInterval(saveInterval.value);
        }
    };

    const setTitleChanged = (changed) => {
        titleChanged.value = changed;
    };

    return {
        isSaving,
        lastSaved,
        titleChanged,
        saveDocument,
        updateDocumentTitle,
        setupAutosave,
        setTitleChanged,
        cleanup
    };
}