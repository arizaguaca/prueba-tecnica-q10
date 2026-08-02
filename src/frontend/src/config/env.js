const apiUrl = import.meta.env.VITE_API_URL;

if (!apiUrl) {
  throw new Error(
    'VITE_API_URL no está configurada. Copia .env.example a .env en src/frontend/ o define la variable de entorno.'
  );
}

export const env = {
  API_URL: apiUrl,
};
