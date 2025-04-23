# https://medium.com/@aliyildizoz/understanding-asp-net-core-dockerfile-a523233bb9a4
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 7000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["SocialApp.WebApi/SocialApp.WebApi.csproj", "SocialApp.WebApi/"]
COPY ["SocialApp.Models/SocialApp.Models.csproj", "SocialApp.Models/"]
RUN dotnet restore "SocialApp.WebApi/SocialApp.WebApi.csproj"
COPY . .
WORKDIR "/src/SocialApp.WebApi"
RUN dotnet build "./SocialApp.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./SocialApp.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SocialApp.WebApi.dll"]
