import axios from "axios";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || "https://localhost:7065",
  headers: {
    "Content-Type": "application/json",
  },
  timeout: 5000,
});

export default api;
