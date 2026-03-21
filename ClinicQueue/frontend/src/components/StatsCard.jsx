import React from 'react';

const StatsCard = ({ title, value, icon: Icon, bgColor, iconColor }) => (
    <div className="bg-white p-5 rounded-2xl shadow-sm border border-slate-200 flex items-center gap-4 transition-all hover:border-indigo-200 group">
        <div className={`w-12 h-12 rounded-xl ${bgColor || 'bg-slate-50'} flex items-center justify-center group-hover:scale-105 transition-transform shadow-sm`}>
            <Icon size={20} className={iconColor} />
        </div>
        <div>
            <p className="text-slate-400 text-[11px] font-black uppercase tracking-widest">{title}</p>
            <p className="text-2xl font-semibold text-slate-800 tracking-tighter mt-0.5">{value}</p>
        </div>
    </div>
);

export default StatsCard;
