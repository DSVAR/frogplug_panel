# FrogPlug

FrogPlug управляет узлами Xray, пользователями и подписками, белыми списками OpenFlux и MTProto.

## Требования

.NET SDK 10 для локального запуска. Для сервера: Debian 12+ или Ubuntu 22.04+.

## Локальный запуск

```bash
dotnet run --project src/FrogPlug.Server
```

Откройте адрес из вывода приложения и завершите настройку на `/setup`. Данные хранятся в `appdata`.

## Установка

Соберите образы, скопируйте архивы на сервер и запустите установщик:

```bash
bash scripts/build-images.sh
scp artifacts/images-*.tar artifacts/installer-*.tar.gz root@example.com:/root/
tar -xzf installer-*.tar.gz
sudo bash deploy/install.sh --with-panel --domain example.com --images /root/images-*.tar
```

Для подключения отдельного узла используйте выданный в панели токен:

```bash
sudo bash install.sh --images images-VERSION.tar --panel-url https://example.com/secret --token TOKEN
```

## Обновление

Повторно запустите установщик с теми же параметрами. Секретные пути и рабочие конфиги сохраняются.

## Доступ

Панель доступна по адресу `https://домен/<секретный-путь>`. Ссылки пользователей и подписки находятся на их личной странице `/u/{token}`.

Поддерживаются Reality, XHTTP, Hysteria2, OpenFlux и MTProto.

## Лицензия и уведомления

Сведения о сторонних компонентах находятся в [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
