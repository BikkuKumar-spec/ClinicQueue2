import React, { useState, useEffect, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import axios from 'axios';
import * as signalR from '@microsoft/signalr';
import {
    LogOut, Users, Clock, CalendarCheck, Calendar,
    RefreshCw, Stethoscope, User as UserIcon,
    LayoutDashboard, ChevronRight
} from 'lucide-react';
import StatsCard from '../components/StatsCard';
import AppointmentCard from '../components/AppointmentCard';
import DoctorStatusControl from '../components/DoctorStatusControl';
import QueueView from '../components/QueueView';

export default function Dashboard() {
    const [slots, setSlots] = useState([]);
    const [queue, setQueue] = useState([]);
    const [selectedDate, setSelectedDate] = useState(new Date().toISOString().split('T')[0]);
    const [selectedSpecialty, setSelectedSpecialty] = useState('');
    const [selectedDoctor, setSelectedDoctor] = useState('');
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [processing, setProcessing] = useState(false);
    const [isQueueVisible, setIsQueueVisible] = useState(true);
    const navigate = useNavigate();

    const fetchData = async () => {
        try {
            const token = localStorage.getItem('token');
            const headers = { Authorization: `Bearer ${token}` };
            const url = `/api/dashboard/slots?date=${selectedDate}${selectedSpecialty ? `&specialty=${selectedSpecialty}` : ''}${selectedDoctor ? `&doctorName=${selectedDoctor}` : ''}`;
            const queueUrl = `/api/dashboard/queue/live${selectedDoctor ? `?doctorName=${encodeURIComponent(selectedDoctor)}` : ''}`;

            const [slotsRes, queueRes] = await Promise.all([
                axios.get(url, { headers }),
                axios.get(queueUrl, { headers })
            ]);

            setSlots(slotsRes.data);
            setQueue(queueRes.data);
            setError(null);
        } catch (error) {
            console.error(error);
            setError(error.response?.data?.message || error.message || "Failed to connect to server");
            if (error.response?.status === 401) handleLogout();
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchData();
    }, [selectedDate, selectedSpecialty, selectedDoctor]);

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/dashboard')
            .withAutomaticReconnect()
            .build();

        connection.on('SlotBooked', fetchData);
        connection.on('StatusChanged', fetchData);
        connection.on('QueueUpdated', fetchData);

        connection.start().catch(err => console.error('SignalR error:', err));
        const interval = setInterval(fetchData, 60000);
        return () => { clearInterval(interval); connection.stop(); };
    }, []);

    const handleStatusUpdate = async (id, action) => {
        if (processing) return;
        setProcessing(true);
        try {
            const token = localStorage.getItem('token');
            await axios.patch(`/api/dashboard/appointments/${id}/${action}`, {}, {
                headers: { Authorization: `Bearer ${token}` }
            });
            await fetchData();
        } catch (error) {
            alert(error.response?.data?.message || "Update failed");
        } finally {
            setProcessing(false);
        }
    };

    const handleLogout = () => {
        localStorage.removeItem('token');
        localStorage.removeItem('username');
        navigate('/login');
    };

    const groupedData = useMemo(() => {
        const groups = {};
        slots.forEach(batch => {
            if (!batch.patients || batch.patients.length === 0) return;
            batch.patients.forEach(patient => {
                const spec = patient.specialty || "General";
                const doc = patient.doctorName || "Unknown";
                const timeStr = batch.time;
                if (!groups[spec]) groups[spec] = {};
                if (!groups[spec][doc]) groups[spec][doc] = {};
                if (!groups[spec][doc][timeStr]) groups[spec][doc][timeStr] = [];
                groups[spec][doc][timeStr].push(patient);
            });
        });
        return groups;
    }, [slots]);

    const SPECIALTIES = ["General", "Dermatology", "Pediatrics", "Orthopedics", "ENT"];
    const DOCTORS_MAP = {
        "General": ["Dr. Sharma", "Dr. Verma"],
        "Dermatology": ["Dr. Mehta"],
        "Pediatrics": ["Dr. Gupta"],
        "Orthopedics": ["Dr. Rao"],
        "ENT": ["Dr. Singh"]
    };

    const availableDoctors = selectedSpecialty ? DOCTORS_MAP[selectedSpecialty] || [] : Object.values(DOCTORS_MAP).flat();
    const allPatients = slots.flatMap(slot => slot.patients || []);
    const stats = {
        total: allPatients.length,
        waiting: queue.length,
        completed: allPatients.filter(p => p.status === 'COMPLETED').length,
        noShow: allPatients.filter(p => p.status === 'NO_SHOW').length
    };

    const formatTimeBatch = (timeStr) => {
        const start = new Date(timeStr);
        const end = new Date(start.getTime() + 30 * 60000);
        return `${start.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} - ${end.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
    };

    return (
        <div className="min-h-screen bg-slate-50 flex flex-col">
            <header className="sticky top-0 z-50 bg-white/80 backdrop-blur-md border-b border-slate-200">
                <div className="px-6 py-2.5 flex justify-between items-center border-b border-slate-100">
                    <div className="flex items-center gap-2">
                        <div className="w-8 h-8 bg-indigo-500 rounded-lg flex items-center justify-center text-white shadow-sm shadow-indigo-100">
                            <LayoutDashboard size={20} />
                        </div>
                        <h1 className="text-xl font-black text-slate-900 tracking-tight">Clinic<span className="text-indigo-500">Queue</span></h1>
                    </div>

                    <div className="flex items-center gap-4">
                        <button
                            onClick={() => setIsQueueVisible(!isQueueVisible)}
                            className={`flex items-center gap-2 px-3 py-1.5 rounded-lg font-bold text-xs transition-all ${isQueueVisible ? 'bg-indigo-500 text-white shadow-md shadow-indigo-100' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                                }`}
                        >
                            <Users size={14} />
                            {isQueueVisible ? 'Hide Queue' : 'View Queue'}
                        </button>

                        <button onClick={handleLogout} className="flex items-center gap-2 text-slate-600 hover:text-indigo-500 font-bold px-3 py-1.5 rounded-lg transition-all text-xs group">
                            <UserIcon size={14} className="group-hover:scale-110 transition-transform" />
                            <span>Staff Logout</span>
                        </button>
                    </div>
                </div>

                <div className="px-6 py-2 flex items-center gap-3">
                    <div className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1.5 focus-within:border-indigo-300 transition-colors">
                        <Stethoscope size={14} className="text-slate-400" />
                        <select
                            value={selectedSpecialty}
                            onChange={(e) => { setSelectedSpecialty(e.target.value); setSelectedDoctor(''); }}
                            className="bg-transparent border-none outline-none text-xs font-bold text-slate-700 cursor-pointer"
                        >
                            <option value="">All Specialists</option>
                            {SPECIALTIES.map(s => <option key={s} value={s}>{s}</option>)}
                        </select>
                    </div>

                    <div className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1.5 focus-within:border-indigo-300 transition-colors">
                        <UserIcon size={14} className="text-slate-400" />
                        <select
                            value={selectedDoctor}
                            onChange={(e) => setSelectedDoctor(e.target.value)}
                            className="bg-transparent border-none outline-none text-xs font-bold text-slate-700 cursor-pointer"
                        >
                            <option value="">Specific Doctor</option>
                            {availableDoctors.map(d => <option key={d} value={d}>{d}</option>)}
                        </select>
                    </div>

                    <div className="flex items-center gap-2 bg-slate-50 border border-slate-200 rounded-lg px-2.5 py-1.5 focus-within:border-indigo-300 transition-colors">
                        <Calendar size={14} className="text-slate-400" />
                        <input
                            type="date"
                            value={selectedDate}
                            onChange={(e) => setSelectedDate(e.target.value)}
                            className="bg-transparent border-none outline-none text-xs font-bold text-slate-700 w-28 cursor-pointer"
                        />
                    </div>

                    <button onClick={fetchData} className="p-1.5 bg-white border border-slate-200 rounded-lg text-slate-400 hover:text-indigo-500 transition-all hover:bg-slate-50 active:scale-95">
                        <RefreshCw size={14} />
                    </button>
                </div>
            </header>

            <main className="flex-1 max-w-[1700px] mx-auto w-full px-6 py-6">
                <div className="flex gap-8 items-start">

                    <div className="flex-1 space-y-8 min-w-0">
                        {/* KPI SECTION (With tinted backgrounds) */}
                        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
                            <StatsCard title="Total Appointments" value={stats.total} icon={CalendarCheck} bgColor="bg-blue-50" iconColor="text-blue-600" />
                            <StatsCard title="In Queue" value={stats.waiting} icon={Users} bgColor="bg-orange-50" iconColor="text-orange-600" />
                            <StatsCard title="Completed" value={stats.completed} icon={Clock} bgColor="bg-emerald-50" iconColor="text-emerald-600" />
                            <StatsCard title="No Shows" value={stats.noShow} icon={LogOut} bgColor="bg-rose-50" iconColor="text-rose-600" />
                        </div>

                        <div className="space-y-12">
                            {loading ? (
                                <div className="py-20 text-center text-slate-400 animate-pulse font-bold text-sm uppercase tracking-widest">Hydrating Clinic Boards...</div>
                            ) : Object.keys(groupedData).length === 0 ? (
                                <div className="bg-white border border-slate-200 rounded-3xl p-20 text-center flex flex-col items-center gap-4">
                                    <div className="p-4 bg-slate-50 rounded-full text-slate-200">
                                        <Calendar size={48} />
                                    </div>
                                    <p className="text-slate-400 font-bold">No active appointments found for this selection.</p>
                                </div>
                            ) : (
                                Object.entries(groupedData).map(([specialty, doctors]) => (
                                    <div key={specialty} className="space-y-6">
                                        {/* ENHANCED SPECIALTY LANDMARK */}
                                        <div className="flex items-center justify-center relative">
                                            <div className="absolute inset-x-0 h-px bg-slate-200"></div>
                                            <div className="relative bg-slate-50 px-6 py-2 rounded-full border border-indigo-100 shadow-sm flex items-center gap-2 ring-4 ring-slate-50">
                                                <div className="w-2 h-2 rounded-full bg-indigo-500 animate-pulse"></div>
                                                <span className="text-[12px] font-black text-indigo-900 uppercase tracking-[0.2em] leading-none">{specialty}</span>
                                            </div>
                                        </div>

                                        <div className="space-y-6">
                                            {Object.entries(doctors).map(([doctorName, timeSlots]) => (
                                                <div key={doctorName} className="bg-white rounded-3xl border border-slate-200 shadow-sm p-6 space-y-6">
                                                    <div className="flex items-center justify-between">
                                                        <div className="flex items-center gap-2">
                                                            <div className="w-2 h-5 bg-indigo-500 rounded-full"></div>
                                                            <h3 className="text-lg font-black text-slate-800 tracking-tight">
                                                                {doctorName.startsWith('Dr.') ? doctorName : `Dr. ${doctorName}`}
                                                            </h3>
                                                        </div>
                                                        <div className="px-3 py-1 bg-slate-50 rounded-lg border border-slate-100 text-[10px] font-bold text-slate-400 uppercase tracking-wider">
                                                            Daily Grid
                                                        </div>
                                                    </div>

                                                    <div className="space-y-8">
                                                        {Object.entries(timeSlots).map(([time, patients]) => (
                                                            <div key={time} className="space-y-4">
                                                                <div className="flex items-center gap-3">
                                                                    <div className="flex items-center gap-2 bg-sky-100 text-sky-900 px-3 py-1 rounded-lg">
                                                                        <Clock size={12} className="text-sky-500" />
                                                                        <span className="text-[11px] font-black tracking-tight">{formatTimeBatch(time)}</span>
                                                                    </div>
                                                                    <div className="h-px flex-1 bg-slate-100"></div>
                                                                    {/* VISUAL OCCUPANCY DOT */}
                                                                    <div className="flex items-center gap-1.5 opacity-60">
                                                                        <div className={`w-1.5 h-1.5 rounded-full ${patients.length >= 5 ? 'bg-rose-500' : 'bg-emerald-500'}`}></div>
                                                                        <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">{patients.length}/5 Bookings</span>
                                                                    </div>
                                                                </div>

                                                                <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 2xl:grid-cols-5 gap-4">
                                                                    {patients.map(apt => (
                                                                        <AppointmentCard key={apt.appointmentId} appointment={apt} onStatusUpdate={handleStatusUpdate} disabled={processing} />
                                                                    ))}
                                                                </div>
                                                            </div>
                                                        ))}
                                                    </div>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                ))
                            )}
                        </div>
                    </div>

                    {isQueueVisible && (
                        <aside className="w-80 shrink-0 sticky top-36 max-h-[calc(100vh-180px)] overflow-hidden flex flex-col animate-in slide-in-from-right duration-500 space-y-4">
                            {/* NEW: Doctor Status Control */}
                            {selectedDoctor && (
                                <DoctorStatusControl doctorId={selectedDoctor} doctorName={selectedDoctor} />
                            )}

                            <div className="flex-1 overflow-y-auto pr-2 custom-scrollbar">
                                <QueueView queue={queue} onStatusUpdate={handleStatusUpdate} disabled={processing} />
                            </div>
                        </aside>
                    )}
                </div>
            </main>
        </div>
    );
}
