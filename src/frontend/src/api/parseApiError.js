/**
 * Normaliza errores de validación RFC 7807 / ASP.NET ValidationProblem
 * a claves camelCase para los campos del formulario.
 */
export function normalizeFieldErrors(errors) {
  if (!errors || typeof errors !== 'object') {
    return {};
  }

  return Object.entries(errors).reduce((acc, [key, messages]) => {
    const fieldName = key.charAt(0).toLowerCase() + key.slice(1);
    const message = Array.isArray(messages) ? messages[0] : messages;
    if (message) {
      acc[fieldName] = message;
    }
    return acc;
  }, {});
}

/**
 * Construye un Error enriquecido a partir de la respuesta de error de la API.
 */
export function parseApiError(errorData, status, defaultMessage) {
  const errors = errorData.errors ?? null;
  const fieldErrors = normalizeFieldErrors(errors);
  const validationMessages = Object.values(fieldErrors);

  let message = errorData.detail || defaultMessage;

  if (status === 400 && validationMessages.length > 0) {
    message = validationMessages.join(' ');
  } else if (errorData.title && !errorData.detail) {
    message = errorData.title;
  }

  const error = new Error(message);
  error.status = status;
  error.errors = errors;
  error.fieldErrors = fieldErrors;
  return error;
}
