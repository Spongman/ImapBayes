# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build
#FROM mcr.microsoft.com/dotnet/runtime:7.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
RUN mkdir -p ImapBayes AE.Net.Mail
COPY ImapBayes/*.csproj ImapBayes/
COPY AE.Net.Mail/*.csproj AE.Net.Mail/

WORKDIR /source/ImapBayes
RUN dotnet restore --use-current-runtime /p:PublishReadyToRun=true

# copy and publish app and libraries
WORKDIR /source
COPY . .
WORKDIR /source/ImapBayes
RUN dotnet publish --use-current-runtime --self-contained true --no-restore -o /app /p:PublishTrimmed=true /p:PublishReadyToRun=true


# Enable globalization and time zones:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/enable-globalization.md
# final stage/image
FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine
#FROM mcr.microsoft.com/dotnet/runtime:7.0-alpine
#FROM mcr.microsoft.com/dotnet/runtime:7.0
WORKDIR /app
COPY --from=build /app .
COPY ./ImapBayes.s3db .
#ENTRYPOINT ["./dotnetapp"]
ENTRYPOINT ["./ImapBayes"]