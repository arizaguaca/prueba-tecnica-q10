import { env } from '../config/env';

export const apiClient = {
  async get(endpoint) {
    const response = await fetch(`${env.API_URL}${endpoint}`, {
      headers: {
        'Accept': 'application/json',
      },
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      const error = new Error(errorData.detail || 'Ocurrió un error en el servidor.');
      error.status = response.status;
      error.errors = errorData.errors || null;
      throw error;
    }

    return response.json();
  },

  async post(endpoint, data) {
    const response = await fetch(`${env.API_URL}${endpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
      },
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      const error = new Error(errorData.detail || 'Ocurrió un error al procesar la solicitud.');
      error.status = response.status;
      error.errors = errorData.errors || null;
      throw error;
    }

    return response.json();
  },
};
