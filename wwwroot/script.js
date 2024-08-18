$(document).ready(function () {
    const username = localStorage.getItem("username");
    const activeUsers = new Map();
    const userColors = {};

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build();

    toastr.options = {
        "closeButton": true,
        "newestOnTop": true,
        "progressBar": true,
        "positionClass": "toast-top-right",
        "preventDuplicates": true,
        "timeOut": "5000",
        "extendedTimeOut": "1000",
        "showEasing": "swing",
        "hideEasing": "linear",
        "showMethod": "fadeIn",
        "hideMethod": "fadeOut"
    };

    function showNotification(message, type = 'info') {
        toastr[type](message);
    }

    function getChatHistory(userId, pageNumber, pageSize) {
        connection.invoke("GetChatHistory", userId, pageNumber, pageSize)
            .catch(err => {
                console.error("Error invoking GetChatHistory:", err.message);
                console.error("Stack trace:", err.stack);
                showNotification("Failed to load chat history. Please try again.", 'error');
            });
    }

    connection.on("ReceiveChatHistory", function (messages) {
        $('#chatroom').empty();
        if (messages.length === 0) {
            appendMessage('System', 'No messages found for the given filters.', new Date().toLocaleString());
        } else {
            messages.forEach(msg => {
                appendMessage(msg.UserId, msg.Content, msg.Timestamp);
            });
            showNotification("Messages loaded successfully.", 'success');
        }
    });

    function appendMessage(user, message, timestamp) {
        const formattedMessage = `
            <div class='message'>
                ${timestamp} - ${user}: ${message}
            </div>`;
        $('#chatroom').append(formattedMessage).scrollTop($('#chatroom')[0].scrollHeight);
    }

    // Event handler to view all messages
    $('#viewAllMessages').on('click', function () {
        getChatHistory(null, 1, 100); // null for userId will fetch all messages
    });

    connection.start().then(() => {
        console.log("Connected to SignalR hub");
    }).catch(err => {
        console.error("Error starting connection:", err.message);
        showNotification("Failed to connect to the server.", 'error');
    });

    function connectToChat(username) {
        if (connection.state === signalR.HubConnectionState.Disconnected) {
            connection.start().then(() => {
                console.log("Connected to SignalR hub");
                showNotification('Connection established.', 'success');
                connection.invoke("UserJoined", username)
                    .then(() => {
                        connection.invoke("GetUserId", username).then(userId => {
                            activeUsers.set(username, userId);
                            userColors[username.toLowerCase()] = getRandomPastelColor();
                            $('#loginBlock').hide();
                            $('#chatBody').show();
                            appendMessage('System', `Welcome, ${username}!`);
                            showNotification(`Welcome, ${username}!`, 'success');
                            updateUserList();
                            loadChatHistory(); // Load chat history on entry
                        }).catch(err => {
                            showNotification("Failed to retrieve user ID.", 'error');
                            console.error("Error invoking GetUserId:", err);
                        });
                    })
                    .catch(err => {
                        showNotification("Failed to join the chat. Please try again.", 'error');
                        console.error("Error invoking UserJoined:", err);
                    });
            }).catch(err => {
                showNotification("Failed to connect to the server.", 'error');
                console.error("Error starting connection:", err);
            });
        }
    }

    $('#sendmessage').on('click', function () {
        const message = $('#message').val().trim();
        if (message) {
            connection.invoke("SendMessage", message).then(() => {
                showNotification("Message sent successfully.", 'success');
            }).catch(err => {
                showNotification("Failed to send message. Please try again.", 'error');
                console.error(err.toString());
            });
            $('#message').val('');
        }
    });

    $('#btnRegister').on('click', function () {
        const username = $('#txtRegisterUserName').val().trim();
        const password = $('#txtRegisterPassword').val().trim();
        if (username && password) {
            $.post('/register', { username, password })
                .done(() => {
                    showNotification("Registration successful. Please log in.", 'success');
                    localStorage.setItem("username", username);
                    connectToChat(username);
                })
                .fail(xhr => {
                    let errorMessage = "Registration failed. Please try again.";
                    if (xhr.responseJSON && xhr.responseJSON.Errors) {
                        errorMessage += ` Error(s): ${xhr.responseJSON.Errors}`;
                    }
                    showNotification(errorMessage, 'error');
                });
        } else {
            showNotification("Please enter a username and password.", 'warning');
        }
    });

    $('#btnLogin').on('click', function () {
        const username = $('#txtUserName').val().trim();
        const password = $('#txtPassword').val().trim();
        if (username && password) {
            $.post('/login', { username, password })
                .done(() => {
                    showNotification("Login successful.", 'success');
                    localStorage.setItem("username", username);
                    connectToChat(username);
                })
                .fail(() => {
                    showNotification("Login failed. Please try again.", 'error');
                });
        } else {
            showNotification("Please enter a username and password.", 'warning');
        }
    });

    function loadChatHistory() {
        getChatHistory(null, 1, 100); // Load all chat history
    }

    $('#viewChatHistory').on('click', function () {
        loadChatHistory(); // Load chat history on demand
    });

    connection.on("ReceiveChatHistory", function (messages) {
        $('#chatroom').empty();
        if (messages.length === 0) {
            appendMessage('System', 'No messages found.', new Date().toLocaleString());
        } else {
            messages.forEach(msg => {
                appendMessage(msg.Username, msg.Content, msg.Timestamp);
            });
            showNotification("Messages loaded successfully.", 'success');
        }
    });

    connection.on("ReceiveMessage", function (username, message, timestamp, messageId) {
        appendMessage(username, message, timestamp, messageId);
        showNotification("New message received.", 'info');
    });

    connection.on("UserJoined", function (username, timestamp) {
        appendMessage('System', `${username} joined the chat.`, timestamp);
        connection.invoke("GetUserId", username).then((userId) => {
            activeUsers.set(username, userId); // Store the username -> userId mapping
            updateUserList();
        }).catch(err => {
            console.error("Error invoking GetUserId:", err.toString());
        });
        showNotification(`${username} joined the chat.`, 'info');
    });

    connection.on("UserLeft", function (username, timestamp) {
        appendMessage('System', `${username} left the chat.`, timestamp);
        activeUsers.delete(username);
        updateUserList();
        showNotification(`${username} left the chat.`, 'info');
    });

    connection.on("ReceiveAllUsers", function (users) {
        $('#userList').empty();
        users.forEach(user => {
            activeUsers.set(user.username, user.userId);
            userColors[user.username.toLowerCase()] = getRandomPastelColor();
            $('#userList').append(`<li class="list-group-item">${user.username}</li>`);
        });
        showNotification("User list loaded successfully.", 'success');
    });

    $('#viewAllUsers').on('click', function () {
        connection.invoke("GetAllUsers")
            .catch(err => {
                console.error("Error invoking GetAllUsers:", err.toString());
                showNotification("Failed to load user list. Please try again.", 'error');
            });
    });

    function appendMessage(user, message, timestamp, messageId) {
        const userColor = userColors[user.toLowerCase()] || getRandomPastelColor();
        userColors[user.toLowerCase()] = userColor;

        user = user || 'undefined';
        message = message || 'Empty message';
        timestamp = timestamp || 'undefined';

        const formattedMessage = `
            <div class='message' id='message-${messageId}' style='background-color:${userColor}'>
                ${timestamp} - ${user}: ${message}
            </div>`;

        $('#chatroom').append(formattedMessage).scrollTop($('#chatroom')[0].scrollHeight);
    }

    function updateUserList() {
        $('#userList').empty();
        activeUsers.forEach((userId, user) => {
            $('#userList').append(`<li class="list-group-item">${user}</li>`);
        });
    }

    if (username) {
        connectToChat(username);
        connection.invoke("GetAllUsers").catch(err => {
            console.error("Error invoking GetAllUsers:", err.toString());
        });
    } else {
        $('#loginBlock').show();
    }
});

function getRandomPastelColor() {
    const hue = Math.floor(Math.random() * 360);
    return `hsl(${hue}, 100%, 85%)`;
}