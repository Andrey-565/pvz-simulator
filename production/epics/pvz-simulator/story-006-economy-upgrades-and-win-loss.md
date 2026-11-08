# Story 006: Экономика, магазин улучшений ПВЗ и победа/поражение

> **Epic**: pvz-simulator
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-08

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 6`

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

- [ ] Рейтинг ПВЗ вычисляется динамически на основе оценок последних 10 обслуженных клиентов (начальный рейтинг: 1.0 звезда).
- [ ] Текущий рейтинг и баланс денег отображаются в верхнем углу экрана (HUD) и на мониторе компьютера.
- [ ] В компьютере доступен раздел «Улучшения ПВЗ» (Upgrades) со следующими позициями:
  - «Дополнительный стеллаж» ($300): добавляет 4 новые ячейки склада.
  - «Косметический ремонт стен и пола» ($500): заменяет обшарпанные текстуры на чистые и современные, повышает базовое терпение клиентов на +10 секунд.
  - «Быстрый сканер штрихкодов» ($250): автоматизирует поиск номера ячейки при клике на клиента.
- [ ] При покупке улучшения деньги списываются, апгрейд помечается как «Куплено», а соответствующие 3D-объекты или текстуры в сцене активируются.
- [ ] Условие поражения (Fail State): если баланс падает ниже нуля при отсутствии заказов на складе (банкротство), либо рейтинг опускается ниже 0.5 звезд — игра останавливается и появляется экран «ПВЗ Закрыт» с кнопкой рестарта.
- [ ] Условие победы (Win State): при достижении рейтинга 5.0 звезд и покупке всех улучшений выводится поздравительный экран победы.

---

## Implementation Notes

- Ожидает подтверждения и указаний пользователя для реализации.

---

## Out of Scope

- Бесконечный режим песочницы с процедурными событиями.

---

## Files

- `Assets/Scripts/Shop/StoreUpgradeManager.cs`
- `Assets/Scripts/Economy/RatingManager.cs`
- `Assets/Scripts/Game/GameManager.cs`
- `Assets/Tests/EditMode/StoreUpgradeTests.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/StoreUpgradeTests.cs`
