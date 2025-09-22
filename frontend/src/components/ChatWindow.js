import React, { useRef, useEffect } from 'react';
import Message from './Message';
import './ChatWindow.css';

function ChatWindow({ messages, loading }) {
  const messagesEndRef = useRef(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  return (
    <div className="chat-window">
      <div className="messages-container">
        {messages.length === 0 && (
          <div className="welcome-message">
            <h2>Welcome to the Healthcare Assistant</h2>
            <p>How can I help you today?</p>
          </div>
        )}
        
        {messages.map((msg, index) => (
          <Message key={index} message={msg} />
        ))}
        
        {loading && (
          <div className="message bot">
            <div className="typing-indicator">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>
    </div>
  );
}

export default ChatWindow;