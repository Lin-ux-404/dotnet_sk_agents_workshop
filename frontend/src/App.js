import React, { useState, useEffect } from 'react';
import './App.css';
import ChatWindow from './components/ChatWindow';
import ChatInput from './components/ChatInput';
import { sendMessage } from './services/chatService';

function App() {
  const [messages, setMessages] = useState([]);
  const [loading, setLoading] = useState(false);
  const [chatId, setChatId] = useState(null);
  
  useEffect(() => {
    // Check for existing chat ID in local storage
    const savedChatId = localStorage.getItem('healthcareChatId');
    if (savedChatId) {
      setChatId(savedChatId);
    }
  }, []);

  const handleSendMessage = async (messageText) => {
    if (!messageText.trim()) return;
    
    // Add user message to the chat
    const userMessage = {
      content: messageText,
      sender: 'user',
      timestamp: new Date()
    };
    setMessages(prevMessages => [...prevMessages, userMessage]);
    setLoading(true);
    
    try {
      // Call the API
      const response = await sendMessage({
        message: messageText,
        chatId: chatId,
        startChat: !chatId
      });
      
      // Save new chat ID if first message
      if (!chatId && response.chatId) {
        localStorage.setItem('healthcareChatId', response.chatId);
        setChatId(response.chatId);
      }
      
      // Add bot response to messages
      const botMessage = {
        content: response.response.answer,
        sender: 'bot',
        timestamp: new Date(),
        references: response.response.references,
        subAgentsUsed: response.response.subAgentsUsed
      };
      
      setMessages(prevMessages => [...prevMessages, botMessage]);
    } catch (error) {
      console.error('Error sending message:', error);
      // Add error message
      const errorMessage = {
        content: 'Sorry, there was an error processing your message. Please try again.',
        sender: 'bot',
        timestamp: new Date(),
        isError: true
      };
      setMessages(prevMessages => [...prevMessages, errorMessage]);
    }
    
    setLoading(false);
  };

  const startNewChat = () => {
    setMessages([]);
    setChatId(null);
    localStorage.removeItem('healthcareChatId');
  };

  return (
    <div className="App">
      <header className="App-header">
        <h1>Healthcare Assistant</h1>
        <button className="new-chat-btn" onClick={startNewChat}>New Chat</button>
      </header>
      <main>
        <ChatWindow messages={messages} loading={loading} />
        <ChatInput onSendMessage={handleSendMessage} disabled={loading} />
      </main>
    </div>
  );
}

export default App;