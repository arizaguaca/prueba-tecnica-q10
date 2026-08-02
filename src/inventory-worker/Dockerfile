# Etapa 1: Compilación (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY ["src/inventory-worker/InventoryWorker.csproj", "src/inventory-worker/"]
RUN dotnet restore "src/inventory-worker/InventoryWorker.csproj"

# Copiar el resto del código y compilar la aplicación
COPY . .
WORKDIR "/src/src/inventory-worker"
RUN dotnet publish "InventoryWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Ejecución (Runtime ultra liviano en Alpine)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InventoryWorker.dll"]
