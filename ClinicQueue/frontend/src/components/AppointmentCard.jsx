import React from 'react';
import { Phone, CheckCircle, Calendar, UserCheck, Stethoscope, XCircle, Clock, MoreHorizontal, Info, AlertTriangle } from 'lucide-react';

const STATUS_CONFIG = {
    BOOKED: { label: 'Confirmed', style: 'bg-slate-50 text-slate-500 border-slate-100', icon: Calendar },
    ARRIVED: { label: 'Waiting', style: 'bg-orange-50 text-orange-600 border-orange-100', icon: Clock },
    IN_QUEUE: { label: 'Waiting', style: 'bg-orange-50 text-orange-600 border-orange-100', icon: Clock },
    IN_CONSULTATION: { label: 'Consulting', style: 'bg-blue-50 text-blue-600 border-blue-100', icon: Stethoscope },
    COMPLETED: { label: 'Done', style: 'bg-emerald-50 text-emerald-600 border-emerald-100', icon: CheckCircle },
    NO_SHOW: { label: 'No Show', style: 'bg-rose-50 text-rose-600 border-rose-100', icon: XCircle },
    CANCELLED: { label: 'Cancelled', style: 'bg-gray-50 text-gray-400 border-gray-100', icon: XCircle }
};

const SEVERITY_STYLE = {
    Low: 'bg-blue-50 text-blue-600 border-blue-100',
    Medium: 'bg-orange-50 text-orange-600 border-orange-100',
    High: 'bg-rose-50 text-rose-600 border-rose-100',
    Emergency: 'bg-red-600 text-white border-red-600 animate-pulse'
};

const AppointmentCard = ({ appointment, onStatusUpdate, disabled }) => {
    const config = STATUS_CONFIG[appointment.status] || STATUS_CONFIG.BOOKED;
    const StatusIcon = config.icon;

    return (
        <div className="group bg-white p-4 rounded-2xl shadow-sm border border-slate-200 hover:border-indigo-300 hover:shadow-xl hover:shadow-indigo-100/40 hover:-translate-y-1 transition-all duration-300 flex flex-col min-w-[180px] flex-1">
            {/* Patient Info */}
            <div className="flex-1 mb-3">
                <div className="flex justify-between items-start gap-2">
                    <h3 className="font-semibold text-slate-900 text-sm truncate leading-tight group-hover:text-indigo-500 transition-colors" title={appointment.patientName}>
                        {appointment.patientName}
                    </h3>
                    <MoreHorizontal size={14} className="text-slate-300 shrink-0 cursor-default" />
                </div>
                <a
                    href={`tel:+91${appointment.phoneNumber}`}
                    className="flex items-center gap-1.5 text-slate-400 hover:text-indigo-500 transition-colors truncate mt-1 group/phone"
                >
                    <div className="p-1 bg-slate-50 rounded-md group-hover/phone:bg-indigo-50 transition-colors">
                        <Phone size={10} className="shrink-0" />
                    </div>
                    <span className="text-[10px] font-bold tracking-tight">+91 {appointment.phoneNumber}</span>
                </a>
            </div>

            {/* AI Insights Section */}
            {appointment.symptomSummary && (
                <div className="mb-4 bg-slate-50 border border-slate-100 rounded-xl p-2.5 space-y-2 group/ai relative">
                    <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1.5 text-slate-500">
                            <Info size={10} className="text-indigo-500" />
                            <span className="text-[9px] font-black uppercase tracking-widest">AI Insights</span>
                        </div>
                        {appointment.symptomSeverity && (
                            <div className={`px-1.5 py-0.5 rounded-md border text-[8px] font-bold uppercase tracking-tighter ${SEVERITY_STYLE[appointment.symptomSeverity] || SEVERITY_STYLE.Low}`}>
                                {appointment.symptomSeverity}
                            </div>
                        )}
                    </div>
                    <p className="text-[11px] text-slate-600 leading-relaxed italic line-clamp-2" title={appointment.symptomSummary}>
                        "{appointment.symptomSummary}"
                    </p>
                </div>
            )}

            {/* Status Pill with Icon */}
            <div className="flex items-center justify-between gap-3 mt-auto mb-4">
                <div className={`px-2.5 py-1 rounded-full border text-[9px] font-black tracking-widest uppercase flex items-center gap-1.5 shadow-sm ${config.style}`}>
                    <StatusIcon size={10} strokeWidth={3} />
                    {config.label}
                </div>
            </div>

            {/* Actions Grid - Primary CTAs */}
            <div className="flex flex-col gap-2">
                {appointment.status === 'BOOKED' && (
                    <div className="flex flex-col gap-2">
                        <button
                            onClick={() => onStatusUpdate(appointment.appointmentId, 'arrive')}
                            disabled={disabled}
                            className="w-full h-8 bg-emerald-600 text-white border border-emerald-600 rounded-full font-black text-[9px] uppercase tracking-widest hover:bg-emerald-700 transition-all shadow-sm flex items-center justify-center gap-1.5"
                        >
                            <UserCheck size={12} strokeWidth={3} />
                            Arrive
                        </button>
                        <button
                            onClick={() => onStatusUpdate(appointment.appointmentId, 'no-show')}
                            disabled={disabled}
                            className="w-full h-8 bg-white border border-slate-200 text-slate-400 rounded-full font-black text-[9px] uppercase tracking-widest hover:bg-rose-50 hover:text-rose-600 hover:border-rose-100 transition-all shadow-sm flex items-center justify-center gap-1.5"
                        >
                            <XCircle size={10} />
                            No Show
                        </button>
                    </div>
                )}

                {['ARRIVED', 'IN_QUEUE'].includes(appointment.status) && (
                    <div className="flex gap-2">
                        <button
                            onClick={() => onStatusUpdate(appointment.appointmentId, 'start-consultation')}
                            disabled={disabled}
                            className="flex-[2] h-8 bg-blue-600 text-white border border-blue-600 rounded-full font-black text-[9px] uppercase tracking-widest hover:bg-blue-700 transition-all shadow-sm flex items-center justify-center gap-1.5"
                        >
                            <Stethoscope size={12} strokeWidth={3} />
                            Start Consult
                        </button>
                        <button
                            onClick={() => onStatusUpdate(appointment.appointmentId, 'no-show')}
                            disabled={disabled}
                            className="flex-1 h-8 bg-white border border-slate-200 text-slate-400 rounded-full font-black text-[9px] uppercase tracking-widest hover:bg-rose-50 hover:text-rose-600 hover:border-rose-100 transition-all shadow-sm flex items-center justify-center gap-1.5"
                        >
                            <XCircle size={10} />
                            No Show
                        </button>
                    </div>
                )}

                {appointment.status === 'IN_CONSULTATION' && (
                    <div className="flex flex-col gap-2">
                        <button
                            onClick={() => onStatusUpdate(appointment.appointmentId, 'complete')}
                            disabled={disabled}
                            className="w-full h-8 bg-emerald-600 text-white border border-emerald-600 rounded-full font-black text-[9px] uppercase tracking-widest hover:bg-emerald-700 transition-all shadow-sm flex items-center justify-center gap-1.5"
                        >
                            <CheckCircle size={12} strokeWidth={3} />
                            Complete
                        </button>
                        <button
                            onClick={() => onStatusUpdate(appointment.appointmentId, 'no-show')}
                            disabled={disabled}
                            className="w-full h-8 bg-white border border-slate-200 text-slate-400 rounded-full font-black text-[9px] uppercase tracking-widest hover:bg-rose-50 hover:text-rose-600 hover:border-rose-100 transition-all shadow-sm flex items-center justify-center gap-1.5"
                        >
                            <XCircle size={10} />
                            No Show
                        </button>
                    </div>
                )}
            </div>
        </div>
    );
};

export default AppointmentCard;
