import React from 'react';

export const OrderStatusBadge = ({ status }) => {
  // Normalize status as string
  const statusStr = typeof status === 'number' 
    ? (status === 0 ? 'Pending' : status === 1 ? 'Confirmed' : 'Rejected')
    : String(status);

  let badgeClass = 'badge-pending';
  let statusText = 'Pendiente';
  let icon = '⏳';

  if (statusStr === 'Confirmed') {
    badgeClass = 'badge-confirmed';
    statusText = 'Confirmado';
    icon = '✅';
  } else if (statusStr === 'Rejected') {
    badgeClass = 'badge-rejected';
    statusText = 'Rechazado';
    icon = '❌';
  }

  return (
    <span className={`status-badge ${badgeClass}`}>
      <span className="badge-icon">{icon}</span>
      <span className="badge-text">{statusText}</span>
    </span>
  );
};
