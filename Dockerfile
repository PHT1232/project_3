# 1. Stage build frontend (Node.js)
FROM node:20-alpine AS build-frontend
WORKDIR /src/frontend
# Copy package files and install dependencies
COPY frontend/package*.json ./
RUN npm install
# Copy frontend source and build
COPY frontend/ ./
RUN npm run build
# (Vite mặc định build ra thư mục 'dist')

# 2. Stage build backend (.NET SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-backend
WORKDIR /src
# Copy file solution và các file csproj để restore trước (giúp cache Docker layer)
COPY Project.slnx ./
COPY Application/Application.csproj Application/
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY WebApi/WebApi.csproj WebApi/
RUN dotnet restore Project.slnx

# Copy toàn bộ mã nguồn còn lại
COPY . .
# Publish dự án WebApi
RUN dotnet publish WebApi/WebApi.csproj -c Release -o /app/publish /p:UseAppHost=false

# 3. Stage Runtime (Chạy ứng dụng)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Expose port 8080 (mặc định của .NET 8+)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Copy file chạy của Backend
COPY --from=build-backend /app/publish .
# Copy file static của Frontend vào thư mục wwwroot
COPY --from=build-frontend /src/frontend/dist ./wwwroot

ENTRYPOINT ["dotnet", "WebApi.dll"]
