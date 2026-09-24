# Build from the repository root: docker build -f src/ClaimsModule.API/Dockerfile .
# (needs the whole solution in its build context, since the API project references every
# other src/ project). docker-compose.yml sets the build context accordingly.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy just the project files first so `dotnet restore` is cached across source-only changes.
COPY ClaimsModule.sln .
COPY src/ClaimsModule.Domain/ClaimsModule.Domain.csproj src/ClaimsModule.Domain/
COPY src/ClaimsModule.Application/ClaimsModule.Application.csproj src/ClaimsModule.Application/
COPY src/ClaimsModule.Persistence/ClaimsModule.Persistence.csproj src/ClaimsModule.Persistence/
COPY src/ClaimsModule.Infrastructure/ClaimsModule.Infrastructure.csproj src/ClaimsModule.Infrastructure/
COPY src/ClaimsModule.API/ClaimsModule.API.csproj src/ClaimsModule.API/

RUN dotnet restore src/ClaimsModule.API/ClaimsModule.API.csproj

COPY src/ src/

# dotnet-ef isn't part of the SDK image — installed here so the `migrations` compose service
# (which targets this `build` stage) can run `dotnet ef database update` against it.
RUN dotnet tool install --global dotnet-ef --version 9.*
ENV PATH="$PATH:/root/.dotnet/tools"

FROM build AS publish
RUN dotnet publish src/ClaimsModule.API/ClaimsModule.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 5299
ENV ASPNETCORE_URLS=http://+:5299
ENTRYPOINT ["dotnet", "ClaimsModule.API.dll"]
