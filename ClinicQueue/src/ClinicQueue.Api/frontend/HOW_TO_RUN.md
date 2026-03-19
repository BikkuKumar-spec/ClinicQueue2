# Clinic Dashboard - Quick Start

## 🚀 How to Run

1. **Start Backend** (New Terminal):
   ```bash
   cd d:\Doctor\ClinicQueue
   dotnet run
   ```
   *Keep this terminal open!*

2. **Start Frontend** (New Terminal):
   ```bash
   cd d:\Doctor\ClinicQueue\frontend
   npm run dev
   ```

3. **Open Browser**:
   [http://localhost:5173](http://localhost:5173)

## 🔑 Login Credentials
- **Username:** `assistant`
- **Password:** `clinic123`

---

## 🆘 Troubleshooting "Login Failed"

If you see "Login failed" or "Network Error", it means the **Backend Server is OFF**.

### 1. Fix: Run the Server
The dashboard needs the backend to check your password.
1. Open a **New Terminal**.
2. Run:
   ```bash
   cd d:\Doctor\ClinicQueue
   dotnet run
   ```
3. Wait until you see `Now listening on...`.
4. Try logging in again.

### 2. Verify Server is Online
Open [http://localhost:5000/swagger](http://localhost:5000/swagger).
- If it loads → Server is **ON**.
- If it fails → Server is **OFF**.

### 3. Check Credentials
- Username: `assistant` (lowercase)
- Password: `clinic123`
