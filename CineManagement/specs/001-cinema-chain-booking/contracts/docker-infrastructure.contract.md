# Contract: Cloud-Native Infrastructure (Docker, NGINX & Cloudflare SSL)

**Component**: Production & Development Deployment Topology  
**Standard**: Multi-container Docker Compose, NGINX 1.25+ Alpine, Cloudflare Origin CA TLS 1.3

---

## 1. `docker-compose.yml` Specification

```yaml
version: '3.8'

services:
  # ── Reverse Proxy & Edge SSL Termination ─────────────────────────────────
  nginx:
    image: nginx:1.25-alpine
    container_name: cinemanagement-nginx
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/conf.d:/etc/nginx/conf.d:ro
      - ./nginx/certs:/etc/nginx/certs:ro
    depends_on:
      - app
    networks:
      - cinemanagement-net

  # ── ASP.NET Core 10 Web Application ──────────────────────────────────────
  app:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: cinemanagement-app
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - DOTNET_RUNNING_IN_CONTAINER=true
      - ConnectionStrings__DefaultConnection=Server=mariadb;Port=3306;Database=cinemanagement_db;User=cinema_user;Password=cinema_secure_pass_2026;
      - Storage__Provider=CloudflareR2
      - Storage__ServiceUrl=${CLOUDFLARE_R2_URL:-http://minio:9000}
      - Storage__BucketName=${CLOUDFLARE_R2_BUCKET:-cinemanagement-media}
      - Storage__AccessKeyId=${CLOUDFLARE_R2_ACCESS_KEY:-minioadmin}
      - Storage__SecretAccessKey=${CLOUDFLARE_R2_SECRET_KEY:-minioadmin123}
      - Storage__PublicBaseUrl=${CLOUDFLARE_R2_PUBLIC_URL:-http://localhost:9000/cinemanagement-media}
      - Storage__ForcePathStyle=${STORAGE_FORCE_PATH_STYLE:-true}
    depends_on:
      mariadb:
        condition: service_healthy
      minio:
        condition: service_started
    networks:
      - cinemanagement-net

  # ── MariaDB 10.11 InnoDB Relational Database ─────────────────────────────
  mariadb:
    image: mariadb:10.11
    container_name: cinemanagement-mariadb
    restart: unless-stopped
    environment:
      - MYSQL_ROOT_PASSWORD=root_secure_pass_2026
      - MYSQL_DATABASE=cinemanagement_db
      - MYSQL_USER=cinema_user
      - MYSQL_PASSWORD=cinema_secure_pass_2026
    volumes:
      - mariadb_data:/var/lib/mysql
    healthcheck:
      test: ["CMD", "healthcheck.sh", "--connect", "--innodb_initialized"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - cinemanagement-net

  # ── MinIO S3 Object Storage (Development & Fallback) ─────────────────────
  minio:
    image: minio/minio:latest
    container_name: cinemanagement-minio
    restart: unless-stopped
    command: server /data --console-address ":9001"
    environment:
      - MINIO_ROOT_USER=minioadmin
      - MINIO_ROOT_PASSWORD=minioadmin123
    ports:
      - "9000:9000"
      - "9001:9001"
    volumes:
      - minio_data:/data
    networks:
      - cinemanagement-net

  # ── MinIO Initialization: Auto Bucket Provisioning ───────────────────────
  minio-init:
    image: minio/mc:latest
    container_name: cinemanagement-minio-init
    depends_on:
      - minio
    entrypoint: >
      /bin/sh -c "
      /usr/bin/mc alias set localminio http://minio:9000 minioadmin minioadmin123;
      /usr/bin/mc mb localminio/cinemanagement-media --ignore-existing;
      /usr/bin/mc anonymous set download localminio/cinemanagement-media;
      exit 0;
      "
    networks:
      - cinemanagement-net

networks:
  cinemanagement-net:
    driver: bridge

volumes:
  mariadb_data:
  minio_data:
```

---

## 2. NGINX Reverse Proxy & WebSocket Configuration (`nginx/conf.d/default.conf`)

```nginx
upstream app_upstream {
    server app:8080;
    keepalive 32;
}

# HTTP: Chuyển hướng toàn bộ sang HTTPS
server {
    listen 80;
    listen [::]:80;
    server_name cinemanagement.vn *.cinemanagement.vn;

    location / {
        return 301 https://$host$request_uri;
    }
}

# HTTPS: Cloudflare Origin CA Termination & WebSocket Proxying
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name cinemanagement.vn *.cinemanagement.vn;

    # Cloudflare Origin CA Certificates
    ssl_certificate /etc/nginx/certs/origin.crt;
    ssl_certificate_key /etc/nginx/certs/origin.key;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    client_max_body_size 10M;

    # 1. SignalR Real-Time WebSocket Proxying
    location /seatBookingHub {
        proxy_pass http://app_upstream;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
        
        # Vô hiệu hóa buffer và timeout dài cho kết nối thường trực
        proxy_buffering off;
        proxy_read_timeout 3600s;
        proxy_send_timeout 3600s;
    }

    # 2. General HTTP Web Endpoints
    location / {
        proxy_pass http://app_upstream;
        proxy_http_version 1.1;
        proxy_set_header Connection "";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
    }
}
```

---

## 3. Dockerfile Multi-Stage Build Contract

```dockerfile
# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CineManagement.csproj", "./"]
RUN dotnet restore "CineManagement.csproj"

COPY . .
RUN dotnet build "CineManagement.csproj" -c Release -o /app/build
RUN dotnet publish "CineManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Image (Stateless)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

# Use non-root user
USER $APP_UID

ENTRYPOINT ["dotnet", "CineManagement.dll"]
```
