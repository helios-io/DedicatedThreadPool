#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["tests/Helios.DedicatedThreadPool.IdleCpu.Program/Helios.DedicatedThreadPool.IdleCpu.Program.csproj", "tests/Helios.DedicatedThreadPool.IdleCpu.Program/"]
COPY ["core/Helios.DedicatedThreadPool/Helios.DedicatedThreadPool.csproj", "core/Helios.DedicatedThreadPool/"]
RUN dotnet restore "tests/Helios.DedicatedThreadPool.IdleCpu.Program/Helios.DedicatedThreadPool.IdleCpu.Program.csproj"
COPY . .
WORKDIR "/src/tests/Helios.DedicatedThreadPool.IdleCpu.Program"
RUN dotnet build "Helios.DedicatedThreadPool.IdleCpu.Program.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Helios.DedicatedThreadPool.IdleCpu.Program.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Helios.DedicatedThreadPool.IdleCpu.Program.dll"]