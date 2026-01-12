# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for caching
COPY ["tsgsBot-CSharp.csproj", "./"]
RUN dotnet restore "tsgsBot-CSharp.csproj"

# Copy everything else
COPY . .
WORKDIR "/src"
RUN dotnet publish "tsgsBot-CSharp.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "tsgsBot-CSharp.dll"]