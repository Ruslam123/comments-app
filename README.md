# SPA Додаток: Комментарі

## Швидкий старт

```bash
docker-compose up --build
```

- Frontend: http://localhost:3000
- Backend: http://localhost:5000/swagger
- RabbitMQ: http://localhost:15672 (guest/guest)

## Що виправлено

1. CommentService.cs - логіка кешування, SignalR
2. RedisCacheService.cs - синтаксис
3. CommentsController.cs - валідація
4. FileController.cs - завантаження файлів
5. Lightbox компонент
6. Docker compose оновлено

## Деплой

Дивіться повну інструкцію в документації
