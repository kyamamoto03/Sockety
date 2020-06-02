FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .


COPY Sockety/*.csproj ./Sockety/
RUN dotnet restore Sockety

COPY Example/SocketyServer/*.csproj ./SocketyServer/
RUN dotnet restore SocketyServer

# copy everything else and build app

COPY Sockety ./Sockety/
COPY Example/SocketyServer ./Example/SocketyServer/

WORKDIR /app/Example/SocketyServer
RUN dotnet publish -c Release -o out


FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

WORKDIR /app

COPY --from=build /app/Example/SocketyServer/out ./


ENTRYPOINT ["dotnet", "SocketyServer.dll"]

EXPOSE 11000

# docker build -t kyamamoto03/sockety-server:latest .
#単体実行は docker run -it --rm -p 11000:11000 -p 11000-11200:11000-11200/udp --name SocketyServer kyamamoto03/sockety-server:latest