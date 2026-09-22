# Подписка OpenFlux

OpenFlux использует адрес подписки Happ:

```text
https://панель.example/sub/<токен>
```

Клиент передаёт поддерживаемые транспорты в заголовке
`X-OpenFlux-Transports`. Для совместимых клиентов без заголовка, но с
`OpenFlux` в `User-Agent`, возвращается полный набор.

Ответ — JSON с полями `version`, `title`, `status`, `expires`, `updateHours`,
`support`, `announce` и `profiles`. Значения `status`: `active`, `expired`,
`revoked`. Для неактивной подписки `profiles` пуст.

Поддерживаются `vyandex`, `yandex`, `mailru`, `telemost`, `boards`, `avito`, `cupsonline` и `oneme` (MAX). Учётные данные MAX в подписку не попадают.
