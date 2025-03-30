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
        for (const op of operations) {
            // Sync immediately on significant changes
            if (op.baseVersion &&
                (op.type === 'insert' || op.type === 'remove') &&
                (op.position?.path?.[0] !== undefined || // Paragraph level changes
                    (op.nodes && op.nodes.length > 10))) {  // Large text operations
                return true;
            }
        }
        return false;
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

        // Restore editor state
        restoreEditorState(ranges, isBackward, scrollPosition);
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

        // If the editor content differs from the temporary document, merge them
        if (updatedTempContent !== editor.value.getData()) {
            reconcileEditorWithTempDocument(updatedTempContent);
        }
    };

    /**
     * Reconciles differences between editor and temporary document
     * @param {string} updatedTempContent - Updated temporary document content
     */
    const reconcileEditorWithTempDocument = (updatedTempContent) => {
        console.log("Selective node update from server to editor...");

        try {
            // First, validate if the HTML is well-formed
            const isValidHtml = validateHtmlStructure(updatedTempContent);

            if (!isValidHtml) {
                console.warn("HTML structure validation failed - falling back to full update");
                editor.value.setData(updatedTempContent);
                return;
            }

            // Parse HTML content into DOM structures
            const parser = new DOMParser();
            const editorDoc = parser.parseFromString(editor.value.getData(), 'text/html');
            const tempDoc = parser.parseFromString(updatedTempContent, 'text/html');

            // Get node arrays
            const editorNodes = Array.from(editorDoc.body.childNodes);
            const tempNodes = Array.from(tempDoc.body.childNodes);

            // Find modified nodes
            const nodeChanges = identifyChangedNodes(editorNodes, tempNodes);
            console.log("Node changes:", nodeChanges);

            if (nodeChanges.length === 0) {
                console.log("No specific node changes detected");
                // If we can't identify specific changes, fall back to full update
                editor.value.setData(updatedTempContent);
            } else {
                applySelectiveNodeUpdates(editorNodes, tempNodes, nodeChanges);
            }
        } catch (error) {
            console.error("Error performing selective update:", error);
            // Fallback to simple replacement if the selective update fails
            editor.value.setData(updatedTempContent);
        }
    };

    /**
     * Applies selective node updates to the editor
     * @param {Array} editorNodes - Editor DOM nodes
     * @param {Array} tempNodes - Temporary document DOM nodes
     * @param {Array} nodeChanges - List of node changes
     */
    const applySelectiveNodeUpdates = (editorNodes, tempNodes, nodeChanges) => {
        // Perform selective node updates using CKEditor API
        editor.value.model.change(writer => {
            const editorRoot = editor.value.model.document.getRoot();

            // Apply each identified change to the editor
            nodeChanges.forEach(change => {
                if (change.type === 'replace' || change.type === 'update') {
                    // Get the HTML for the updated node
                    const tempNode = tempNodes[change.tempIndex];
                    const nodeHtml = nodeToHtml(tempNode);

                    // Find the corresponding position in the editor model
                    const position = findNodePosition(editorRoot, change.editorIndex);
                    if (position) {
                        // Calculate the length of the node to replace
                        const nodeToReplace = editorNodes[change.editorIndex];
                        const endPosition = findNodeEndPosition(position, nodeToReplace);

                        if (endPosition) {
                            // Create a range covering the node to replace
                            const range = writer.createRange(position, endPosition);

                            // Remove the old content
                            writer.remove(range);

                            // Insert the new content at the same position
                            const viewFragment = editor.value.data.processor.toView(nodeHtml);
                            const modelFragment = editor.value.data.toModel(viewFragment);
                            writer.insert(modelFragment, position);
                        }
                    }
                }
            });
        });
    };

    /**
     * Restores editor state after content changes
     * @param {Array} ranges - Selection ranges to restore
     * @param {boolean} isBackward - Whether selection is backward
     * @param {number} scrollPosition - Scroll position to restore
     */
    const restoreEditorState = (ranges, isBackward, scrollPosition) => {
        // Restore selection if we had any ranges
        if (ranges.length) {
            editor.value.model.change(writer => {
                // Need to convert saved positions to new positions in the document
                try {
                    const newRanges = ranges.map(range => {
                        // Try to find equivalent positions in new document structure
                        const root = editor.value.model.document.getRoot();
                        const startPath = range.start.path;
                        const endPath = range.end.path;

                        // Create new positions based on paths (if they still exist in the document)
                        const start = writer.createPositionFromPath(root, startPath, 'toNearestPosition');
                        const end = writer.createPositionFromPath(root, endPath, 'toNearestPosition');

                        return writer.createRange(start, end);
                    });

                    writer.setSelection(newRanges, { backward: isBackward });
                } catch (e) {
                    console.warn('Could not restore selection after content change', e);
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

    // HELPER FUNCTIONS FOR NODE COMPARISON AND MANIPULATION
    // ------------------------------------------------------

    /**
     * Finds common prefix length between two node arrays
     * @param {Array} nodesA - First array of nodes
     * @param {Array} nodesB - Second array of nodes
     * @returns {number} - Common prefix length
     */
    function findCommonPrefixLength(nodesA, nodesB) {
        const maxLength = Math.min(nodesA.length, nodesB.length);
        let i = 0;

        while (i < maxLength &&
            nodesA[i].nodeType === nodesB[i].nodeType &&
            nodesA[i].nodeName === nodesB[i].nodeName &&
            nodesA[i].textContent === nodesB[i].textContent) {
            i++;
        }

        return i;
    }

    /**
     * Finds common suffix length between two node arrays
     * @param {Array} nodesA - First array of nodes
     * @param {Array} nodesB - Second array of nodes
     * @returns {number} - Common suffix length
     */
    function findCommonSuffixLength(nodesA, nodesB) {
        const maxLength = Math.min(nodesA.length, nodesB.length);
        let i = 0;

        while (i < maxLength &&
            nodesA[nodesA.length - 1 - i].nodeType === nodesB[nodesB.length - 1 - i].nodeType &&
            nodesA[nodesA.length - 1 - i].nodeName === nodesB[nodesB.length - 1 - i].nodeName &&
            nodesA[nodesA.length - 1 - i].textContent === nodesB[nodesB.length - 1 - i].textContent) {
            i++;
        }

        return i;
    }

    /**
     * Converts a DOM node to HTML string
     * @param {Node} node - DOM node
     * @returns {string} - HTML string
     */
    function nodeToHtml(node) {
        const wrapper = document.createElement('div');
        wrapper.appendChild(node.cloneNode(true));
        return wrapper.innerHTML;
    }

    /**
     * Identifies which nodes have changed between two node arrays
     * @param {Array} editorNodes - Editor DOM nodes
     * @param {Array} tempNodes - Temporary document DOM nodes
     * @returns {Array} - List of node changes
     */
    function identifyChangedNodes(editorNodes, tempNodes) {
        const changes = [];

        // Find common prefix and suffix
        const commonPrefixLength = findCommonPrefixLength(editorNodes, tempNodes);
        const remainingEditorNodes = editorNodes.slice(commonPrefixLength);
        const remainingTempNodes = tempNodes.slice(commonPrefixLength);
        const commonSuffixLength = findCommonSuffixLength(remainingEditorNodes, remainingTempNodes);

        // Calculate the changed middle section
        const editorChangeStart = commonPrefixLength;
        const editorChangeEnd = editorNodes.length - commonSuffixLength;
        const tempChangeStart = commonPrefixLength;
        const tempChangeEnd = tempNodes.length - commonSuffixLength;

        // Record each changed node
        for (let i = tempChangeStart; i < tempChangeEnd; i++) {
            // Map to the corresponding editor node if available
            const editorIndex = editorChangeStart + (i - tempChangeStart);
            if (editorIndex < editorChangeEnd) {
                changes.push({
                    type: 'update',
                    tempIndex: i,
                    editorIndex: editorIndex
                });
            } else {
                changes.push({
                    type: 'add',
                    tempIndex: i,
                    insertAfter: editorChangeEnd - 1
                });
            }
        }

        return changes;
    }

    /**
     * Finds the position of a node in the editor model
     * @param {object} root - Editor root node
     * @param {number} nodeIndex - Index of the node
     * @returns {object|null} - Position in the editor model
     */
    function findNodePosition(root, nodeIndex) {
        try {
            // Simplified approach - assuming each paragraph is a separate node
            return root.getChild(nodeIndex).getPosition(0);
        } catch (e) {
            console.warn('Could not find node position:', e);
            return null;
        }
    }

    /**
     * Finds the end position of a node
     * @param {object} startPosition - Start position of the node
     * @param {Node} node - DOM node
     * @returns {object|null} - End position in the editor model
     */
    function findNodeEndPosition(startPosition, node) {
        try {
            // Simplified approach - get approximate node length
            const nodeLength = node.textContent.length;
            return startPosition.getShiftedBy(nodeLength);
        } catch (e) {
            console.warn('Could not find node end position:', e);
            return null;
        }
    }

    /**
     * Validates HTML structure to ensure it's well-formed
     * @param {string} htmlContent - HTML content to validate
     * @returns {boolean} - Whether the HTML is valid
     */
    function validateHtmlStructure(htmlContent) {
        try {
            // 1. Check for tag balance
            const parser = new DOMParser();
            const doc = parser.parseFromString(htmlContent, 'text/html');

            // Look for parsing errors
            const parserErrors = doc.querySelectorAll('parsererror');
            if (parserErrors.length > 0) {
                console.warn("HTML parsing error detected:", parserErrors[0].textContent);
                return false;
            }

            // 2. Check for broken HTML tags in the content
            if (hasPartialTags(htmlContent)) {
                console.warn("Detected partial HTML tags in content");
                return false;
            }

            // 3. Special check for tables to ensure they're complete
            const tables = doc.querySelectorAll('table');
            for (const table of tables) {
                // Check if table has any broken structures
                if (!validateTableStructure(table)) {
                    return false;
                }
            }

            return true;
        } catch (error) {
            console.error("HTML validation error:", error);
            return false;
        }
    }

    /**
     * Checks for partial HTML tags in content
     * @param {string} html - HTML content
     * @returns {boolean} - Whether partial tags are found
     */
    function hasPartialTags(html) {
        // Check for cases where tags might be broken
        const suspiciousPatterns = [
            /< [a-z]+[^>]*$/i,           // Opening tag cut off at the end
            /^[^<]*>/i,                  // Closing tag without opening
            /<[a-z]+[^>]*$/i,            // Incomplete opening tag
            /< \/[a-z]+/i,               // Malformed closing tag
            /<[a-z]+ [^>]*[^\/]>(?![^<]*<\/)/i  // Self-closing tag missing slash
        ];

        return suspiciousPatterns.some(pattern => pattern.test(html));
    }

    /**
     * Validates table structure to ensure it's well-formed
     * @param {Element} table - Table DOM element
     * @returns {boolean} - Whether the table is valid
     */
    function validateTableStructure(table) {
        // Basic structure checks for tables
        const rows = table.querySelectorAll('tr');
        if (rows.length === 0) return false;

        // Check each row has at least one cell
        for (const row of rows) {
            const cells = row.querySelectorAll('td, th');
            if (cells.length === 0) return false;

            // Check each cell is properly closed
            for (const cell of cells) {
                if (cell.innerHTML.includes('<td') || cell.innerHTML.includes('<th') ||
                    cell.innerHTML.includes('</tr') || cell.innerHTML.includes('</table')) {
                    return false;
                }
            }
        }

        return true;
    }

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