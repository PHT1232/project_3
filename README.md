# Project 3 - Fullstack Application

This is a fullstack application built with:
- **Backend:** .NET 10 WebApi (Clean Architecture)
- **Frontend:** Vite (Node.js)
- **CI/CD:** Jenkins & Docker

## Architecture
- `Core`: Domain entities and interfaces
- `Application`: Application business logic and services
- `Infrastructure`: Data access and external services
- `WebApi`: REST API and static file serving for the SPA
- `frontend`: Vite-based single page application

## Deployment
This project is configured to be built and deployed via a Jenkins CI pipeline using Docker multi-stage builds.
