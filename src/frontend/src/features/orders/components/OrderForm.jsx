import React, { useState } from 'react';
import { Alert } from '../../../components/Alert';

export const OrderForm = ({ onSubmit }) => {
  const [clienteNombre, setClienteNombre] = useState('');
  const [sku, setSku] = useState('ABC-01');
  const [cantidad, setCantidad] = useState(1);
  
  const [loading, setLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState(null);
  const [successMsg, setSuccessMsg] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});

  const validate = () => {
    const errors = {};
    if (!clienteNombre.trim()) {
      errors.clienteNombre = 'El nombre del cliente es obligatorio.';
    }
    if (!sku) {
      errors.sku = 'El SKU es obligatorio.';
    }
    const cantNum = parseInt(cantidad, 10);
    if (isNaN(cantNum) || cantNum < 1 || cantNum > 100) {
      errors.cantidad = 'La cantidad debe ser un número entero entre 1 y 100.';
    }
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setErrorMsg(null);
    setSuccessMsg(null);
    setFieldErrors({});

    if (!validate()) return;

    setLoading(true);
    const result = await onSubmit({
      clienteNombre: clienteNombre.trim(),
      sku,
      cantidad: parseInt(cantidad, 10),
    });
    setLoading(false);

    if (result.success) {
      setSuccessMsg('¡Pedido creado exitosamente!');
      setClienteNombre('');
      setCantidad(1);
      setSku('ABC-01');
    } else {
      setErrorMsg(result.error);
      if (result.errors) {
        // If the backend returns detailed validation errors (RFC 7807)
        // Convert FluentValidation fields to lowercase keys for comparison
        const normalizedErrors = {};
        Object.entries(result.errors).forEach(([key, messages]) => {
          const fieldName = key.charAt(0).toLowerCase() + key.slice(1);
          normalizedErrors[fieldName] = Array.isArray(messages) ? messages[0] : messages;
        });
        setFieldErrors(normalizedErrors);
      }
    }
  };

  return (
    <div className="card form-card">
      <h2 className="card-title">Crear Nuevo Pedido</h2>
      
      {successMsg && <Alert type="success" message={successMsg} onClose={() => setSuccessMsg(null)} />}
      {errorMsg && <Alert type="error" message={errorMsg} onClose={() => setErrorMsg(null)} />}

      <form onSubmit={handleSubmit} className="order-form">
        <div className="form-group">
          <label htmlFor="clienteNombre" className="form-label">Nombre del Cliente</label>
          <input
            id="clienteNombre"
            type="text"
            className={`form-input ${fieldErrors.clienteNombre ? 'input-error' : ''}`}
            placeholder="Ej. Juan Pérez"
            value={clienteNombre}
            onChange={(e) => setClienteNombre(e.target.value)}
            disabled={loading}
          />
          {fieldErrors.clienteNombre && <span className="error-text">{fieldErrors.clienteNombre}</span>}
        </div>

        <div className="form-row">
          <div className="form-group flex-1">
            <label htmlFor="sku" className="form-label">Producto (SKU)</label>
            <select
              id="sku"
              className={`form-input ${fieldErrors.sku ? 'input-error' : ''}`}
              value={sku}
              onChange={(e) => setSku(e.target.value)}
              disabled={loading}
            >
              <option value="ABC-01">ABC-01 (10 disponibles inicialmente)</option>
              <option value="ABC-02">ABC-02 (5 disponibles inicialmente)</option>
              <option value="ABC-03">ABC-03 (Agotado inicialmente)</option>
            </select>
            {fieldErrors.sku && <span className="error-text">{fieldErrors.sku}</span>}
          </div>

          <div className="form-group flex-1">
            <label htmlFor="cantidad" className="form-label">Cantidad</label>
            <input
              id="cantidad"
              type="number"
              min="1"
              max="100"
              className={`form-input ${fieldErrors.cantidad ? 'input-error' : ''}`}
              value={cantidad}
              onChange={(e) => setCantidad(e.target.value)}
              disabled={loading}
            />
            {fieldErrors.cantidad && <span className="error-text">{fieldErrors.cantidad}</span>}
          </div>
        </div>

        <button type="submit" className="btn btn-primary btn-submit" disabled={loading}>
          {loading ? (
            <>
              <span className="spinner"></span> Guardando...
            </>
          ) : (
            'Enviar Pedido'
          )}
        </button>
      </form>
    </div>
  );
};
