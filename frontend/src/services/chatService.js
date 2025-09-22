import axios from 'axios';

// Base URL of the API - will connect to the local server by default
// In production, this would be configured to point to the deployed backend
const API_URL = process.env.REACT_APP_API_URL || 'http://localhost:5000';

/**
 * Send a chat message to the API
 * @param {object} requestData - The chat request data
 * @param {string} requestData.message - The message text to send
 * @param {string} [requestData.chatId] - Existing chat ID if continuing a conversation
 * @param {boolean} [requestData.startChat=false] - Whether this is the start of a new chat
 * @returns {Promise<object>} - The response from the API with the assistant's reply
 */
export const sendMessage = async (requestData) => {
  try {
    // The .NET API expects a different structure
    const payload = {
      message: requestData.message
    };

    const response = await axios.post(`${API_URL}/api/chat`, payload);
    
    // Transform the response to match the expected format in the frontend
    return {
      chatId: requestData.chatId || Date.now().toString(), // Use existing chatId or generate a new one
      response: {
        answer: response.data.message,
        references: response.data.references?.map(ref => ({ source: ref })) || [],
        subAgentsUsed: response.data.agentsUsed || []
      }
    };
  } catch (error) {
    console.error('Error calling chat API:', error);
    throw error;
  }
};

/**
 * Add CORS headers to API requests
 */
axios.defaults.headers.common['Access-Control-Allow-Origin'] = '*';