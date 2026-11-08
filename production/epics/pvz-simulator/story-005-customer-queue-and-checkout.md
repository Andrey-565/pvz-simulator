# Story 005: Очередь посетителей, проверка кодов и выдача заказов

> **Epic**: pvz-simulator
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-08

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP features 4 & 5`

**ADR Governing Implementation**: N/A (minimal — no ADRs)
**ADR Decision Summary**: N/A (minimal — no ADRs)
**ADR Version**: N/A (minimal — no ADRs)

**Engine**: Unity Unity 6.3 LTS | **Risk**: HIGH
**Engine Notes**: none (no ADR engine-compatibility analysis at minimal)

**Control Manifest Rules (this layer)**:
- Required: N/A (minimal — no control manifest)
- Forbidden: N/A (minimal — no control manifest)
- Guardrail: N/A (minimal — no control manifest)

---

## Acceptance Criteria

- [x] На стойке выдачи присутствует переключатель / табличка «Открыть ПВЗ» (Open/Closed). Открыть пункт можно только при наличии хотя бы одной коробки на складе.
- [x] Клиенты заходят через вход для посетителей (Южная стена) в клиентский зал и выстраиваются в упорядоченную очередь до 3 человек перед стойкой.
- [x] Игрок находится в рабочей зоне за стойкой, имея доступ к складу, компьютеру и стойке.
- [x] Номер заказа клиента скрыт до момента сканирования сканером штрихкодов.
- [x] При сканировании клиента сканером на стойке:
  - Раскрывается номер заказа (`Заказ #...`).
  - Подсвечивается ячейка на стеллаже склада, где лежит нужная коробка.
  - Запускается таймер скорости обслуживания.
- [x] Таймер обслуживания и расчет рейтинга:
  - 0–15 сек: **+1 очко рейтинга** (бонусная скорость).
  - 15–45 сек: **нейтрально (0)**.
  - 45–60 сек: **-1 очко рейтинга**.
  - 60–120 сек: **-3 очка рейтинга**.
  - >120 сек: **-5 очков рейтинга** (клиент не уходит сам, терпеливо ждет выдачи заказа).
- [x] Игрок приносит коробку со склада и выдает заказ:
  - Если номер коробки совпадает: клиент забирает посылку, начисляется оплата ($50 + бонус) и применяется изменение рейтинга.
  - Если номер не совпадает: отказ в приеме, клиент ждет свой заказ.
- [x] Обслуженный клиент освобождает место у стойки и уходит через выход, следующий клиент продвигается вперед.

---

## Implementation Notes

- Обновлен `CustomerAI.cs`: скрытие номера до сканирования, запуск таймера при сканировании, градации зон скорости (+1, 0, -1, -3, -5), клиент не уходит по таймеру.
- Обновлен `RatingManager.cs`: поддержка очков рейтинга (+1, 0, -1, -3, -5) и скользящего расчета звездного рейтинга.
- Обновлен `CheckoutCounter.cs`: вызов сканирования клиента, раскрытие заказа, запуск таймера и подсветка ячейки на складе.
- Обновлен `PickupController.cs`: сканирование по клику на сканер или клиента, передача коробки.
- Обновлен `CustomerQueueManager.cs`: спавн в клиентском зале с южной стороны, очередь лицом к стойке.
- Обновлен `TestSceneBuilder.cs`: перестроена сцена с южным входом для посетителей, стойкой и спавном игрока в рабочей зоне за стойкой.

---

## Out of Scope

- [Story 006]: Апгрейды стойки и расширение склада за заработанные деньги.

---

## Files

- `Assets/Scripts/Customers/CustomerAI.cs`
- `Assets/Scripts/Customers/CustomerQueueManager.cs`
- `Assets/Scripts/Counter/CheckoutCounter.cs`
- `Assets/Scripts/Economy/RatingManager.cs`
- `Assets/Scripts/Warehouse/WarehouseSlot.cs`
- `Assets/Scripts/Interactions/PickupController.cs`
- `Assets/Scripts/Editor/TestSceneBuilder.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Waived at qa.level minimal; verified via Unity Editor playtest and script compilation.
