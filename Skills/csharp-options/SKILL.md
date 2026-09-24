---
name: csharp-options
description: "IOptions<T> configuration conventions: bind settings from appsettings.json via AddOptions<T>().Bind instead of hardcoding constants, options classes live in the Options folder with the Options suffix, use the required keyword instead of = null! defaults, Environment.ExpandEnvironmentVariables for path properties, Path.Join for path segments. Use when writing or editing C# options/settings classes, appsettings.json binding, or IOptions usage."
---

# Опции (IOptions<T>)

## Вместо констант — IOptions<T>

Не хардкодь настройки (пути, таймауты, размеры, имена ресурсов) константами (`const`, `static readonly`, литералы). Выноси их в классы опций и читай через `IOptions<T>`:

- значения меняются в `appsettings.json` без пересборки;
- зависимости видны явно через конструктор;
- опции легко подменить в тестах.

```csharp
// Плохо
private const string DataPath = "data/storage.json";

// Хорошо
public sealed class StorageOptions
{
    public required string DataPath { get; init; }
}
```

## Расположение

Классы опций размещай в папке **`Options`** внутри проекта, имена — с суффиксом `Options`: `Options/StorageOptions.cs`.

## `required` вместо `= null!`

- Не инициализируй свойства опций через `= null!` и не оставляй `null`-дефолты: ошибка конфигурации обнаружится только в runtime при первом обращении.
- Помечай обязательные свойства ключевым словом **`required`** — компилятор заставит заполнить их при создании объекта.

```csharp
// Плохо
public class StorageOptions
{
    public string DataPath { get; set; } = null!;
}

// Хорошо
public sealed class StorageOptions
{
    public required string DataPath { get; init; }
}
```

## Биндинг конфигурации

```csharp
services.AddOptions<StorageOptions>()
    .Bind(configuration.GetSection(nameof(StorageOptions)));
```

Секция в `appsettings.json` называется по имени класса опций.

## Пути

- Свойства, содержащие пути, раскрывай через `Environment.ExpandEnvironmentVariables`, если конфиг может содержать переменные окружения (C# 14 keyword `field`):

  ```csharp
  public sealed class StorageOptions
  {
      public required string DataPath
      {
          get => field;
          set => field = Environment.ExpandEnvironmentVariables(value);
      }
  }
  ```

- Сегменты пути склеивай через `Path.Join`, а не `Path.Combine`.

## Чек-лист

- [ ] Нет захардкоженных констант-настроек — всё через `IOptions<T>`.
- [ ] Класс опций лежит в папке `Options` и называется с суффиксом `Options`.
- [ ] Обязательные свойства — `required`, а не `= null!`.
- [ ] Пути раскрываются через `Environment.ExpandEnvironmentVariables`, склейка — `Path.Join`.
