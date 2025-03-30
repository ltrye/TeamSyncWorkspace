import EditorPositioning from '../utils/editorPositioning.js';

export function useEditor(documentId, content, canEdit, tempDocument) {
    const { ref, markRaw } = Vue;

    // STATE VARIABLES
    // ------------------------------
    const editor = ref(null);
    const isTyping = ref(false);
    const typingTimeout = ref(null);
    const syncOperationInProgress = ref(false);
    const editorLoaded = ref(false);
    const cursorPosition = ref(null);
    const previousDocumentState = ref({
        structure: null,
        selectionPaths: []
    });

    /**
     * Initializes the editor with provided factory function
     * @param {Function} createEditorFn - Factory function to create editor instance
     * @param {Function} onSyncChanges - Callback for syncing changes
     * @returns {boolean} - Success status
     */
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

    /**
     * Sets up event listeners for tracking editor changes
     * @param {Function} onSyncChanges - Callback for syncing changes
     */
    const setupEditorListeners = (onSyncChanges) => {
        setupTypingListeners(onSyncChanges);
        setupDocumentChangeListeners(onSyncChanges);
        setupActionListeners(onSyncChanges);
    };

    /**
     * Sets up listeners specifically for typing events
     * @param {Function} onSyncChanges - Callback for syncing changes
     */
    const setupTypingListeners = (onSyncChanges) => {
        // Listen for keydown events (typing)
        editor.value.editing.view.document.on('keydown', (evt, data) => {
            // Mark user as typing
            isTyping.value = true;

            // Clear previous timeout if exists
            if (!typingTimeout.value) {
                typingTimeout.value = setTimeout(() => {
                    isTyping.value = false;
                    onSyncChanges(); // Sync after typing stops
                    clearTimeout(typingTimeout.value);
                    typingTimeout.value = null;
                }, 500);
            }
        });
    };

    /**
     * Sets up listeners for document change events
     * @param {Function} onSyncChanges - Callback for syncing changes
     */
    const setupDocumentChangeListeners = (onSyncChanges) => {
        // Listen for specific editing operations (more immediate sync)
        editor.value.model.document.on('change:data', (evt, batch) => {
            // Only sync if the change came from user actions, not from loading content
            if (batch.isUndoable && !syncOperationInProgress.value) {
                const operations = batch.operations;
                console.log('Editor operations:', operations);

                if (shouldSyncImmediately(operations) && !isTyping.value) {
                    onSyncChanges();
                }
            }
        });
    };

    /**
     * Determines if changes should be synced immediately based on operations
     * @param {Array} operations - Editor operations 
     * @returns {boolean} - Whether to sync immediately
     */
    const shouldSyncImmediately = (operations) => {
        return true; // For now, always sync immediately
    };

    /**
     * Sets up listeners for specific editor actions
     * @param {Function} onSyncChanges - Callback for syncing changes
     */
    const setupActionListeners = (onSyncChanges) => {
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

    /**
     * Sets up a fallback textarea when editor initialization fails
     * @param {boolean} canEdit - Whether editing is allowed
     * @param {string} content - Initial content
     */
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

    /**
     * Updates the current cursor position for collaboration
     */
    const updateCursorPosition = () => {
        if (!editor.value || !canEdit) return;

        cursorPosition.value = EditorPositioning.updateCursorPosition(editor.value, canEdit);
    };

    /**
     * Gets the current editor content
     * @returns {string} - Editor content as HTML
     */
    const getContent = () => {
        if (!editor.value) return '';
        return editor.value.getData();
    };

    /**
     * Applies changes from external sources (other collaborators)
     * @param {object} delta - Text delta object containing changes
     */
    const applyExternalChanges = (delta) => {
        if (!editor.value || delta === null || delta === undefined) return;

        console.log('Applying external changes:', delta);
        try {
            // Set a flag to prevent our own change handlers from firing
            syncOperationInProgress.value = true;

            const editorElement = editor.value.editing.view.getDomRoot();
            if (editorElement) {
                // Save state before changes
                const scrollPosition = editorElement.scrollTop;
                const selection = editor.value.model.document.selection;
                const ranges = Array.from(selection.getRanges());
                const isBackward = selection.isBackward;

                // Apply changes to editor
                applyDeltaChanges(delta, ranges, isBackward, scrollPosition);
            }
        } catch (error) {
            console.error('Error applying external changes:', error);
        } finally {
            // Reset flag after applying changes
            syncOperationInProgress.value = false;
        }
    };

    /**
     * Applies delta changes to the editor content
     * @param {object} delta - Text delta object  
     * @param {Array} ranges - Selection ranges to restore
     * @param {boolean} isBackward - Whether selection is backward
     * @param {number} scrollPosition - Scroll position to restore
     */
    const applyDeltaChanges = (delta, ranges, isBackward, scrollPosition) => {
        // Apply the delta changes instead of replacing content
        const currentContent = editor.value.getData();
        console.log("Delta:", delta);

        // Extract delta components
        const { prefixLength, suffixLength, removed, added } = delta;
        const safeRemoved = removed || '';
        const safeAdded = added || '';

        // Generate new content by applying delta
        const newContent = generateUpdatedContent(currentContent, prefixLength, suffixLength, safeRemoved, safeAdded);

        // Apply the new content
        editor.value.setData(newContent);

        // Update temporary document if needed
        if (tempDocument) {
            updateTempDocument(delta, prefixLength, suffixLength, safeRemoved, safeAdded);
        }

        // Restore editor state using the utility
        EditorPositioning.restoreEditorState(editor.value, ranges, isBackward, scrollPosition, previousDocumentState);
    };

    /**
     * Generates updated content by applying delta to current content
     * @param {string} currentContent - Current editor content
     * @param {number} prefixLength - Length of unchanged prefix
     * @param {number} suffixLength - Length of unchanged suffix
     * @param {string} safeRemoved - Text being removed
     * @param {string} safeAdded - Text being added
     * @returns {string} - Updated content
     */
    const generateUpdatedContent = (currentContent, prefixLength, suffixLength, safeRemoved, safeAdded) => {
        if (currentContent.length === 0) {
            // If document is empty, just use the added text
            return safeAdded;
        }

        // Apply delta with bounds checking
        const boundedPrefixLength = Math.min(prefixLength, currentContent.length);
        const prefix = currentContent.substring(0, boundedPrefixLength);

        // Calculate mid section carefully
        let midStartIndex = prefixLength + safeRemoved.length;
        midStartIndex = Math.min(midStartIndex, currentContent.length);

        // Ensure bounds when calculating midLength
        const maxMidEndIndex = currentContent.length - suffixLength;
        const midEndIndex = Math.min(maxMidEndIndex, currentContent.length);
        const midLength = Math.max(0, midEndIndex - midStartIndex);

        // Debug delta application
        console.log("Content length:", currentContent.length);
        console.log("Delta application:", {
            prefixLength,
            suffixLength,
            removedLength: safeRemoved.length,
            midStartIndex,
            midEndIndex,
            midLength
        });

        // Build content in parts with careful bounds checking
        console.log("Prefix:", prefix, "Safe added:", safeAdded);
        let newContent = prefix + safeAdded;

        // Add middle portion if needed
        if (midStartIndex < currentContent.length && midLength > 0) {
            const midSection = currentContent.substring(midStartIndex, midStartIndex + midLength);
            newContent += midSection;
        }

        // Add suffix if needed
        if (suffixLength > 0 && currentContent.length >= suffixLength) {
            const suffixStart = Math.max(0, currentContent.length - suffixLength);
            const suffix = currentContent.substring(suffixStart);
            newContent += suffix;
        }

        console.log("Original length:", currentContent.length, "New length:", newContent.length);
        return newContent;
    };

    /**
     * Updates the temporary document with the same delta
     * @param {object} delta - Text delta object
     * @param {number} prefixLength - Length of unchanged prefix
     * @param {number} suffixLength - Length of unchanged suffix
     * @param {string} safeRemoved - Text being removed
            midEndIndex,
            midLength
        });

        // Build content in parts with careful bounds checking
        console.log("Prefix:", prefix, "Safe added:", safeAdded);
        let newContent = prefix + safeAdded.trim();

        // Add middle portion if needed
        if (midStartIndex < currentContent.length && midLength > 0) {
            const midSection = currentContent.substring(midStartIndex, midStartIndex + midLength);
            newContent += midSection;
        }

        // Add suffix if needed
        if (suffixLength > 0 && currentContent.length >= suffixLength) {
            const suffixStart = Math.max(0, currentContent.length - suffixLength);
            const suffix = currentContent.substring(suffixStart);
            newContent += suffix;
        }

        console.log("Original length:", currentContent.length, "New length:", newContent.length);
        return newContent;
    };

    /**
     * Updates the temporary document with the same delta
     * @param {object} delta - Text delta object
     * @param {number} prefixLength - Length of unchanged prefix
     * @param {number} suffixLength - Length of unchanged suffix
     * @param {string} safeRemoved - Text being removed
     * @param {string} safeAdded - Text being added
     */
    const updateTempDocument = (delta, prefixLength, suffixLength, safeRemoved, safeAdded) => {
        // Apply delta to the temporary document instead
        // This ensures the server and client stay in sync
        const tempContent = tempDocument.value || '';

        // Generate updated temp content
        const updatedTempContent = generateUpdatedContent(
            tempContent, prefixLength, suffixLength, safeRemoved, safeAdded
        );

        console.log("Temp document - Original:", tempContent.length, "Updated:", updatedTempContent.length);

        // Update the temporary document
        tempDocument.value = updatedTempContent;
    };

    const restoreEditorState = (ranges, isBackward, scrollPosition) => {
        // Restore selection if we had any ranges
        if (ranges.length) {
            editor.value.model.change(writer => {
                try {
                    // Analyze current document structure
                    const root = editor.value.model.document.getRoot();
                    const currentDocStructure = analyzeDocumentStructure(root);

                    // Store ranges to track paragraph positions
                    const selectionPaths = ranges.map(range => ({
                        startPath: [...range.start.path],
                        endPath: [...range.end.path]
                    }));

                    // Compare with previous structure to detect inserted paragraphs
                    let insertedParas = 0;
                    let insertionPoint = -1;

                    if (previousDocumentState.value.structure) {
                        const prevStructure = previousDocumentState.value.structure;
                        const prevPaths = previousDocumentState.value.selectionPaths;

                        // Determine if paragraphs were inserted and where
                        if (currentDocStructure.totalParagraphs > prevStructure.totalParagraphs) {
                            insertedParas = currentDocStructure.totalParagraphs - prevStructure.totalParagraphs;

                            // Try to find where paragraphs were inserted by comparing content
                            for (let i = 0; i < prevStructure.paragraphs.length; i++) {
                                if (i >= currentDocStructure.paragraphs.length) break;

                                // If content doesn't match, assume insertion happened here
                                if (prevStructure.paragraphs[i].text !== currentDocStructure.paragraphs[i].text) {
                                    insertionPoint = i;
                                    break;
                                }
                            }

                            // If we couldn't determine insertion point, use cursor position as hint
                            if (insertionPoint === -1 && prevPaths.length > 0) {
                                insertionPoint = prevPaths[0].startPath[0];
                            }

                            console.log("Detected paragraph insertion:", {
                                inserted: insertedParas,
                                at: insertionPoint
                            });
                        }
                    }

                    const newRanges = ranges.map(range => {
                        const startPath = [...range.start.path];
                        const endPath = [...range.end.path];

                        // If paragraphs were inserted before our position, adjust the position accordingly
                        if (insertedParas > 0 && insertionPoint >= 0) {
                            // Only adjust if our position is after the insertion point
                            if (startPath[0] >= insertionPoint) {
                                startPath[0] += insertedParas;
                            }

                            if (endPath[0] >= insertionPoint) {
                                endPath[0] += insertedParas;
                            }

                            console.log("Adjusted selection paths:", {
                                original: [range.start.path, range.end.path],
                                adjusted: [startPath, endPath]
                            });
                        }

                        // Create positions with adjusted paths, with fallback to safe positions
                        let start, end;

                        try {
                            // Try to create position directly with adjusted path
                            if (startPath[0] < root.childCount) {
                                const node = root.getChild(startPath[0]);
                                const offset = Math.min(startPath[1] || 0, node.maxOffset || 0);
                                start = writer.createPositionAt(node, offset);
                            } else {
                                // Fallback to last paragraph if index out of bounds
                                const lastNode = root.getChild(root.childCount - 1);
                                start = writer.createPositionAt(lastNode, 0);
                            }

                            if (endPath[0] < root.childCount) {
                                const node = root.getChild(endPath[0]);
                                const offset = Math.min(endPath[1] || 0, node.maxOffset || 0);
                                end = writer.createPositionAt(node, offset);
                            } else {
                                const lastNode = root.getChild(root.childCount - 1);
                                end = writer.createPositionAt(lastNode, lastNode.maxOffset || 0);
                            }
                        } catch (e) {
                            console.warn("Error creating adjusted positions:", e);
                            // Last resort fallback - create position at document start
                            start = writer.createPositionAt(root, 0);
                            end = writer.createPositionAt(root, 0);
                        }

                        return writer.createRange(start, end);
                    });

                    writer.setSelection(newRanges, { backward: isBackward });

                    // Save current state for next comparison
                    previousDocumentState.value = {
                        structure: currentDocStructure,
                        selectionPaths: selectionPaths
                    };

                    // Log restored position for debugging
                    console.log("Restored selection at:",
                        editor.value.model.document.selection.getFirstPosition().path);

                } catch (e) {
                    console.warn('Could not restore selection after content change:', e);
                }
            });
        }

        // Restore scroll position
        const editorElement = editor.value.editing.view.getDomRoot();
        if (editorElement) {
            editorElement.scrollTop = scrollPosition;
        }
    };

    /**
     * Analyzes document structure and provides paragraph information
     * @param {Object} root - Editor root node
     * @returns {Object} - Document structure information with content signatures
     */
    function analyzeDocumentStructure(root) {
        const paragraphs = [];
        let totalChars = 0;

        for (let i = 0; i < root.childCount; i++) {
            const node = root.getChild(i);
            const nodeLength = node.maxOffset || 0;
            totalChars += nodeLength;

            // Create content signature for better paragraph comparison
            const text = node.textContent || '';
            const contentSignature = text.length > 20 ?
                text.substring(0, 10) + '...' + text.substring(text.length - 10) :
                text;

            paragraphs.push({
                index: i,
                type: node.name,
                length: nodeLength,
                text: text,
                signature: contentSignature,
                startOffset: totalChars - nodeLength,
                endOffset: totalChars
            });
        }

        return {
            paragraphs,
            totalParagraphs: root.childCount,
            totalChars
        };
    }

    /**
     * Cleans up editor resources
     */
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