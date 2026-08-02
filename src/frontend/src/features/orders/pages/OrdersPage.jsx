import React from 'react';
import { useOrders } from '../hooks/useOrders';
import { OrderForm } from '../components/OrderForm';
import { OrderList } from '../components/OrderList';

export const OrdersPage = () => {
  const { orders, loading, error, addOrder } = useOrders();

  return (
    <div className="dashboard-grid">
      <div className="dashboard-sidebar">
        <OrderForm onSubmit={addOrder} />
      </div>
      <div className="dashboard-main">
        <OrderList orders={orders} loading={loading} error={error} />
      </div>
    </div>
  );
};
export default OrdersPage;
