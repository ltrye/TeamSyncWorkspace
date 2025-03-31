/**
 * Utility module for handling editor cursor positioning and state restoration
 */
export default {
    /**
     * Restores the editor state after content changes
     * @param {Object} editor - CKEditor instance
     * @param {Array} ranges - Selection ranges to restore
     * @param {boolean} isBackward - Whether selection is backward
     * @param {number} scrollPosition - Scroll position to restore
     * @param {Object} previousDocumentState - Reference to previous document state
     */
    restoreEditorState(editor, ranges, isBackward, scrollPosition, previousDocumentState) {
        // Restore selection if we had any ranges
        if (ranges.length) {
            editor.model.change(writer => {
                try {
                    // Analyze current document structure
                    const root = editor.model.document.getRoot();
                    const currentDocStructure = this.analyzeDocumentStructure(root);

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
                        editor.model.document.selection.getFirstPosition().path);

                } catch (e) {
                    console.warn('Could not restore selection after content change:', e);
                }
            });
        }

        // Restore scroll position
        const editorElement = editor.editing.view.getDomRoot();
        if (editorElement) {
            editorElement.scrollTop = scrollPosition;
        }
    },

    /**
     * Updates the current cursor position for collaboration
     * @param {Object} editor - CKEditor instance
     * @param {boolean} canEdit - Whether editing is allowed
     * @returns {Object|null} Cursor position information
     */
    updateCursorPosition(editor, canEdit) {
        // if (!editor || !canEdit) return null;
        console.log("Updating cursor position...");

        const viewSelection = editor.editing.view.document.selection;

        // Get selection ranges
        const ranges = [];
        for (const range of viewSelection.getRanges()) {
            // Convert view range to DOM range
            const domRange = editor.editing.view.domConverter.viewRangeToDom(range);
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

        // Return cursor position data
        return {
            ranges,
            timestamp: Date.now(),
            caretPosition: {
                focus: editor.model.document.selection.focus.toJSON(),
                anchor: editor.model.document.selection.anchor.toJSON()
            }
        };
    },

    /**
     * Analyzes document structure and provides paragraph information
     * @param {Object} root - Editor root node
     * @returns {Object} - Document structure information with content signatures
     */
    analyzeDocumentStructure(root) {
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
};