FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src ./src
RUN dotnet publish "src/Participant/SportTournaments.Participant.API/SportTournaments.Participant.API.csproj" -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /out .
ENTRYPOINT ["dotnet", "SportTournaments.Participant.API.dll"]
