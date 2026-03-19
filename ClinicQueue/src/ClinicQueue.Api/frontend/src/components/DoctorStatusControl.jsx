import React, { useState, useEffect } from 'react';
import { ChevronDown, CheckCircle, AlertCircle } from 'lucide-react';

const DoctorStatusControl = ({ doctorId, doctorName }) => {
    const [status, setStatus] = useState(0); // Default to Available
    const [isUpdating, setIsUpdating] = useState(false);
    const [toast, setToast] = useState(null);

    // Fetch status when doctorId changes
    useEffect(() => {
        const fetchStatus = async () => {
            if (!doctorId) return;
            try {
                const response = await fetch(`/api/doctors/${doctorId}/status`);
                if (response.ok) {
                    const data = await response.json();
                    if (data !== null) {
                        // VALIDATION: If DB has "Offline" (3) or invalid, default to "On Break" (2)
                        // This prevents the UI from breaking for legacy data
                        const validStatus = [0, 1, 2].includes(data) ? data : 2;
                        setStatus(validStatus);
                    }
                }
            } catch (error) {
                console.error("Failed to fetch status:", error);
            }
        };
        fetchStatus();
    }, [doctorId]);

    // Map integer status to visual properties - Removed "Offline" (3)
    const statusConfig = {
        0: { label: 'Available', color: 'bg-emerald-100 text-emerald-800 border-emerald-200', dot: 'bg-emerald-500' },
        1: { label: 'Consulting', color: 'bg-blue-100 text-blue-800 border-blue-200', dot: 'bg-blue-500' },
        2: { label: 'On Break', color: 'bg-amber-100 text-amber-800 border-amber-200', dot: 'bg-amber-500' }
    };

    const handleStatusChange = async (e) => {
        const newStatus = parseInt(e.target.value);
        setIsUpdating(true);

        try {
            const response = await fetch(`/api/doctors/${doctorId}/status`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(newStatus),
            });

            if (!response.ok) {
                throw new Error('Failed to update status');
            }

            setStatus(newStatus);
            showToast(`Status updated to ${statusConfig[newStatus].label}`, 'success');
        } catch (error) {
            console.error('Error updating status:', error);
            showToast('Failed to update status. Please try again.', 'error');
        } finally {
            setIsUpdating(false);
        }
    };

    const showToast = (message, type) => {
        setToast({ message, type });
        setTimeout(() => setToast(null), 3000); // Auto-dismiss after 3s
    };

    // Safe fallback if status is somehow still invalid
    const currentConfig = statusConfig[status] || statusConfig[2];

    return (
        <div className="relative">
            {/* Component Container */}
            <div className="bg-white rounded-2xl p-4 border border-slate-100 shadow-sm flex items-center justify-between gap-4 transition-all duration-300">

                <div className="flex items-center gap-3">
                    <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${currentConfig.color} transition-colors duration-300`}>
                        <div className={`w-3 h-3 rounded-full ${currentConfig.dot} animate-pulse`}></div>
                    </div>
                    <div>
                        <h3 className="text-xs font-black text-slate-400 uppercase tracking-widest leading-tight">Current Status</h3>
                        <p className="font-bold text-slate-700 text-sm leading-tight mt-0.5">{doctorName || "Doctor"}</p>
                    </div>
                </div>

                <div className="relative group">
                    <div className={`flex items-center gap-2 pl-3 pr-8 py-2.5 rounded-xl border font-bold text-xs uppercase tracking-wider transition-all cursor-pointer hover:shadow-md ${currentConfig.color}`}>
                        <div className={`w-2 h-2 rounded-full ${currentConfig.dot}`}></div>
                        <select
                            value={status}
                            onChange={handleStatusChange}
                            disabled={isUpdating}
                            className="appearance-none bg-transparent border-none focus:ring-0 cursor-pointer w-full absolute inset-0 opacity-0 z-10"
                        >
                            <option value={0}>Available</option>
                            <option value={1}>Consulting</option>
                            <option value={2}>On Break</option>
                        </select>
                        <span className="relative z-0 pointer-events-none">{currentConfig.label}</span>
                        <ChevronDown size={14} className="absolute right-3 top-1/2 -translate-y-1/2 opacity-50 pointer-events-none" />
                    </div>
                </div>
            </div>

            {/* Custom Inline Toast Notification */}
            {toast && (
                <div className={`absolute -bottom-12 left-1/2 -translate-x-1/2 px-4 py-2 rounded-full shadow-lg flex items-center gap-2 text-xs font-bold whitespace-nowrap animate-bounce-in z-50 ${toast.type === 'success' ? 'bg-emerald-600 text-white' : 'bg-red-500 text-white'
                    }`}>
                    {toast.type === 'success' ? <CheckCircle size={14} /> : <AlertCircle size={14} />}
                    {toast.message}
                </div>
            )}
        </div>
    );
};

export default DoctorStatusControl;
