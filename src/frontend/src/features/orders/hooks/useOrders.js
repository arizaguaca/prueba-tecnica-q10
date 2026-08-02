import { useState, useEffect, useCallback, useRef } from 'react';
import { HubConnectionBuilder, LogLevel, HttpTransportType, HubConnectionState } from '@microsoft/signalr';
import { ordersApi } from '../api/ordersApi';
import { env } from '../../../config/env';

export const useOrders = () => {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [connectionStatus, setConnectionStatus] = useState('Disconnected');

  const isFetchingRef = useRef(false);
  const connectionRef = useRef(null);

  // 1. Initial REST Fetch (Fallback & initial state)
  const fetchOrders = useCallback(async (isSilent = false) => {
    if (isFetchingRef.current) return;
    isFetchingRef.current = true;

    if (!isSilent) {
      setLoading(true);
    }

    try {
      const data = await ordersApi.getOrders();
      const sorted = [...data].sort((a, b) => new Date(b.creadoEn) - new Date(a.creadoEn));
      setOrders(sorted);
      setError(null);
    } catch (err) {
      setError(err.message || 'Error al cargar los pedidos.');
    } finally {
      setLoading(false);
      isFetchingRef.current = false;
    }
  }, []);

  // 2. Setup SignalR Real-Time Connection
  useEffect(() => {
    fetchOrders();

    const hubUrl = `${env.API_URL}/hubs/orders`;

    if (!connectionRef.current) {
      const connection = new HubConnectionBuilder()
        .withUrl(hubUrl, {
          skipNegotiation: true,
          transport: HttpTransportType.WebSockets,
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on('OrderUpdated', (updatedOrder) => {
        setOrders((prevOrders) => {
          const index = prevOrders.findIndex((o) => o.id === updatedOrder.id);
          if (index !== -1) {
            const newOrders = [...prevOrders];
            newOrders[index] = updatedOrder;
            return newOrders;
          } else {
            return [updatedOrder, ...prevOrders];
          }
        });
      });

      connection.onreconnecting(() => setConnectionStatus('Reconnecting'));
      connection.onreconnected(() => setConnectionStatus('Connected'));
      connection.onclose(() => setConnectionStatus('Disconnected'));

      connectionRef.current = connection;
    }

    const conn = connectionRef.current;

    if (conn.state === HubConnectionState.Disconnected) {
      conn
        .start()
        .then(() => setConnectionStatus('Connected'))
        .catch((err) => {
          if (!err.message?.includes('stop() was called')) {
            console.error('SignalR Connection Error: ', err);
            setConnectionStatus('Error');
          }
        });
    }

    return () => {
      // Keep connection alive across React StrictMode re-renders to prevent premature stop() calls
    };
  }, [fetchOrders]);

  // 3. Add order function
  const addOrder = useCallback(async (orderData) => {
    try {
      const newOrder = await ordersApi.createOrder(orderData);
      setOrders((prevOrders) => {
        const exists = prevOrders.some((o) => o.id === newOrder.id);
        if (exists) return prevOrders;
        return [newOrder, ...prevOrders];
      });
      return { success: true };
    } catch (err) {
      return {
        success: false,
        error: err.message || 'Error al guardar el pedido.',
        errors: err.errors || null,
      };
    }
  }, []);

  return {
    orders,
    loading,
    error,
    connectionStatus,
    refresh: fetchOrders,
    addOrder,
  };
};
