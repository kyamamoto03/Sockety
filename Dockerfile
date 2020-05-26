FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .


COPY iSocket/*.csproj ./iSocket/
RUN dotnet restore iSocket

COPY iSocketServer/*.csproj ./iSocketServer/
RUN dotnet restore iSocketServer

# copy everything else and build app

COPY iSocket ./iSocket/
COPY iSocketServer ./iSocketServer/
WORKDIR /app/iSocketServer
RUN dotnet publish -c Release -o out


FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

WORKDIR /app

COPY --from=build /app/iSocketServer/out ./


ENTRYPOINT ["dotnet", "iSocketServer.dll"]

EXPOSE 11000

# docker build -t kyamamoto03/isocketierver:latest .
#単体実行は docker run -it --rm -p 12345:12345 -p 5000:80 -e GenbaAppFolder="/genbaapp" -v c:\genbaapp:/genbaapp --name iSocketServer kyamamoto03/iSocketServer:latest /bin/bash