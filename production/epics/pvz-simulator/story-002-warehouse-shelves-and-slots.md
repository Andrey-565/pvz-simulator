# Story 002: Складские стеллажи и размещение коробок по ячейкам

> **Epic**: pvz-simulator
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-08

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 3`

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

- [ ] В помещении склада размещаются стеллажи с набором ячеек (например, Стеллаж A: ячейки A1..A4, Стеллаж B: ячейки B1..B4).
- [ ] Каждая ячейка имеет уникальный строковый идентификатор (SlotId) и отслеживает свое состояние (`IsEmpty`, ссылка на размещенную коробку).
- [ ] При взаимодействии с ячейкой, когда коробка находится в руках, коробка автоматически выравнивается (Snap) по центру полки и фиксируется в ней.
- [ ] Размещенная коробка регистрирует в своей структуре данных номер текущей ячейки (`CurrentSlotId`).
- [ ] Если ячейка уже занята, попытка поставить в нее вторую коробку отклоняется с визуальным или звуковым сигналом ошибки.
- [ ] Взятие коробки с полки в руки игрока освобождает ячейку склада (`Slot.Clear()`).

---

## Implementation Notes

- Создать компонент `WarehouseSlot.cs` с триггерным коллайдером для детекции взаимодействия.
- Реализовать менеджер склада `WarehouseManager.cs`, предоставляющий методы `RegisterBox(string slotId, BoxItem box)` и `GetBox(string slotId)`.
- Добавить визуальный 3D Text / Canvas с номером ячейки над каждой полкой.

---

## Out of Scope

- [Story 003]: Отображение списка ячеек в интерфейсе рабочего компьютера.
- [Story 004]: Доставка коробок грузовиком.

---

## Files

- Assets/Scripts/Warehouse/WarehouseSlot.cs
- Assets/Scripts/Warehouse/WarehouseShelf.cs
- Assets/Scripts/Warehouse/WarehouseManager.cs
- Assets/Scripts/Interactions/PickupController.cs

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `Assets/Tests/EditMode/WarehouseSlotTests.cs` (тесты регистрации и освобождения ячейки).


---

## Completion Notes

**Completed**: 2026-11-08  
**Criteria**: 6/6 passing  
**Deviations**: None  
**Test Evidence**: waived at qa.level minimal; verified via Unity Editor playtest  
**Reviewed by**: self, no independent review (Solo mode)
