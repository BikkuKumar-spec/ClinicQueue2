# Clinic Queue Dashboard (React + Vite)

Admin dashboard for managing the Clinic Queue System. Built with React, TailwindCSS, and Polling for real-time updates.

## 🚀 Setup & Run

### 1. Install Dependencies
```bash
cd d:\Doctor\ClinicQueue\frontend
npm install
```

### 2. Start Development Server
```bash
npm run dev
```

The app will run at: **http://localhost:5173**

### 3. Login Credentials
**Username:** `assistant`  
**Password:** `clinic123`

## 🏗️ Project Structure

```
src/
├── components/
│   ├── AppointmentCard.jsx  # Appointment item with action buttons
│   ├── QueueView.jsx        # Live priority queue visualization
│   └── StatsCard.jsx        # Top summary statistics
├── pages/
│   ├── Dashboard.jsx        # Main admin view (Polling enabled)
│   └── Login.jsx            # JWT Authentication page
├── App.jsx                  # Routes & Auth Guard
└── index.css                # Tailwind imports
```

## 🔌 API Integration

The frontend connects to the backend running on port 5000.
(Configured in `vite.config.js` via proxy)

```javascript
// vite.config.js
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5000',
      changeOrigin: true
    }
  }
}
```

## ✨ Features

- **Live Queue Monitoring**: See real-time priority updates.
- **Status Management**: Mark patients as Arrived, In-Consultation, or Completed.
- **Queue Reordering**: Manually trigger priority re-calculation.
- **Auto-Refresh**: Dashboard polls the API every 30 seconds.
