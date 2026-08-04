# Команды RTS. См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §2.1.

rts-cmd-init-usage = Использование: initrts <игрок1> <игрок2> [туман_войны]
rts-cmd-end-usage = Использование: endrts [игрок]

rts-cmd-player-not-found = Игрок { $player } не найден.
rts-cmd-same-player = Нельзя запустить матч игрока с самим собой.
rts-cmd-not-ghost = Игрок { $player } не является призраком. В RTS можно попасть только призраком.
rts-cmd-no-mind = У игрока { $player } нет разума.
rts-cmd-already-in-match = Игрок { $player } уже участвует в матче.
rts-cmd-already-visiting = Игрок { $player } уже управляет чужой сущностью (админ-призрак?). Сначала верните его в тело.
rts-cmd-bad-bool = "{ $value }" — не true и не false.

rts-cmd-init-started = Матч RTS запущен: { $first } против { $second }. Туман войны: { $fog }. Идёт генерация арены.

rts-cmd-end-not-in-match = Игрок { $player } не участвует в матче.
rts-cmd-end-single = Матч игрока { $player } прерван.
rts-cmd-end-all = Прервано матчей: { $count }.

rts-cmd-hint-ghost = <призрак>
rts-cmd-hint-player = <игрок>
rts-cmd-hint-fog = [туман войны]

# Интерфейс.
rts-minimap-placeholder = Миникарта
rts-alert-idle-worker-short = ␣
rts-alert-idle-worker = Простаивающий рабочий (Пробел)
rts-alert-under-attack-short = ~
rts-alert-under-attack = Последнее нападение (Ё)
rts-alert-leave-short = Ins
rts-alert-leave = Покинуть матч (Insert)

rts-selection-group = { $name } — выделено { $count }

rts-stat-attack = Атака: { $value }
rts-stat-armor = Броня: { $value }
rts-stat-range = Дальность: { $value }
rts-stat-speed = Скорость: { $value }

# Названия биндов в меню управления.
ui-options-function-rts-select = RTS: выделить
ui-options-function-rts-order = RTS: приказ
ui-options-function-rts-attack-move = RTS: атака по точке
ui-options-function-rts-stop = RTS: стоп
ui-options-function-rts-hold-position = RTS: удерживать позицию
ui-options-function-rts-patrol = RTS: патрулировать
ui-options-function-rts-idle-worker = RTS: простаивающий рабочий
ui-options-function-rts-last-event = RTS: последнее событие
ui-options-function-rts-leave-match = RTS: покинуть матч

# Описания и справка консольных команд.
cmd-initrts-desc = Запускает RTS-матч между двумя призраками на отдельной сгенерированной карте.
cmd-initrts-help = initrts <игрок1> <игрок2> [туман_войны]
cmd-endrts-desc = Прерывает RTS-матч указанного игрока или все матчи сразу.
cmd-endrts-help = endrts [игрок]
cmd-rtsdebug-desc = Печатает состояние RTS на клиенте: команду игрока и выделяемые сущности.
cmd-rtsdebug-help = rtsdebug
