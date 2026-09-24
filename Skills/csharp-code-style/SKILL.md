---
name: csharp-code-style
description: "C# code style rules: var by default, is null / is not null, single-statement bodies without braces, early return, PascalCase/camelCase/_camelCase naming without abbreviations, private/readonly/sealed, async/await without .Result, no XML docs, one public class per file matching file name. Also mandates that all C# skills are named with the csharp- prefix. Use when writing or editing ANY C# code (.cs files)."
---

# C# код-стайл

Общий принцип: пиши современный, лаконичный и читаемый C#. При изменении существующего кода в первую очередь придерживайся стиля файла, а не догматично следуй правилам ниже.

## Именование C# скиллов

**Все C# скиллы должны начинаться с префикса `csharp-`** (например `csharp-collections`, `csharp-testing`, `csharp-options`, `csharp-code-style`). Символ `#` в именах opencode-скиллов запрещён валидацией, поэтому вместо `c#-` используется `csharp-`.

## Язык и платформа

- Целевая платформа — .NET (последняя стабильная LTS, поддерживаемая проектом).
- Используй возможности современного C#: file-scoped namespaces, primary constructors, pattern matching, switch expressions, null-conditional операторы, collection expressions, target-typed `new`.

## `var`

**По умолчанию используй `var`** везде, где тип переменной очевиден из правой части выражения.

```csharp
// Хорошо
var user = await storage.LoadAsync();
var name = user.Name;
var sum = Calculate(a, b);
```

**Исключение — collection expressions.** Для них указывай явный тип коллекции, чтобы читатель сразу видел её вид. Остальные правила по коллекциям — в скилле `csharp-collections`.

**Исключение — адаптация под файл.** Если класс полностью написан без `var` (явные типы везде), при изменении такого класса **поддерживай его стиль**. Не внедряй `var` насильно и не переписывай существующие строки только ради `var`.

## Проверка на `null`

Используй `is null` / `is not null` вместо `== null` / `!= null`.

```csharp
// Хорошо
if (value is null) return;
if (items is not null)
    Process(items);

// Плохо
if (value == null) return;
if (items != null)
    Process(items);
```

## Фигурные скобки и вложенность

- Если тело `if`/`else`/`for`/`while`/`foreach` состоит из одной строки, **не оборачивай его в фигурные скобки**.

  ```csharp
  if (count > 0)
      Process(count);

  foreach (var item in items)
      writer.Write(item);
  ```

- Избегай лишней вложенности: используй ранний `return`, `continue` и `break`, когда это упрощает чтение.

  ```csharp
  // Хорошо
  if (value is null)
      return default;

  return value.Transform();

  // Плохо
  if (value is not null)
  {
      return value.Transform();
  }
  else
  {
      return default;
  }
  ```

## Именование

- `PascalCase` для классов, интерфейсов, методов, свойств, публичных полей, enum-ов и namespace-ов.
- `camelCase` для локальных переменных, параметров и приватных полей.
- `_camelCase` для приватных полей класса.
- Интерфейсы начинаются с `I`.
- Асинхронные методы заканчиваются на `Async`.
- **Не используй сокращения** в именах переменных, параметров, полей, свойств и методов. Используй полные, читаемые слова: `directory` вместо `dir`, `options` вместо `opts`, `buffer` вместо `buf`, `configuration` вместо `config`. Допускаются только общепринятые аббревиатуры, такие как `db` для database, `id` для идентификатора, `ui` для пользовательского интерфейса в контекстах, где это стандарт.

## Модификаторы

- Явно указывай `private` для членов класса.
- Указывай `readonly` для полей, которые не изменяются после инициализации.
- Предпочитай `sealed` классам, которые не проектируются для наследования.

## Коллекции и LINQ

- Тип параметра/возврата — минимально достаточный: `IReadOnlyCollection<T>`/`IReadOnlyList<T>` для чтения, `ICollection<T>`/`IList<T>`/`IDictionary<TKey,TValue>` для изменения.
- Не допускай двойного перечисления `IEnumerable<T>`; пустые коллекции — `[]`; для многопоточного доступа — concurrent/immutable/frozen коллекции.
- Полные правила и примеры — в скилле `csharp-collections`.

## Асинхронность

- Всегда используй `async`/`await`, избегай `.Result`, `.Wait()` и синхронных блокировок в асинхронном коде.
- Не лови исключения без причины; если ловишь — обрабатывай, логируй или добавь комментарий, почему подавляешь.

## Комментарии

- Комментарии должны объяснять *почему*, а не *что*.
- Избегай закомментированного кода.
- **Не пиши XML-документацию.**

## Адаптация под файл

- Главное — единообразие внутри файла.
- Правила про `{}` и лаконичность применяются в разумных пределах: если класс использует исключительно блочный стиль, не ломай его ради единообразия.
- Не превращай правку в рефакторинг ради рефакторинга.

## Структура файлов

- **Категорически запрещается создавать подклассы (вложенные классы)** внутри других классов.
- **Запрещается размещать более одного публичного класса в одном файле.**
- **Имя файла должно строго соответствовать имени публичного класса**, который в нём содержится.
- Вспомогательные типы (enum, record, struct) могут быть размещены в том же файле, только если они тесно связаны с основным классом и используются исключительно им.

```csharp
// Хорошо — файл UserService.cs
public sealed class UserService
{
    // ...
}

// Плохо — файл UserService.cs с двумя классами
public sealed class UserService
{
    // ...
}

public sealed class UserValidator
{
    // ...
}

// Плохо — вложенный класс
public sealed class UserService
{
    private sealed class InnerHelper
    {
        // ...
    }
}
```
