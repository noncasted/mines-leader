FROM mcr.microsoft.com/dotnet/sdk:latest AS builder

# Copy only the deploy project files
COPY tools/deploy/ /build/tools/deploy/
WORKDIR /build/tools/deploy
RUN dotnet publish -c Release -o /publish

FROM mcr.microsoft.com/dotnet/sdk:latest

RUN apt-get update && \
    apt-get install -y nginx && \
    apt-get clean

RUN curl -sSL https://aspire.dev/install.sh | bash
ENV PATH="/root/.aspire/bin:$PATH"

# Copy entire repository (required for aspire paths)
COPY . /app

# Overlay compiled deploy project
COPY --from=builder /publish /app

WORKDIR /app

CMD ["dotnet", "deploy.dll"]