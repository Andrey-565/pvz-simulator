# Story 003: Рабочий компьютер — терминал заказа товаров и база ячеек

> **Epic**: pvz-simulator
> **Status**: Complete
> **Layer**: Feature
> **Type**: UI
> **Estimate**: M
> **Manifest Version**: N/A (minimal — no control manifest)
> **Last Updated**: 2026-11-08

## Context

**GDD**: `design/game-brief.md`
**Requirement**: `Brief MVP feature 2`

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

- [ ] При подходе к рабочему столу в ПВЗ и нажатии [E] активируется режим взаимодействия с ПК: камера фокусируется на мониторе, включается курсор мыши.
- [ ] Экран рабочего стола содержит две основные вкладки/кнопки: «Заказ товаров» (Market) и «Складской учёт» (Inventory).
- [ ] Во вкладке «Заказ товаров» доступны пакеты поставок (например: «Партия коробок: 3 шт. / $150», «Партия коробок: 6 шт. / $280»).
- [ ] При нажатии кнопки покупки проверяется текущий баланс игрока: если денег достаточно, сумма списывается, а событие заказа отправляется в систему доставки.
- [ ] Если денег недостаточно, кнопка покупки неактивна или выводит уведомление о нехватке средств.
- [ ] Во вкладке «Складской учёт» выводится таблица с перечнем всех находящихся на складе коробок и их ячейками (например: «Заказ #1042 — Ячейка A2»).
- [ ] Нажатие клавиши [Escape] или кнопки «Выход» возвращает камеру игроку и восстанавливает стандартное управление.

---

## Implementation Notes

- Реализовать `ComputerTerminal.cs` для управления переключением камер и состоянием курсора (`Cursor.lockState`).
- Реализовать интерфейс `ComputerUI.cs` на базе Unity UI / UI Toolkit.
- Подключить модуль экономики `EconomyManager.cs` для хранения баланса игрока.

---

## Out of Scope

- [Story 004]: Анимация фуры и физическая доставка коробок.
- [Story 006]: Вкладка магазина косметических улучшений и апгрейдов помещения.

---

## Files

- Assets/Scripts/Economy/EconomyManager.cs
- Assets/Scripts/Computer/ComputerTerminal.cs
- Assets/Scripts/Computer/UI/ComputerUI.cs
- Assets/Scripts/Interactions/PickupController.cs

## QA Test Cases

*N/A — no qa-lead specs at this tier; implement against the Acceptance Criteria above*

---

## Test Evidence

**Story Type**: UI
**Required evidence**:
- Сохраненный скриншот интерфейса терминала заказа в `production/qa/evidence/story-003-evidence.md`.


---

## Completion Notes

**Completed**: 2026-11-08  
**Criteria**: 7/7 passing  
**Deviations**: None  
**Test Evidence**: waived at qa.level minimal; verified via Unity Editor playtest  
**Reviewed by**: self, no independent review (Solo mode)
