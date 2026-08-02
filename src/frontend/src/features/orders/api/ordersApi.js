import { apiClient } from '../../../api/apiClient';

export const ordersApi = {
  async getOrders() {
    return apiClient.get('/orders');
  },

  async getOrderById(id) {
    return apiClient.get(`/orders/${id}`);
  },

  async createOrder(orderData) {
    return apiClient.post('/orders', orderData);
  },
};
