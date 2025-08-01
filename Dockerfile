# chuẩn bị môi trường để build và chạy ứng dụng ASP.NET Core (Multi-Stage Build)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:3050
EXPOSE 3050

# Stage 1: Build và tạo cơ sở dữ liệu
# Sử dụng SDK để biên dịch mã nguồn và tạo cơ sở dữ liệu
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY OnlineShop.sln ./
COPY Entities/Entities.csproj Entities/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Infrastructure.SqlServer/Infrastructure.SqlServer.csproj Infrastructure.SqlServer/
COPY UseCase/UseCase.csproj UseCase/
COPY UseCase.Caching/UseCase.Caching.csproj UseCase.Caching/

RUN dotnet restore OnlineShop.sln
COPY . .
WORKDIR /src/Infrastructure
RUN dotnet tool install --global dotnet-ef 
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet build ./Infrastructure.csproj -c $BUILD_CONFIGURATION -o /app/build
CMD ["dotnet", "ef", "database", "update", "--environment", "Development", "--project", "src/Repositories"]

# Stage 2: Publish ứng dụng
# Biên dịch mã nguồn và tạo ra các tệp cần thiết để chạy ứng dụng
FROM build AS publish
RUN dotnet publish ./Infrastructure.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 3: Tạo image cuối cùng
# Sử dụng image đã được biên dịch và xuất bản để tạo image cuối cùng
FROM base AS final  
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Infrastructure.dll"]