import { env } from '../config/env';
import { parseApiError } from './parseApiError';

async function handleResponse(response, defaultErrorMessage) {
  if (response.ok) {
    return response.json();
  }

  const errorData = await response.json().catch(() => ({}));
  throw parseApiError(errorData, response.status, defaultErrorMessage);
}

export const apiClient = {
  async get(endpoint) {
    const response = await fetch(`${env.API_URL}${endpoint}`, {
      headers: {
        Accept: 'application/json',
      },
    });

    return handleResponse(response, 'Ocurrió un error en el servidor.');
  },

  async post(endpoint, data) {
    const response = await fetch(`${env.API_URL}${endpoint}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify(data),
    });

    return handleResponse(response, 'Ocurrió un error al procesar la solicitud.');
  },
};
