Workflow создан: docs/tasks/current/card_animation_refactor/workflow.md

Основные решения:
- IBoardCellsAnimator инжектируется через DI (не параметр Sync)
- ICardActionSync<T>.Sync signature не меняется
- CardActionSnapshotHandler убирает PlayTarget/PlayAction — оставляет только card.Use()
- MenuCardPreviewPlayer убирает PlayTarget/PlayAction — оставляет только _syncRegistry.Dispatch
- Каждый Snapshot сам решает какие анимации проигрывать, используя данные ИЗ СВОЕГО ПЕЙЛОДА

Бэкенд не трогаем (snapshot типы уже имеют нужные поля).

~40 Snapshot классов в GamePlay/Cards/Entities/Actions/*/*.cs нужно обновить.

Начинаем выполнение?
