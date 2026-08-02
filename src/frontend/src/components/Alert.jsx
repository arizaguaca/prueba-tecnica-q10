import React from 'react';

export const Alert = ({ type = 'info', message, onClose }) => {
  if (!message) return null;

  const baseClass = 'alert';
  const typeClass = `alert-${type}`;

  return (
    <div className={`${baseClass} ${typeClass}`}>
      <div className="alert-content">
        <span className="alert-icon">
          {type === 'error' ? '❌' : type === 'success' ? '✅' : 'ℹ️'}
        </span>
        <div className="alert-message">{message}</div>
      </div>
      {onClose && (
        <button className="alert-close" onClick={onClose} aria-label="Close alert">
          &times;
        </button>
      )}
    </div>
  );
};
