# LTSS Service API

Небольшой backend-сервис для хранения и выдачи данных экспериментальных сессий.

Сервис работает по модели `bootstrap + checkpoint`:
- клиент получает полную конфигурацию одной сессии;
- проходит эксперимент локально;
- отправляет на сервер только результаты периодов, опросы и пакетные логи.

## Назначение

- хранение конфигураций экспериментальных сессий;
- хранение учёток участников и их прохождений;
- выдача bootstrap payload для клиента;
- приём checkpoint, survey submissions и log batches;
- выгрузка данных для исследователя.

## API участника

- `POST /api/auth/login`  
  Вход участника и получение токена.

- `GET /api/run/current`  
  Краткая информация о текущем прохождении.

- `GET /api/run/{runId}/bootstrap`  
  Полный bootstrap payload для запуска сессии на клиенте.

- `POST /api/run/{runId}/checkpoint`  
  Сохранение чекпоинта завершённого периода.

- `POST /api/run/{runId}/surveys/submit`  
  Сохранение ответов на опрос.

- `POST /api/run/{runId}/logs/batch`  
  Сохранение пакетного клиентского лога.

- `POST /api/run/{runId}/complete`  
  Завершение прохождения.

## API администратора

- `POST /api/admin/session-definitions`  
  Создание экспериментальной сессии.

- `PUT /api/admin/session-definitions/{id}`  
  Обновление экспериментальной сессии.

- `GET /api/admin/session-definitions/{id}`  
  Получение одной сессии.

- `GET /api/admin/session-definitions`  
  Список сессий.

- `POST /api/admin/session-definitions/{id}/generate-accounts`  
  Генерация набора учёток участников.

- `GET /api/admin/session-definitions/{id}/accounts`  
  Учётки по сессии.

- `GET /api/admin/session-definitions/{id}/runs`  
  Прохождения по сессии.

- `POST /api/admin/surveys`  
  Создание шаблона опроса.

- `PUT /api/admin/surveys/{id}`  
  Обновление шаблона опроса.

- `GET /api/admin/surveys`  
  Список шаблонов опросов.

## Выгрузка данных

- `GET /api/admin/export/session-definition/{id}/summary`  
  Краткая сводка по сессии.

- `GET /api/admin/export/session-definition/{id}/checkpoints`  
  Выгрузка чекпоинтов.

- `GET /api/admin/export/session-definition/{id}/surveys`  
  Выгрузка ответов на опросы.

- `GET /api/admin/export/session-definition/{id}/accounts-runs`  
  Выгрузка связки учёток и прохождений.

## Базовый сценарий

1. Исследователь создаёт сессию и генерирует учётки.
2. Участник входит в систему и получает bootstrap.
3. Клиент локально ведёт эксперимент.
4. Сервер принимает результаты и хранит их.
5. Исследователь забирает данные через export endpoints.
