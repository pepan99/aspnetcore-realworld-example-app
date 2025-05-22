FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 5000

COPY ./publish .

ENTRYPOINT ["dotnet", "Conduit.dll"]
