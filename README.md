# Eco-Activist Backend

Серверный RESTful API сервис для координации экологических мероприятий, учета волонтерской активности и валидации посещений.

---

## Стек технологий

- **Платформа:** .NET 8 / C# (ASP.NET Core Web API)
- **База данных:** PostgreSQL 15+
- **ORM:** Entity Framework Core
- **Аутентификация:** JWT Bearer Token
- **Инфраструктура:** Docker, Docker Compose
- **Документация:** OpenAPI / Swagger UI

---

## Требования к окружению

- .NET SDK 8.0+
- Git
- Docker и Docker Compose
- Утилита EF Core CLI: `dotnet tool install --global dotnet-ef`

---

## Конфигурация

Параметры задаются через `appsettings.Development.json` или переменные окружения:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=eco_activist_db;Username=postgres;Password=postgres"
  },
  "JwtSettings": {
    "Secret": "YourStrongSecretKeyMustBeAtLeast32CharsLength123456",
    "Issuer": "EcoActivistBackend",
    "Audience": "EcoActivistClient",
    "ExpiryMinutes": 1440
  }
}
```

---

## Локальный запуск

Склонируйте репозиторий:

```bash
git clone https://github.com/darksvoid/Eco-Activist-backend.git
cd Eco-Activist-backend
```

### Способ 1. Запуск через Docker Compose (Все в одном)

Поднимает базу данных PostgreSQL и API в изолированных контейнерах:

```bash
# Сборка и запуск в фоновом режиме
docker compose up --build -d

# Просмотр логов
docker compose logs -f

# Остановка контейнеров
docker compose down
```

---

### Способ 2. Локальный запуск через .NET CLI

Используется для активной разработки и отладки.

1. Запустите только контейнер базы данных:
   ```bash
   docker compose up -d db
   ```

2. Примените миграции для создания схемы БД:
   ```bash
   dotnet ef database update
   ```

3. Запустите приложение:
   ```bash
   dotnet run
   ```

Сервис запустится по адресу `http://localhost:5000` (HTTPS: `https://localhost:5001`).

---

## Документация API

После запуска приложения интерфейс Swagger UI доступен по адресу:
- `http://localhost:5000/swagger`

### Основные эндпоинты

- `POST /api/auth/register` — регистрация пользователя
- `POST /api/auth/login` — авторизация и получение JWT-токена
- `GET /api/events` — список активных мероприятий
- `POST /api/events` — создание нового события
- `POST /api/events/{id}/join` — запись на мероприятие
- `POST /api/attendance/verify` — подтверждение посещения по QR-коду
- `GET /api/profile` — профиль текущего пользователя и статистика баллов

---

## Тестирование

Запуск модульных и интеграционных тестов:

```bash
dotnet test
```

---

## Базовые команды EF Core

- Создание новой миграции: `dotnet ef migrations add <Name>`
- Применение миграций к базе: `dotnet ef database update`
- Откат последней миграции: `dotnet ef migrations remove`
