FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Cinegy.TsAnalyzer/Cinegy.TsAnalyzer.csproj", "Cinegy.TsAnalyzer/"]

RUN dotnet restore "Cinegy.TsAnalyzer/Cinegy.TsAnalyzer.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Cinegy.TsAnalyzer/Cinegy.TsAnalyzer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cinegy.TsAnalyzer/Cinegy.TsAnalyzer.csproj" -c Release --os linux -o /app/publish

FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .
ENTRYPOINT ["/app/tsanalyzer"]
