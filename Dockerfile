# One image carries the API and the pages it serves, so they share an origin.

# Node builds the frontend.
FROM node:22-slim AS frontend
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# The .NET SDK builds the API and takes the frontend into wwwroot.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS api
WORKDIR /repo
# Project files first, so a code change does not download every package again.
COPY src/TactIQ.Domain/TactIQ.Domain.csproj src/TactIQ.Domain/
COPY src/TactIQ.Application/TactIQ.Application.csproj src/TactIQ.Application/
COPY src/TactIQ.Infrastructure/TactIQ.Infrastructure.csproj src/TactIQ.Infrastructure/
COPY src/TactIQ.API/TactIQ.API.csproj src/TactIQ.API/
RUN dotnet restore src/TactIQ.API/TactIQ.API.csproj
COPY src/ src/
RUN dotnet publish src/TactIQ.API/TactIQ.API.csproj -c Release -o /publish --no-restore /p:UseAppHost=false
COPY --from=frontend /frontend/dist/ /publish/wwwroot/

# The runtime image carries only the output.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=api /publish/ ./
# Render's default port. Secrets come from the host's environment variables.
ENV ASPNETCORE_HTTP_PORTS=10000
# The free instance has a fraction of one CPU: server GC would only cost memory.
ENV DOTNET_gcServer=0
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "TactIQ.API.dll"]
