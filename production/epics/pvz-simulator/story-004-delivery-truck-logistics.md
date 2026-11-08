# Story 004: Логистика доставки — прибытие фуры и разгрузка партий

> **Epic**: pvz-simulator
> **Status**: Complete
> **Layer**: Integration
> **Type**: Integration
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

- [x] После оформления заказа на ПК запускается таймер доставки длительностью 15–20 секунд.
- [x] По завершении таймера у зоны разгрузки перед входом в ПВЗ появляется модель фуры с характерным звуковым сигналом прибытия (клаксон/шум мотора).
- [x] Задние двери фуры открываются, и внутри кузова появляются заказанные коробки (каждая имеет сгенерированный уникальный номер заказа, например #201, #202).
- [x] Игрок может подойти к кузову фуры, взять коробку и перенести ее в складскую зону ПВЗ.
- [x] Система отслеживает количество выгруженных коробок: когда кузов опустошен, выводится уведомление «Партия выгружена».
- [x] Через 5 секунд после полной разгрузки фура закрывает двери и отъезжает (деспавнится).

---

## Implementation Notes

- Реализован менеджер доставки `DeliveryManager.cs`, отсчитывающий таймер после события `ComputerUI.OnSupplyBatchOrdered`.
- Создан грузовик `DeliveryTruck.cs` с анимированными створками дверей, сеткой спавна коробок в кузове и логикой отъезда.
- Интегрирована проверка опустошения кузова с механикой подбора коробки (`PickupController` / `BoxItem`).
- Добавлена уличная погрузочная зона и ворота в `TestSceneBuilder.cs`.

---

## Out of Scope

- [Story 002]: Непосредственное размещение по ячейкам стеллажей (коробку можно временно бросить на пол зоны приемки).
- [Story 005]: Вызов клиентов на получение посылок.

---

## Files

- `Assets/Scripts/Delivery/DeliveryManager.cs`
- `Assets/Scripts/Delivery/DeliveryTruck.cs`
- `Assets/Scripts/Editor/TestSceneBuilder.cs`

---

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Waived at qa.level minimal; verified via Unity Editor playtest and script compilation.

---

## Completion Notes

**Completed**: 2026-11-08  
**Criteria**: 6/6 passing  
**Deviations**: None  
**Test Evidence**: waived at qa.level minimal; verified via Unity Editor playtest  
**Reviewed by**: self, no independent review (Solo mode)
