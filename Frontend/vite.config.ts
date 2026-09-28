import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5172",
      "/auth": "http://localhost:5172",
      "/healthz": "http://localhost:5172"
    }
  },
  build: {
    outDir: "dist",
    sourcemap: false
  }
});
