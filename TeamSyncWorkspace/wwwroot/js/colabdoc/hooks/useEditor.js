export function useEditor(documentId, content, canEdit,) {
    const { ref, markRaw } = Vue;

    const editor = ref(null);
    const isTyping = ref(false);
    const typingTimeout = ref(null);
    const syncOperationInProgress = ref(false);
    // Add editorLoaded state in the setup function
    const editorLoaded = ref(false);
    const cursorPosition = ref(null);
    const initEditor = async (createEditorFn, onSyncChanges) => {
        try {
            const editorElement = document.querySelector('#editor');
            const editorInstance = await createEditorFn("#editor", "#editor-toolbar", "#editor-menu-bar");

            // Use markRaw to prevent Vue from making the editor instance reactive
            editor.value = markRaw(editorInstance);

            // Set initial content
            editor.value.setData(content);

            if (canEdit) {
                setupEditorListeners(onSyncChanges);
            }

            // Mark editor as loaded
            editorLoaded.value = true;

            return true;
        } catch (error) {
            console.error('Error initializing editor:', error);
            setupFallbackEditor(canEdit, content);
            return false;
        }
    };

    const setupEditorListeners = (onSyncChanges) => {
        // Listen for keydown events (typing)
        editor.value.editing.view.document.on('keydown', (evt, data) => {


            // Clear previous timeout if exists
            if (!typingTimeout.value) {
                typingTimeout.value = setTimeout(() => {
                    isTyping.value = false;
                    onSyncChanges(); // Sync after typing stops
                    clearTimeout(typingTimeout.value);
                    typingTimeout.value = null;
                }, 500);
            }


            // Mark user as typing
            isTyping.value = true;


            // Set a timeout to mark user as no longer typing after 1 second of inactivity
        });

        // Listen for specific editing operations (more immediate sync)
        editor.value.model.document.on('change:data', (evt, batch) => {
            // Only sync if the change came from user actions, not from loading content
            if (batch.isUndoable && !syncOperationInProgress.value) {
                const operations = batch.operations;

                // Check if this is a significant edit that should be synced immediately
                let shouldSyncImmediately = false;

                for (const op of operations) {
                    // Sync immediately on significant changes
                    if (op.baseVersion &&
                        (op.type === 'insert' || op.type === 'remove') &&
                        (op.position?.path?.[0] !== undefined || // Paragraph level changes
                            (op.nodes && op.nodes.length > 10))) {  // Large text operations
                        shouldSyncImmediately = true;
                        break;
                    }
                }

                if (shouldSyncImmediately && !isTyping.value) {
                    onSyncChanges();
                }
            }
        });

        // Setup handlers for specific actions that should sync immediately
        const immediateActions = [
            'paste', 'imageUpload', 'mediaEmbed', 'insertTable',
            'heading', 'bulletedList', 'numberedList', 'blockQuote'
        ];

        for (const action of immediateActions) {
            if (editor.value.commands.get(action)) {
                editor.value.commands.get(action).on('execute', () => {
                    onSyncChanges();
                });
            }
        }
    };

    const setupFallbackEditor = (canEdit, content) => {
        const editorElement = document.querySelector('#editor');
        if (editorElement) {
            editorElement.innerHTML = `
        <textarea class="textarea" style="width: 100%; min-height: 400px;" 
                  ${!canEdit ? 'readonly' : ''}>
            ${content}
        </textarea>
      `;
        }
    };



    // Get cursor position data
    const updateCursorPosition = () => {
        if (!editor.value || !canEdit) return;

        const viewSelection = editor.value.editing.view.document.selection;

        // Get selection ranges
        const ranges = [];
        for (const range of viewSelection.getRanges()) {
            // Convert view range to DOM range
            const domRange = editor.value.editing.view.domConverter.viewRangeToDom(range);
            if (!domRange) continue;

            const rect = domRange.getBoundingClientRect();
            const editorElement = document.querySelector('#editor');
            const editorRect = editorElement.getBoundingClientRect();

            // Calculate relative position
            ranges.push({
                left: rect.left - editorRect.left,
                top: rect.top - editorRect.top,
                width: rect.width,
                height: rect.height
            });
        }

        // Set cursor position data
        cursorPosition.value = {
            ranges,
            timestamp: Date.now(),
            caretPosition: {
                focus: editor.value.model.document.selection.focus.toJSON(),
                anchor: editor.value.model.document.selection.anchor.toJSON()
            }
        };
    };



    const getContent = () => {
        if (!editor.value) return '';
        return editor.value.getData();
    };

    const applyExternalChanges = (content) => {
        if (!editor.value) return;

        try {
            // Set a flag to prevent our own change handlers from firing
            syncOperationInProgress.value = true;

            // Temporarily disable the change:data event to avoid recursion
            const editorElement = editor.value.editing.view.getDomRoot();
            if (editorElement) {
                const scrollPosition = editorElement.scrollTop;

                // Apply the new content
                editor.value.setData(content);

                // Restore scroll position
                editorElement.scrollTop = scrollPosition;
            }
        } catch (error) {
            console.error('Error applying external changes:', error);
        } finally {
            // Reset flag after applying changes
            syncOperationInProgress.value = false;
        }
    };

    const cleanup = () => {
        if (editor.value) {
            try {
                editor.value.destroy();
            } catch (error) {
                console.error('Error destroying editor:', error);
            }
        }

        if (typingTimeout.value) {
            clearTimeout(typingTimeout.value);
        }
    };

    return {
        editor,
        editorLoaded,
        cursorPosition,
        updateCursorPosition,
        isTyping,
        syncOperationInProgress,
        initEditor,
        getContent,
        applyExternalChanges,
        cleanup
    };
}