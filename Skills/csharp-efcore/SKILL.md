---
name: csharp-efcore
description: "EF Core rules for this project: entities and enums in Domain, convention over configuration, string lengths from IOptions<T>, DbContext configuration with per-table regions, context interface in Application and implementation in Infrastructure. Use when creating or editing EF Core entities, DbContext, model configuration, persistence options, or migrations."
---

# EF Core

## Где что лежит

- Сущности — `Domain/Entities`, enum'ы — `Domain/Enums`.
- Интерфейс контекста (порт) — `Application/Persistence/Interfaces/IRepoHealthCheckerDbContext`, с `DbSet<T>` и `SaveChangesAsync`.
- Реализация контекста — `Infrastructure/Persistence/RepoHealthCheckerDbContext`.
- Классы опций для длин строк — `Application/Options` (см. скилл `csharp-options`).

## Соглашение важнее конфигурации

- Связи и required явно не описывай: EF выводит их из навигаций, имён FK и nullability.
- Настраивай модель только там, где соглашений не хватает: one-to-one, уникальные индексы, максимальные длины, конвертации.
- Конфигурация — в `OnModelCreating`, **каждая таблица в отдельном `#region`**.
- Не пиши `required` у свойств сущностей. Строки инициализируй через `= string.Empty;`, reference-навигации делай nullable или обходись только коллекционными навигациями, чтобы не требовалась инициализация.

## Длины строк и валидация

- Максимальные длины и ограничения бери из `IOptions<T>`, не хардкодь значения в `HasMaxLength`.
- Опции создавай в `Application/Options` с суффиксом `Options` (см. `csharp-options`).

## Контекст

```csharp
public DbSet<User> Users => Set<User>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    #region User
    modelBuilder.Entity<User>(entity =>
    {
        entity.Property(x => x.Login).HasMaxLength(_userOptions.MaxLoginLength);
        entity.HasIndex(x => x.YaId).IsUnique();
    });
    #endregion
}
```

## Правила

- Только async: `SaveChangesAsync(cancellationToken)`, без `.Result`/`.Wait()`.
- Один публичный тип на файл, имя файла = имя типа.
- Никаких `= null!`.
- Навигации: коллекционная навигация на главной стороне + FK на зависимой, без обратной reference-навигации — не требует инициализации.
- Enum'ы храни по умолчанию (int), если явно не нужна строковая форма.
- Миграции — `dotnet ef migrations add <Name>` / `dotnet ef database update`; провайдер и DI регистрируются в composition root.

## Чек-лист

- [ ] Сущности/энумы — в `Domain`, порт контекста — в `Application`, реализация — в `Infrastructure`.
- [ ] Нет явной конфигурации того, что EF выводит сам.
- [ ] Длины строк — из `IOptions<T>`.
- [ ] Каждая таблица — в своём `#region`.
- [ ] Нет `= null!`, нет sync-вызовов.
