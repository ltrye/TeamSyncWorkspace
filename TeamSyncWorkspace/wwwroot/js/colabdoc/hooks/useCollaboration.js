// Usage: useCollaboration.js is a Vue 3 composition function 
// that encapsulates the SignalR connection and collaboration logic for the document editor.
//  It provides functions to broadcast changes, cursor positions, and chat messages,
//  as well as to set up and clean up the SignalR connection. 
// The function also tracks active collaborators and their cursor positions. (Not yet)

export function useCollaboration(documentId, currentUser, tempDocument) {
    const { ref, toRaw, markRaw } = Vue;


    // SignalR connection
    const connection = ref(null);
    const activeCollaborators = ref([]);
    const cursorPositions = ref({});

    const setupSignalR = async (onExternalChanges, onCursorUpdate = null, onChatMessage, onChatHistory) => {
        try {
            // Create connection
            const signalrConnection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/document")
                .withAutomaticReconnect()
                .build();

            // Use markRaw to prevent reactivity issues
            connection.value = markRaw(signalrConnection);

            // Set up event handlers
            connection.value.on("ReceiveDocumentUpdate", (userId, content) => {
                // Don't apply our own changes
                // console.log(userId);
                // console.log(currentUser);
                if (userId === 0 || !userId) {
                    return;
                }
                if (userId !== currentUser.id) {
                    onExternalChanges(content);
                }
            });

            // Handle initial list of active users (new)
            connection.value.on("ActiveUsers", (users) => {
                if (!users || !Array.isArray(users)) {
                    console.warn("Received invalid ActiveUsers data:", users);
                    return;
                }

                console.log("Received active users:", users);

                // Only add users not already in the list
                for (const user of users) {
                    if (user && user.id &&
                        !activeCollaborators.value.some(u => u.id === user.id)) {
                        activeCollaborators.value.push({
                            ...user,
                            email: user.email || 'No email available',
                            isOnline: true, // Assume online since they are active
                            isMe: user.id === currentUser.id
                        });
                    }
                }
            });

            connection.value.on("UserJoined", (user) => {
                console.log("User joined event:", user);
                if (user && user.id !== currentUser.id &&
                    !activeCollaborators.value.some(u => u.id === user.id)) {
                    activeCollaborators.value.push({
                        ...user,
                        email: user.email || 'No email available',
                        isOnline: true
                    });
                }
            });

            connection.value.on("UserLeft", (userId) => {
                console.log("User left event:", userId);
                activeCollaborators.value = activeCollaborators.value.filter(u => u.id !== userId);

                // Remove cursor position data
                if (cursorPositions.value[userId]) {
                    const newPositions = { ...cursorPositions.value };
                    delete newPositions[userId];
                    cursorPositions.value = newPositions;
                }

            });

            // Handle cursor position updates
            connection.value.on("CursorPosition", (userId, userInfo, cursorData) => {
                if (userId !== currentUser.id) {
                    // Update or add the cursor position
                    cursorPositions.value = {
                        ...cursorPositions.value,
                        [userId]: {
                            ...JSON.parse(cursorData),
                            userInfo: userInfo
                        }
                    };
                }
            });

            // Add to useCollaboration.js in the setupSignalR function

            // Handle chat messages
            connection.value.on("ReceiveChatMessage", (userId, userInfo, message, timestamp) => {
                onChatMessage(userId, userInfo, message, timestamp);
            });

            connection.value.on("ChatHistory", (messages) => {
                onChatHistory(messages);
            });
            // Start connection
            await connection.value.start();

            // Join the document "room"
            await connection.value.invoke("JoinDocument", documentId, toRaw(currentUser));

            return true;
        } catch (error) {
            console.error('Error initializing SignalR:', error);
            return false;
        }
    };

    const broadcastChanges = async (content) => {
        if (!connection.value || connection.value.state !== "Connected") return false;


        var tempDoc = tempDocument.value;
        var currentDoc = content;

        console.log("Current Document:", currentDoc);
        console.log("Temporary Document:", tempDoc);



        // Find common prefix and suffix
        let prefixLength = 0;
        const minLength = Math.min(tempDoc.length, currentDoc.length);

        // Find common prefix
        while (prefixLength < minLength &&
            tempDoc.charAt(prefixLength) === currentDoc.charAt(prefixLength)) {
            prefixLength++;
        }

        // Find common suffix
        let tempSuffixPos = tempDoc.length - 1;
        let currentSuffixPos = currentDoc.length - 1;
        let suffixLength = 0;

        while (tempSuffixPos >= prefixLength &&
            currentSuffixPos >= prefixLength &&
            tempDoc.charAt(tempSuffixPos) === currentDoc.charAt(currentSuffixPos)) {
            suffixLength++;
            tempSuffixPos--;
            currentSuffixPos--;
        }

        // Prepare delta (what was removed, what was added)
        const removed = tempDoc.substring(prefixLength, tempDoc.length - suffixLength);
        const added = currentDoc.substring(prefixLength, currentDoc.length - suffixLength);

        const delta = {
            prefixLength: prefixLength,
            suffixLength: suffixLength,
            removed: removed,
            added: added
        };
        console.log("Delta:", delta);



        try {
            await connection.value.invoke("UpdateDocument", documentId, currentUser.id, content, delta);
            // Update the temporary document after successful broadcast
            tempDocument.value = currentDoc;
            return true;
        } catch (error) {
            console.error('Error broadcasting changes:', error);
            return false;
        }

    };



    // Broadcast cursor position to other users
    const broadcastCursorPosition = async (cursorData) => {
        if (!connection.value || connection.value.state !== "Connected") return;
        if (!cursorData) return;

        try {
            await connection.value.invoke(
                "SendCursorPosition",
                documentId,
                currentUser.id,
                currentUser,
                JSON.stringify(cursorData)
            );
        } catch (error) {
            console.error('Error broadcasting cursor position:', error);
        }
    };

    const cleanup = () => {
        if (connection.value) {
            try {
                connection.value.stop();
            } catch (error) {
                console.error('Error stopping SignalR connection:', error);
            }
        }
    };

    return {
        connection,
        broadcastCursorPosition,
        cursorPositions,
        activeCollaborators,
        setupSignalR,
        broadcastChanges,
        cleanup
    };
}