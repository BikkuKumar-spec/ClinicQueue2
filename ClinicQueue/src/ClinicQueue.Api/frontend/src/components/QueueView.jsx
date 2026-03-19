import React from 'react';
import { User, Clock, CheckCircle, Activity, ChevronRight } from 'lucide-react';

const QueueView = ({ queue, onStatusUpdate, disabled }) => {
    return (
        <div className="bg-white rounded-[2rem] shadow-sm border border-slate-200 overflow-hidden flex flex-col h-full border-b-4 border-b-indigo-500">
            {/* Header with Activity Light */}
            <div className="bg-indigo-500 px-6 py-5 flex items-center justify-between relative overflow-hidden">
                <div className="absolute top-0 left-0 w-full h-full bg-indigo-600/10"></div>
                <div className="flex items-center gap-2.5 text-white relative z-10">
                    <div className="relative">
                        <Activity size={18} className="text-white" />
                        <div className="absolute -top-1 -right-1 w-2 h-2 bg-emerald-400 rounded-full animate-ping"></div>
                    </div>
                    <h2 className="font-black text-xs uppercase tracking-[0.2em]">Monitoring</h2>
                </div>
                <div className="flex flex-col items-end relative z-10">
                    <span className="text-white font-black text-lg leading-none">{queue.length}</span>
                    <span className="text-indigo-100 text-[8px] font-bold uppercase tracking-widest mt-0.5">Active</span>
                </div>
            </div>

            <div className="p-4 space-y-4 flex-1">
                {queue.length === 0 ? (
                    <div className="py-20 px-6 text-center flex flex-col items-center gap-3">
                        <div className="w-16 h-16 bg-slate-50 rounded-full flex items-center justify-center text-slate-200">
                            <User size={32} />
                        </div>
                        <div>
                            <p className="text-slate-400 text-xs font-black uppercase tracking-widest">Floor is Clear</p>
                            <p className="text-[10px] text-slate-300 mt-1 font-medium">Waiting for new arrivals...</p>
                        </div>
                    </div>
                ) : (
                    <div className="space-y-3">
                        {queue.map((item, idx) => (
                            <div key={item.id} className={`group relative p-4 rounded-2xl border transition-all duration-300 ${idx === 0
                                ? 'bg-indigo-500 border-indigo-400 text-white shadow-lg shadow-indigo-100'
                                : 'bg-white border-slate-100 hover:border-indigo-200'
                                }`}>
                                {/* Ticket Design Accent */}
                                <div className={`absolute top-1/2 -left-1 w-2 h-4 rounded-full -translate-y-1/2 ${idx === 0 ? 'bg-indigo-500' : 'bg-slate-50'}`}></div>

                                <div className="flex justify-between items-center relative z-10">
                                    <div className="flex items-center gap-3">
                                        <div className={`w-8 h-8 rounded-xl flex items-center justify-center font-black text-xs border ${idx === 0
                                            ? 'bg-white/10 border-white/20 text-white'
                                            : 'bg-slate-50 border-slate-100 text-slate-400'
                                            }`}>
                                            {idx + 1}
                                        </div>
                                        <div>
                                            <p className={`font-black text-sm tracking-tight ${idx === 0 ? 'text-white' : 'text-slate-800'}`}>
                                                {item.patientName}
                                            </p>
                                            <div className="flex items-center gap-1.5 mt-0.5">
                                                <Clock size={10} className={idx === 0 ? 'text-indigo-200' : 'text-slate-300'} />
                                                <span className={`text-[9px] font-bold ${idx === 0 ? 'text-indigo-100' : 'text-slate-400'}`}>
                                                    Waited 12m
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                    <ChevronRight size={16} className={idx === 0 ? 'text-white/40' : 'text-slate-200'} />
                                </div>

                                {idx === 0 && (
                                    <div className="mt-4 pt-3 border-t border-white/10">
                                        <button
                                            onClick={() => onStatusUpdate(item.appointmentId, 'start-consultation')}
                                            disabled={disabled}
                                            className="w-full bg-white text-indigo-500 hover:bg-slate-50 text-[10px] py-2 rounded-xl font-black uppercase tracking-[0.2em] transition-all shadow-sm flex items-center justify-center gap-2 group-hover:scale-[1.02] active:scale-95 disabled:opacity-50"
                                        >
                                            <CheckCircle size={14} strokeWidth={3} />
                                            {disabled ? 'Calling...' : 'Call to Cabin'}
                                        </button>
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/* Sub-footer Hint */}
            <div className="px-6 py-4 bg-slate-50 border-t border-slate-100">
                <div className="flex items-center gap-2 text-[9px] text-slate-400 font-bold uppercase tracking-widest">
                    <div className="w-1.5 h-1.5 rounded-full bg-emerald-500"></div>
                    Live Auto-Sync On
                </div>
            </div>
        </div>
    );
};

export default QueueView;
