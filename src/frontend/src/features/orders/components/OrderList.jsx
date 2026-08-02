import React from 'react';
import { OrderStatusBadge } from './OrderStatusBadge';

export const OrderList = ({ orders, loading, error }) => {
  const formatDate = (dateStr) => {
    try {
      const date = new Date(dateStr);
      return date.toLocaleString();
    } catch {
      return dateStr;
    }
  };

  if (error) {
    return (
      <div className="card list-card">
        <h2 className="card-title">Listado de Pedidos</h2>
        <div className="error-state">
          <span className="error-state-icon">⚠️</span>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="card list-card">
      <div className="list-card-header">
        <h2 className="card-title">Listado de Pedidos</h2>
        <span className="poll-badge">Auto-refresh cada 2s</span>
      </div>

      {loading && orders.length === 0 ? (
        <div className="loading-state">
          <span className="loading-spinner"></span>
          <p>Cargando pedidos...</p>
        </div>
      ) : orders.length === 0 ? (
        <div className="empty-state">
          <span className="empty-state-icon">📦</span>
          <p>No hay pedidos registrados.</p>
        </div>
      ) : (
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Cliente</th>
                <th>SKU</th>
                <th>Cantidad</th>
                <th>Estado</th>
                <th>Fecha de Creación</th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => (
                <tr key={order.id} className="table-row">
                  <td className="font-mono text-sm" title={order.id}>
                    {order.id.substring(0, 8)}...
                  </td>
                  <td>{order.clienteNombre}</td>
                  <td>
                    <span className="sku-tag">{order.sku}</span>
                  </td>
                  <td className="text-center">{order.cantidad}</td>
                  <td>
                    <OrderStatusBadge status={order.estado} />
                  </td>
                  <td>{formatDate(order.creadoEn)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
