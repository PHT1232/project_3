# Frontend Context

## Architecture
The frontend is a modern Single Page Application (SPA) located in the `frontend/` directory. It is designed to be bundled and served by the backend WebApi.

## Tech Stack
- **Build Tool**: Vite (Node.js ecosystem)
- **Build Output**: Running `npm run build` generates the production assets in the `dist/` directory.

## Deployment Integration
The application uses a unified deployment model via Docker:
1. The frontend is built inside a Node.js Docker stage.
2. The resulting `dist` folder is copied directly into the .NET WebApi's `wwwroot` directory.
3. The .NET backend acts as the static file server for the frontend, ensuring both exist in a single container.
