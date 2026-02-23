# ClinicQueue Management System — Project Report

**Project Title:** Smart Clinic Queue & Appointment Management System  
**Date:** February 12, 2026  
**Technology:** ASP.NET Core 8 | React 18 | SQLite | WhatsApp Cloud API | AI (Ollama)

---

## 1. Introduction

### 1.1 Problem Statement
Traditional clinic appointment systems rely on phone calls and manual registers, leading to long patient wait times, missed appointments, scheduling conflicts, and no real-time visibility for staff. Patients lack a convenient way to book, track, or reschedule appointments without calling the clinic.

### 1.2 Objective
Build an intelligent, end-to-end clinic management system that:
- Allows patients to book appointments via **WhatsApp** (no app installation required)
- Provides **AI-powered symptom analysis** to recommend the right specialist
- Manages a **live priority queue** visible to clinic staff
- Sends **automated reminders** and **real-time notifications**
- Offers a **staff dashboard** for complete clinic operations

### 1.3 Scope
The system covers:
- Patient registration and appointment booking via WhatsApp
- AI-based symptom triage and doctor recommendation
- Real-time queue management with priority scoring
- Staff dashboard for appointment lifecycle management
- Automated notification system (reminders, queue alerts, doctor status)

---

## 2. System Architecture

### 2.1 Architecture Type
**3-Tier Client-Server Architecture** with event-driven components.

```
┌─────────────────────────────────────────────────────────────────┐
│                      PRESENTATION LAYER                         │
│                                                                 │
│   📱 WhatsApp (Patient)          🖥️ React Dashboard (Staff)     │
│   Meta Cloud API Webhook         Vite + TailwindCSS + SignalR   │
└────────────────┬────────────────────────────┬───────────────────┘
                 │                            │
                 ▼                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      APPLICATION LAYER                          │
│                     ASP.NET Core 8 Web API                      │
│                                                                 │
│  Controllers:  Auth | Dashboard | Appointments | Queue |        │
│                Doctors | WhatsApp Webhook | SymptomAnalysis      │
│                                                                 │
│  Services:     WhatsAppBotService   (State Machine)             │
│                AppointmentService   (Booking + Slots)           │
│                QueueService         (Priority Queue)            │
│                AISymptomService     (LLM Integration)           │
│                ReminderService      (Scheduled Alerts)          │
│                MetaWhatsAppService  (Cloud API Client)          │
│                DoctorService        (Status Broadcasting)       │
│                NotificationBgSvc    (Cron-based Alerts)         │
│                                                                 │
│  Infrastructure: Hangfire | SignalR Hub | JWT Auth              │
└────────────────┬────────────────────────────┬───────────────────┘
                 │                            │
                 ▼                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                        DATA LAYER                               │
│                                                                 │
│   SQLite Database (clinic_queue.db)   Ollama LLM (localhost)    │
│   6 Tables + Indexes + Migrations     AI Symptom Analysis       │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Technology Stack

| Layer | Technology | Purpose |
|-------|-----------|---------|
| Backend Framework | ASP.NET Core 8 (C#) | REST API, dependency injection, middleware |
| Frontend Framework | React 18 + Vite | Single-page application for staff |
| Styling | TailwindCSS + Lucide Icons | Responsive UI design |
| Database | SQLite + Dapper | Lightweight storage with micro-ORM |
| Authentication | JWT Bearer Tokens | Stateless API authentication |
| Real-time | SignalR WebSocket | Live dashboard updates |
| Task Scheduler | Hangfire (SQLite) | Reminder scheduling, cron jobs |
| Messaging | Meta WhatsApp Cloud API v21.0 | Patient communication channel |
| AI Engine | Ollama (Local LLM) | Symptom analysis, language detection |
| Tunneling | ngrok | Webhook exposure for development |

---

## 3. Database Design

### 3.1 Entity-Relationship Overview

```
patients (1) ──────< (N) appointments (1) ──────< (1) queue
    │                        │                         
    │                        ├──────< (N) notification_log
    │                        │
    │                        ├──────< (1) queue_position_history
    │                        │
    └──────< (N) symptom_analyses ─────> appointments (optional)
```

### 3.2 Table Definitions

**patients** — Patient registry
| Column | Type | Constraint |
|--------|------|-----------|
| id | TEXT | PRIMARY KEY |
| name | TEXT | NOT NULL |
| phone_number | TEXT | UNIQUE, NOT NULL |
| created_at | DATETIME | DEFAULT CURRENT_TIMESTAMP |
| last_visit | DATETIME | nullable |

**appointments** — Appointment records
| Column | Type | Constraint |
|--------|------|-----------|
| id | TEXT | PRIMARY KEY |
| patient_id | TEXT | FK → patients(id) |
| slot_time | DATETIME | NOT NULL |
| status | TEXT | DEFAULT 'BOOKED' |
| patient_name | TEXT | nullable |
| doctor_name | TEXT | nullable |
| specialty | TEXT | nullable |
| queue_position | INTEGER | nullable |
| reminder_sent | INTEGER | DEFAULT 0 |
| expected_to_arrive | INTEGER | DEFAULT 0 |
| created_at | DATETIME | DEFAULT CURRENT_TIMESTAMP |
| updated_at | DATETIME | DEFAULT CURRENT_TIMESTAMP |

**queue** — Live waiting queue
| Column | Type | Constraint |
|--------|------|-----------|
| id | TEXT | PRIMARY KEY |
| appointment_id | TEXT | UNIQUE, FK → appointments(id) |
| position | INTEGER | NOT NULL |
| priority_score | INTEGER | NOT NULL |
| status | TEXT | DEFAULT 'IN_QUEUE' |

**symptom_analyses** — AI analysis records
| Column | Type | Constraint |
|--------|------|-----------|
| id | TEXT | PRIMARY KEY |
| patient_id | TEXT | FK → patients(id) |
| appointment_id | TEXT | FK → appointments(id), nullable |
| original_symptoms | TEXT | NOT NULL |
| recommended_specialty | TEXT | NOT NULL |
| severity | TEXT | NOT NULL (Low/Medium/High/Emergency) |
| detected_language | TEXT | NOT NULL |
| confidence | REAL | DEFAULT 0.0 |

**notification_log** — Message audit trail
| Column | Type | Constraint |
|--------|------|-----------|
| id | TEXT | PRIMARY KEY |
| appointment_id | TEXT | FK → appointments(id) |
| phone | TEXT | NOT NULL |
| message | TEXT | NOT NULL |
| status | TEXT | DEFAULT 'pending' |

**queue_position_history** — Notification deduplication
| Column | Type | Constraint |
|--------|------|-----------|
| appointment_id | TEXT | PRIMARY KEY |
| last_notified_position | INTEGER | NOT NULL |
| next_in_queue_notified | INTEGER | DEFAULT 0 |
| arrival_reminder_sent | INTEGER | DEFAULT 0 |

---

## 4. Module-wise Functionality

### 4.1 WhatsApp Chatbot (Patient Interface)

**File:** `Services/WhatsAppBotService.cs` (565 lines)

The bot uses a **state-machine pattern** with `ConcurrentDictionary` for per-phone session tracking.

**Conversational Flow:**
```
Patient Message → Welcome Menu (3 buttons)
  ├── 🤖 AI Health Assistant → Symptom Input → AI Analysis → Recommendation
  ├── 📅 Book Manual → Name → Specialty → Doctor → Date → Slot → Confirm
  └── 📍 Queue Status → Show Position + Wait Time
```

**Supported Features:**

| Feature | Description |
|---------|------------|
| Manual Booking | Step-by-step: Name → Specialty (5 options) → Doctor → Date (7 days) → Time Slot → Confirm |
| AI-Guided Booking | Describe symptoms → AI recommends specialty → One-tap book |
| Queue Status | Real-time position, estimated wait, friendly status labels |
| Smart Rescheduling | Detects active appointment, preserves doctor/specialty, only asks new date/time |
| Quick Confirmation | Reply YES/OK/CONFIRM to confirm existing appointment |
| Tomorrow Booking | Available after 5:30 PM, shows countdown before that |
| Reminder Response | Yes (confirms attendance) / No (cancels and frees slot) |

**Slot System:**
- Operating hours: 9:00 AM – 5:30 PM
- Batch duration: 30 minutes
- Max patients per batch: 5
- Today closes after 5:30 PM for new bookings

### 4.2 AI Symptom Analysis

**File:** `Services/AISymptomService.cs` (364 lines)

| Feature | Detail |
|---------|--------|
| Multi-language Support | Auto-detects Hindi, English, etc. and responds in same language |
| Symptom Extraction | Parses free-text input into medical symptoms |
| Specialty Recommendation | Maps symptoms to: General, Dermatology, Pediatrics, Orthopedics, ENT |
| Severity Assessment | Classifies as Low, Medium, High, or Emergency |
| 3-Question Limit | AI asks max 3 diagnostic questions before recommending |
| Conversation History | Multi-turn dialogue for accurate diagnosis |
| Persistence | Saves results to `symptom_analyses` table |

### 4.3 Appointment Management

**File:** `Services/AppointmentService.cs` (405 lines)

| Function | Description |
|----------|------------|
| CreateAsync | Creates appointment, schedules Hangfire reminder, broadcasts SignalR event |
| RescheduleAsync | Updates slot_time on existing appointment |
| GetAvailableSlotsAsync | Generates 30-min slots (9 AM–5:30 PM), checks batch capacity |
| UpdateStatusAsync | Transitions with side-effects: ARRIVED auto-queues, COMPLETED removes from queue |
| GetDashboardSlotsAsync | Returns slot grid with patient details for staff |
| LinkSymptomAnalysisAsync | Links AI analysis to appointment record |

**Appointment Status Lifecycle:**
```
BOOKED → ARRIVED → IN_QUEUE → IN_CONSULTATION → COMPLETED
  ↓                                                  
NO_SHOW / CANCELLED                                  
```

### 4.4 Priority Queue System

**File:** `Services/QueueService.cs` (310 lines)

| Feature | Detail |
|---------|--------|
| Priority Scoring | Based on arrival time (ticks from slot time) |
| Auto-Add | Patient enters queue when status changes to ARRIVED |
| Auto-Remove | Removed on COMPLETED, NO_SHOW, or CANCELLED |
| Position Tracking | Real-time position with estimated wait (~10 min/patient) |
| Reordering | Recalculates all positions when queue changes |
| SignalR Broadcast | Dashboard gets instant queue updates |

### 4.5 Notification System (3 Layers)

**Layer 1 — Appointment Reminders** (`ReminderService.cs`)
- Scheduled via Hangfire when appointment is created
- Fires 10 minutes before appointment
- WhatsApp message with Yes/No interactive buttons
- YES → marks expected_to_arrive = 1
- NO → cancels appointment, frees the slot

**Layer 2 — Queue Position Alerts** (`NotificationBackgroundService.cs`)
- Hangfire cron job runs every 1 minute
- Sends "You're Next!" when patient reaches position #2
- Deduplication via `queue_position_history` table

**Layer 3 — Doctor Status Broadcasts** (`DoctorService.cs`)
- Triggered when doctor status changes to Available or On Break
- Sends WhatsApp to all patients with BOOKED/ARRIVED status for that doctor
- "Queue paused" / "Queue moving again" messages

### 4.6 Staff Dashboard (React Frontend)

**Login Page** (`pages/Login.jsx`)
- JWT authentication
- Demo credentials: `assistant` / `clinic123`
- Token stored in localStorage

**Dashboard Page** (`pages/Dashboard.jsx`)
- Auto-refreshes via SignalR events (SlotBooked, StatusChanged, QueueUpdated)
- Date picker for viewing different days
- Filter by specialty and doctor

**Components:**

| Component | Features |
|-----------|---------|
| StatsCard | KPI cards: Total Appointments, Arrived, In Queue, Completed |
| AppointmentCard | Patient info, status badge (color-coded), symptom severity, action buttons: Mark Arrived / No-Show / Start Consultation / Complete |
| QueueView | Live queue monitor, numbered position list, "Call to Cabin" button for #1 patient |
| DoctorStatusControl | Dropdown: Available / Consulting / On Break with status broadcast |

### 4.7 Authentication & Security

| Feature | Implementation |
|---------|---------------|
| JWT Tokens | 8-hour expiration, HS256 signing |
| Protected API | Dashboard endpoints use `[Authorize]` attribute |
| Frontend Guard | `ProtectedRoute` checks localStorage token |
| CORS Policy | Restricted to localhost:3000 and localhost:5173 |
| Webhook Security | HMAC-SHA256 signature verification |

---

## 5. API Endpoints

| # | Endpoint | Method | Auth | Purpose |
|---|----------|--------|------|---------|
| 1 | `/api/auth/login` | POST | ❌ | Staff login, returns JWT |
| 2 | `/api/whatsapp/webhook` | GET | ❌ | Meta webhook verification |
| 3 | `/api/whatsapp/webhook` | POST | ❌ | Receive WhatsApp messages |
| 4 | `/api/dashboard/appointments/today` | GET | ✅ | Today's appointments |
| 5 | `/api/dashboard/appointments/{id}/arrive` | PATCH | ✅ | Mark patient arrived |
| 6 | `/api/dashboard/appointments/{id}/no-show` | PATCH | ✅ | Mark no-show |
| 7 | `/api/dashboard/appointments/{id}/start-consultation` | PATCH | ✅ | Begin consultation |
| 8 | `/api/dashboard/appointments/{id}/complete` | PATCH | ✅ | Complete + auto-call next |
| 9 | `/api/dashboard/slots` | GET | ✅ | Slot grid with patients |
| 10 | `/api/dashboard/queue/live` | GET | ✅ | Live queue view |
| 11 | `/api/appointments` | POST | ❌ | Create appointment |
| 12 | `/api/appointments/{id}` | GET | ❌ | Get appointment |
| 13 | `/api/appointments/slots/available` | GET | ❌ | Available slots |
| 14 | `/api/appointments/{id}` | PATCH | ✅ | Update status |
| 15 | `/api/queue/add` | POST | ✅ | Add to queue |
| 16 | `/api/queue/{id}` | DELETE | ✅ | Remove from queue |
| 17 | `/api/queue/current` | GET | ❌ | Current queue |
| 18 | `/api/queue/reorder` | POST | ✅ | Reorder queue |
| 19 | `/api/queue/position/{id}` | GET | ❌ | Get position |
| 20 | `/api/doctors/{id}/status` | GET/PUT | ❌ | Doctor status |
| 21 | `/api/symptomanalysis/analyze` | POST | ❌ | AI symptom analysis |

---

## 6. Project Structure

```
ClinicQueueProject/
└── ClinicQueue/
    └── ClinicQueue/
        ├── Program.cs                           # Entry point, DI, middleware
        ├── appsettings.json                     # Configuration
        ├── Controllers/                         # 8 API controllers
        │   ├── AuthController.cs
        │   ├── AppointmentsController.cs
        │   ├── DashboardController.cs
        │   ├── DoctorsController.cs
        │   ├── PatientsController.cs
        │   ├── QueueController.cs
        │   ├── SymptomAnalysisController.cs
        │   └── WhatsAppWebhookController.cs
        ├── Services/                            # 14 business logic services
        │   ├── WhatsAppBotService.cs            # 565 lines
        │   ├── AppointmentService.cs            # 405 lines
        │   ├── QueueService.cs                  # 310 lines
        │   ├── AISymptomService.cs              # 364 lines
        │   ├── MetaWhatsAppService.cs           # 430 lines
        │   ├── ReminderService.cs               # 212 lines
        │   ├── DoctorService.cs                 # 117 lines
        │   ├── PatientService.cs                # 78 lines
        │   ├── NotificationBackgroundService.cs # 96 lines
        │   └── (+ interfaces & cron jobs)
        ├── Data/
        │   └── DatabaseService.cs               # Schema + migrations
        ├── Hubs/
        │   └── DashboardHub.cs                  # SignalR events
        ├── Shared/
        │   ├── Models/                          # 5 domain entities
        │   ├── DTOs/                            # Request/Response objects
        │   └── Utilities/                       # String extensions
        └── frontend/                            # React SPA
            └── src/
                ├── App.jsx                      # Router + auth guard
                ├── pages/
                │   ├── Login.jsx
                │   └── Dashboard.jsx            # 301 lines
                └── components/
                    ├── AppointmentCard.jsx       # 144 lines
                    ├── QueueView.jsx             # 97 lines
                    ├── StatsCard.jsx
                    └── DoctorStatusControl.jsx   # 120 lines
```

---

## 7. How to Run

1. **Backend:** `dotnet run` from `ClinicQueue/ClinicQueue/`
2. **Frontend:** `npm run dev` from `ClinicQueue/ClinicQueue/frontend/`
3. **Tunnel:** `ngrok http 5000` for WhatsApp webhook
4. **AI:** Start Ollama with `ollama serve` (i use the model qwen2.5:3b-instruct)

---

## 8. Conclusion

The ClinicQueue system provides a complete, production-ready solution for clinic appointment and queue management. Key innovations include:

- **Zero-install patient interface** via WhatsApp (3B+ users worldwide)
- **AI-powered triage** reducing specialist mismatch
- **Real-time queue visibility** for both patients and staff
- **Automated 3-layer notifications** eliminating manual follow-ups
- **Smart rescheduling** preserving appointment context

The modular architecture with clear separation of concerns (Controllers → Services → Data) ensures maintainability and extensibility.
