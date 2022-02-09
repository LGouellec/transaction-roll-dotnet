FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["*", "consumer/"]
RUN dotnet restore "consumer/consumer.csproj"
COPY . .
WORKDIR "/src/consumer"
RUN dotnet build "consumer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "consumer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "consumer.dll"]
