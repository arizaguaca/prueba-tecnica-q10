import React from 'react';
import { Layout } from './components/Layout';
import { OrdersPage } from './features/orders/pages/OrdersPage';

function App() {
  return (
    <Layout>
      <OrdersPage />
    </Layout>
  );
}

export default App;
