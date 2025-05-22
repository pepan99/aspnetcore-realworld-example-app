FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Conduit/Conduit.csproj", "src/Conduit/"]
COPY . .

WORKDIR "/src/src/Conduit"
RUN dotnet restore "Conduit.csproj"

WORKDIR "/src"
RUN dotnet run --project build/build.csproj


FROM base AS final
WORKDIR /app
COPY --from=build /src/publish .
ENTRYPOINT ["dotnet", "Conduit.dll"]
