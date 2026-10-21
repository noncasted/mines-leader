# MemoryPack.Core для WebGL-клиента

Урезанная сборка [MemoryPack](https://github.com/Cysharp/MemoryPack) 1.21.4 (MIT, см. `LICENSE.md`). Клиент использует её вместо DLL из NuGet. Бэкенд по-прежнему берёт полный пакет из NuGet, формат на проводе тот же.

## Зачем

NuGet-версия помечает почти все свои форматтеры `[Preserve]` и регистрирует их в статическом конструкторе `MemoryPackFormatterProvider`. Поэтому Unity-линкер не может их вырезать, и в WebGL-билд попадали System.Collections.Immutable, Brotli (нативный энкодер), BigInteger и System.Numerics, Uri, TimeZoneInfo, CultureInfo, Regex (через `TypeFormatter`) и кортежи до 8 элементов. Это около 200 KB br кода и метаданных. `link.xml` тут не помогает: он умеет только сохранять, а не снимать `[Preserve]`.

Исходники нельзя положить прямо в Unity: они требуют C# 11, а Unity компилирует на C# 9. Поэтому DLL собирается отдельно обычным dotnet SDK.

## Что удалено относительно оригинала

- `Compression/`: Brotli-компрессор, декомпрессор и форматтеры. `BitPackFormatter` оставлен. Из `StringFormatter.cs` удалён `BrotliStringFormatter`, из `CustomFormatterAttributes.cs` — Brotli-атрибуты.
- Форматтеры BigInteger, BitArray, CultureInfo, TimeZoneInfo, Uri, Version, Type, StringBuilder, Immutable- и Frozen-коллекций, Lazy, Tuple/ValueTuple, многомерных массивов.
- `MemoryPackFormatterProvider`: регистрации этих типов и System.Numerics (Complex, Vector*, Matrix*, Plane, Quaternion), запасной путь для Lazy, кортежей, Immutable и многомерных массивов.
- Шаблоны `*.tt` (сгенерированные `.cs` остались).

Протокол (`shared/`) использует только примитивы, `string`, `Guid`, `DateTime`, `TimeSpan`, массивы, `List`, `Dictionary`, `IReadOnlyList`, `IReadOnlyDictionary`, `Nullable` и юнионы. Если в протокол добавится тип из удалённого списка, Shared.Game в Unity не скомпилируется (форматтер не найден генератором) или сериализация бросит `MemoryPackSerializationException` «not registered». Тогда вернуть нужный файл из оригинального репозитория.

## Сборка

```bash
tools/memorypack-core/build.sh
```

Скрипт кладёт DLL в `client/Assets/Plugins/MemoryPack/MemoryPack.Core.1.21.4/lib/netstandard2.1/`. `.meta` остаётся прежним. При обновлении MemoryPack: взять `src/MemoryPack.Core` нужной версии, повторить удаления из списка выше и обновить версию генератора в `client/Assets/Plugins/MemoryPack/` на ту же.
