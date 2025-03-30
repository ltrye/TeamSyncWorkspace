// Import Vue composition utilities
const { createApp, onMounted, onBeforeUnmount, ref, reactive, watch } = Vue;

// Import the CKEditor initialization function
import { createEditor } from './ckeditor.js';

// Import composables
import { useEditor } from './hooks/useEditor.js';
import { useCollaboration } from './hooks/useCollaboration.js';
import { useDocumentSave } from './hooks/useDocumentSave.js';
import { useDocumentExport } from './hooks/useDocumentExport.js';
import { useTeamMembers } from './hooks/useTeamMembers.js';
import { useChat } from './hooks/useChat.js';
import { formatTime } from './utils.js';

// Export the function to create the document editor
export function createDocumentEditor() {
    return createApp({
        setup() {

            console.log(docEditorConfig);

            // Read config from global variables
            const documentId = docEditorConfig.documentId;
            const documentTitle = ref(docEditorConfig.documentTitle);
            const canEdit = docEditorConfig.canEdit;
            const currentUser = reactive(docEditorConfig.currentUser);


            const tempDocument = ref(docEditorConfig.documentContent);


            // Get CSRF token
            const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;



            // Link copied indicator
            const linkCopied = ref(false);

            // Use composables

            // Add in the setup function

            const {
                editor,
                isTyping,
                syncOperationInProgress,
                initEditor,
                getContent,
                applyExternalChanges,
                cursorPosition,
                updateCursorPosition,
                setupCursorTracking,
                editorLoaded,
                cleanup: cleanupEditor
            } = useEditor(documentId, docEditorConfig.documentContent, canEdit, tempDocument);

            const {
                connection,
                activeCollaborators,
                cursorPositions,
                setupSignalR,
                broadcastChanges,
                broadcastCursorPosition,
                cleanup: cleanupCollaboration
            } = useCollaboration(documentId, currentUser, tempDocument);

            const {
                isSaving,
                lastSaved,
                titleChanged,
                saveDocument,
                updateDocumentTitle,
                setupAutosave,
                setTitleChanged,
                cleanup: cleanupSave
            } = useDocumentSave(documentId, canEdit);

            const {
                showExportOptions,
                exportAsPdf,
                exportAsDocx,
                printDocument
            } = useDocumentExport(documentId);

            const {
                teamMembers,
                showShareModal,
                loadTeamMembers,
                shareDocument,
                updatePermission,
                getAvatarUrl
            } = useTeamMembers(documentId, docEditorConfig.teamId, canEdit);

            const {
                chatMessages,
                newMessage,
                isChatOpen,
                unreadMessages,
                initChat,
                sendMessage,
                toggleChat,
                handleReceiveMessage,
                handleChatHistory,
                formatChatTime
            } = useChat(documentId, currentUser, connection);
            // Combined function to sync changes
            const syncChanges = async () => {
                if (!canEdit || !editor.value || syncOperationInProgress.value) return;

                syncOperationInProgress.value = true;



                try {
                    // Save document
                    await saveDocument(getContent);

                    // Broadcast changes to other users
                    await broadcastChanges(getContent());
                } finally {
                    syncOperationInProgress.value = false;
                }
            };


            // Watch cursor position changes
            watch(cursorPosition, (newPosition) => {
                if (newPosition) {
                    broadcastCursorPosition(newPosition);
                }
            });

            // Watch for document title changes
            watch(documentTitle, (newTitle, oldTitle) => {
                if (newTitle !== oldTitle) {
                    setTitleChanged(true);
                }
            });


            const userColors = {};
            const colorOptions = [
                '#FF5733', '#33FF57', '#3357FF', '#FF33E9',
                '#33FFF5', '#FFF533', '#FF5733', '#8333FF',
                '#FF8333', '#33FFBC', '#33A2FF', '#FF33A2'
            ];

            const getUserColor = (userId) => {
                if (!userColors[userId]) {
                    // Assign a random color from the options
                    const colorIndex = Object.keys(userColors).length % colorOptions.length;
                    userColors[userId] = colorOptions[colorIndex];
                }
                return userColors[userId];
            };

            const hexToRgb = (hex) => {
                // Remove the # if present
                hex = hex.replace('#', '');

                // Parse r, g, b values
                const r = parseInt(hex.substring(0, 2), 16);
                const g = parseInt(hex.substring(2, 4), 16);
                const b = parseInt(hex.substring(4, 6), 16);

                return `${r}, ${g}, ${b}`;
            };

            // Initialize components
            onMounted(async () => {
                // Initialize editor
                await initEditor(createEditor, syncChanges);

                // Set up collaboration
                await setupSignalR((content) => applyExternalChanges(content),
                    (userId, position) => {
                        // Handle cursor position update from others
                        console.log(`Cursor update from ${userId}`, position);
                    },
                    (userId, userInfo, message, timestamp) => handleReceiveMessage(userId, userInfo, message, timestamp),
                    (messages) => handleChatHistory(messages)

                );

                // Connect cursor tracking to broadcasting
                setupCursorTracking((cursorData) => {
                    broadcastCursorPosition(cursorData);
                });
                // Add to the onMounted function after setupSignalR
                await initChat();

                // Set up autosave
                setupAutosave(() => saveDocument(getContent));

                // Load team members
                await loadTeamMembers();


                // Close dropdowns when clicking outside
                document.addEventListener('click', (e) => {
                    if (!e.target.closest('.dropdown-trigger')) {
                        showExportOptions.value = false;
                    }
                });
            });

            // Clean up resources
            onBeforeUnmount(() => {
                cleanupEditor();
                cleanupCollaboration();
                cleanupSave();
            });

            // Handle document title update
            const handleDocumentTitleUpdate = async () => {
                await updateDocumentTitle(documentTitle.value);
            };

            // Handle print document
            const handlePrintDocument = () => {
                printDocument(documentTitle.value, getContent);
            };

            // Return state and methods to the template
            return {
                // State
                documentTitle,
                isSaving,
                lastSaved,
                showExportOptions,
                showShareModal,
                activeCollaborators,
                teamMembers,
                currentUser,
                linkCopied,
                editorLoaded,

                // New state for cursor tracking
                cursorPositions,
                // Methods
                syncChanges,
                saveDocument: () => saveDocument(getContent),
                updateDocumentTitle: handleDocumentTitleUpdate,
                shareDocument,
                updatePermission,
                exportAsPdf,
                exportAsDocx,
                printDocument: handlePrintDocument,
                formatTime,
                getAvatarUrl,


                //Color util
                getUserColor,
                hexToRgb,

                //Chat
                // ...existing properties
                chatMessages,
                newMessage,
                isChatOpen,
                unreadMessages,
                sendMessage,
                toggleChat,
                formatChatTime
            };
        }
    });
}