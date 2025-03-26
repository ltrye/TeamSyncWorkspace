export function useChat(documentId, currentUser, connection) {
    const { ref, computed } = Vue;

    const chatMessages = ref([]);
    const newMessage = ref('');
    const isChatOpen = ref(false);
    const unreadMessages = ref(0);

    // Initialize chat and load history
    const initChat = async () => {
        if (!connection.value || connection.value.state !== "Connected") return;

        try {
            await connection.value.invoke("LoadChatHistory", documentId);
        } catch (error) {
            console.error('Error loading chat history:', error);
        }
    };

    // Send a new message
    const sendMessage = async () => {
        if (!newMessage.value.trim() || !connection.value || connection.value.state !== "Connected") return;

        try {
            await connection.value.invoke(
                "SendChatMessage",
                documentId,
                currentUser.id,
                currentUser,
                newMessage.value.trim()
            );

            // Clear the message input
            newMessage.value = '';
        } catch (error) {
            console.error('Error sending chat message:', error);
        }
    };

    // Handle receiving a new message
    const handleReceiveMessage = (userId, userInfo, message, timestamp) => {
        const isCurrentUser = userId === currentUser.id;

        chatMessages.value.push({
            id: `temp-${Date.now()}`,
            userId,
            userName: userInfo.name,
            userAvatar: userInfo.avatar,
            content: message,
            sentAt: new Date(timestamp),
            isCurrentUser
        });

        // Increment unread counter if chat is not open
        if (!isChatOpen.value && !isCurrentUser) {
            unreadMessages.value++;
        }

        // Scroll to the bottom of the chat
        setTimeout(() => {
            const chatContainer = document.querySelector('.chat-messages');
            if (chatContainer) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
        }, 50);
    };

    // Handle receiving chat history
    const handleChatHistory = (messages) => {
        if (!Array.isArray(messages)) return;

        chatMessages.value = messages.map(msg => ({
            id: msg.id,
            userId: msg.userId,
            userName: msg.userName,
            content: msg.content,
            sentAt: new Date(msg.sentAt),
            isCurrentUser: msg.userId === currentUser.id
        }));

        // Scroll to the bottom of the chat
        setTimeout(() => {
            const chatContainer = document.querySelector('.chat-messages');
            if (chatContainer) {
                chatContainer.scrollTop = chatContainer.scrollHeight;
            }
        }, 50);
    };

    // Toggle chat visibility
    const toggleChat = () => {
        isChatOpen.value = !isChatOpen.value;

        // Reset unread counter when opening chat
        if (isChatOpen.value) {
            unreadMessages.value = 0;
        }
    };

    // Format chat timestamp
    const formatChatTime = (date) => {
        if (!date) return '';

        const now = new Date();
        const messageDate = new Date(date);

        // If message is from today, show only time
        if (now.toDateString() === messageDate.toDateString()) {
            return messageDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }

        // Otherwise show date and time
        return messageDate.toLocaleDateString() + ' ' +
            messageDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    };

    return {
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
    };
}