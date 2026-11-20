with open('Assets/Docs/LEVEL_SCRIPT.md', 'r', encoding='utf-8') as f:
    text = f.read()

target = '## Звук'
replacement = '''## Консоль Output и вывод (`print()`)

В LevelScript IDE встроен полноценный многострочный терминал вывода **OUTPUT**:
- **Временные метки**: каждое сообщение имеет точное время `[HH:mm:ss]`.
- **Цветовая индикация**:
  - **Красный**: ошибки компиляции, синтаксиса и рантайма (`[Error]`).
  - **Желтый**: системные предупреждения (`[Warning]`).
  - **Белый**: результаты вызовов `print(...)` и информационные логи.
- **Копирование**: кнопка **Copy** копирует весь чистый лог в буфер обмена (без HTML-тегов). Также текст можно выделять мышью и копировать с помощью `Ctrl+C`.
- **Очистка**: кнопка **Clear** очищает вывод.
- **Сворачивание**: кнопка **▼ / ▲** позволяет свернуть панель, чтобы увеличить рабочую область редактора кода (при возникновении новой ошибки консоль разворачивается автоматически).

### `print(value, ...)`
Выводит любые значения в консоль Output:
```csharp
print("Script started!")
print("Player position:", player.x, player.y)

list inventory = ["key", "potion", "sword"]
print("Inventory:", inventory)
print("Items count:", inventory.count, "First item:", inventory[0])

int score = 100
print("Current score is: " + score)
```

---

## Звук'''

assert target in text, "Target not found in LEVEL_SCRIPT.md"
text = text.replace(target, replacement, 1)

with open('Assets/Docs/LEVEL_SCRIPT.md', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated LEVEL_SCRIPT.md")
