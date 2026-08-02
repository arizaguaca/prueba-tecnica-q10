import { useState, useEffect, useCallback, useRef } from 'react';
import { ordersApi } from '../api/ordersApi';

export const useOrders = () => {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  
  // Using ref to prevent calling fetch multiple times simultaneously if a request is slow
  const isFetchingRef = useRef(false);

  const fetchOrders = useCallback(async (isSilent = false) => {
    if (isFetchingRef.current) return;
    isFetchingRef.current = true;

    if (!isSilent) {
      setLoading(true);
    }
    
    try {
      const data = await ordersApi.getOrders();
      // Sort orders by CreadoEn or Date descending so newest orders appear on top
      const sorted = [...data].sort((a, b) => new Date(b.creadoEn) - new Date(a.creadoEn));
      setOrders(sorted);
      setError(null);
    } catch (err) {
      _loggerError(err);
      setError(err.message || 'Error al cargar los pedidos.');
    } finally {
      setLoading(false);
      isFetchingRef.current = false;
    }
  }, []);

  // Safe wrapper for logging
  const _loggerError = (err) => {
    // Console log is fine here since it is for debugging, but in UI we show error state
  };

  useEffect(() => {
    fetchOrders();

    // Polling setup: fetch orders silently every 2 seconds (2000 ms)
    const interval = setInterval(() => {
      fetchOrders(true);
    }, 2000);

    return () => {
      clearInterval(interval);
    };
  }, [fetchOrders]);

  const addOrder = useCallback(async (orderData) => {
    try {
      const newOrder = await ordersApi.createOrder(orderData);
      setOrders((prevOrders) => [newOrder, ...prevOrders]);
      return { success: true };
    } catch (err) {
      return { 
        success: false, 
        error: err.message || 'Error al guardar el pedido.', 
        errors: err.errors || null 
      };
    }
  }, []);

  return {
    orders,
    loading,
    error,
    refresh: fetchOrders,
    addOrder,
  };
};
