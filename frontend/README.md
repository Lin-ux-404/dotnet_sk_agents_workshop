# Healthcare Agent Frontend

This is a React frontend for the Healthcare Agent API. It provides a chat interface for interacting with the healthcare assistant.

## Setup

### Prerequisites
- [Node.js](https://nodejs.org/) (v14 or later)
- [npm](https://www.npmjs.com/) (usually comes with Node.js)

### Installation

1. Navigate to the frontend directory:
   ```
   cd frontend
   ```

2. Install dependencies:
   ```
   npm install
   ```

## Running the Application

### Option 1: Using the Provided Scripts

We've provided scripts to run both the backend and frontend together:

#### Windows:
- Double-click `run-app.bat` or run it from Command Prompt
- Or run `run-app.ps1` from PowerShell

### Option 2: Manual Start

#### Start the Backend:
1. Navigate to the project root directory
2. Run:
   ```
   cd src
   dotnet run
   ```

#### Start the Frontend:
1. In a new terminal window, navigate to the frontend directory
2. Run:
   ```
   npm start
   ```
3. The frontend will be available at http://localhost:3000

## Development Notes

- The frontend is configured to communicate with the backend API running on `http://localhost:5000`
- To change the API URL, you can set the `REACT_APP_API_URL` environment variable
- CORS is already configured in the backend to accept requests from `localhost:3000` and `localhost:3001`

## Building for Production

To create a production build:

1. Navigate to the frontend directory:
   ```
   cd frontend
   ```

2. Build the app:
   ```
   npm run build
   ```

3. The build output will be in the `frontend/build` directory, which can be served by any static file server