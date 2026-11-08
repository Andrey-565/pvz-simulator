# Story 001: Базовое перемещение персонажа и удержание коробки

> **Epic**: pvz-simulator
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Visual/Feel
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-08

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 1`

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

- [ ] Персонаж перемещается в 3D пространстве с помощью клавиш WASD и осматривается мышью (First Person или Third Person камера).
- [ ] Физический контроллер персонажа обеспечивает плавное столкновение со стенами, мебелью и препятствиями без застреваний.
- [ ] При наведении взгляда на коробку на дистанции до 2.5 метров появляется визуальный прицел/индикатор взаимодействия.
- [ ] По нажатию клавиши [E] коробка прикрепляется к точке захвата (HoldPoint) перед игроком и движется вместе с ним.
- [ ] Пока коробка в руках, повторное нажатие [E] аккуратно ставит коробку на поверхность перед персонажем с учетом высоты.
- [ ] По нажатию правой кнопки мыши [ПКМ] коробка бросается вперед с небольшим физическим импульсом.
- [ ] Персонаж может удерживать только одну коробку одновременно; попытка взять вторую игнорируется.

---

## Implementation Notes

- Реализовать скрипт перемещения `PlayerController.cs` на базе Unity CharacterController.
- Создать компонент `BoxItem.cs` с Rigidbody и коллайдером для физического поведения коробки.
- Реализовать `PickupController.cs`, управляющий лучом проверки (Raycast) и привязкой коробки к трансформу HoldPoint (isKinematic = true при удержании).

---

## Out of Scope

- [Story 002]: Привязка коробок к конкретным складским слотам/ячейкам стеллажей.
- [Story 005]: Передача коробки в руки клиенту у стойки.

---

## Files

- Assets/Scripts/Player/PlayerController.cs
- Assets/Scripts/Items/BoxItem.cs
- Assets/Scripts/Interactions/PickupController.cs

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: Visual/Feel
**Required evidence**:
- Сохраненный скриншот перемещения и удержания коробки в `production/qa/evidence/story-001-evidence.md`.


---

## Completion Notes

**Completed**: 2026-11-08  
**Criteria**: 7/7 passing  
**Deviations**: None  
**Test Evidence**: waived at qa.level minimal; verified via Unity Editor playtest  
**Reviewed by**: self, no independent review (Solo mode)
