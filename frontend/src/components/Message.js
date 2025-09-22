import React from 'react';
import ReactMarkdown from 'react-markdown';
import './Message.css';

function Message({ message }) {
  const formatTime = (timestamp) => {
    return new Date(timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  return (
    <div className={`message ${message.sender} ${message.isError ? 'isError' : ''}`}>
      <div className="message-content">
        {message.sender === 'bot' ? (
          <ReactMarkdown>{message.content}</ReactMarkdown>
        ) : (
          <p>{message.content}</p>
        )}
        
        {message.references && message.references.length > 0 && (
          <div className="message-references">
            <h4>Sources:</h4>
            <ul>
              {message.references.map((ref, index) => (
                <li key={index}>{ref.source}</li>
              ))}
            </ul>
          </div>
        )}
        
        {message.subAgentsUsed && message.subAgentsUsed.length > 0 && (
          <div className="message-agents">
            <small>Agents used: {message.subAgentsUsed.join(', ')}</small>
          </div>
        )}
      </div>
      
      <div className="message-time">
        {formatTime(message.timestamp)}
      </div>
    </div>
  );
}

export default Message;