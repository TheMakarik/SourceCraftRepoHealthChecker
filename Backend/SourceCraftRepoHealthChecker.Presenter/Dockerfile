FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Backend/ ./Backend/
RUN dotnet publish Backend/SourceCraftRepoHealthChecker.Presenter/SourceCraftRepoHealthChecker.Presenter.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SourceCraftRepoHealthChecker.Presenter.dll"]
