# Etapa 1: Compilación (Build)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar csproj y restaurar dependencias
COPY ["src/orders-api/OrdersApi.csproj", "src/orders-api/"]
RUN dotnet restore "src/orders-api/OrdersApi.csproj"

# Copiar el resto del código y compilar la aplicación
COPY . .
WORKDIR "/src/src/orders-api"
RUN dotnet publish "OrdersApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Ejecución (Runtime ultra liviano en Alpine)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5051
EXPOSE 5051

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OrdersApi.dll"]
