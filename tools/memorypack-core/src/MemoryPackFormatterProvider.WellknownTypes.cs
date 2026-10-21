using MemoryPack.Formatters;

namespace MemoryPack;

public static partial class MemoryPackFormatterProvider
{
    internal static void RegisterWellKnownTypesFormatters()
    {
        Register(new UnmanagedFormatter<SByte>());
        Register(new UnmanagedArrayFormatter<SByte>());
        Register(new NullableFormatter<SByte>());
        Register(new UnmanagedFormatter<Byte>());
        Register(new UnmanagedArrayFormatter<Byte>());
        Register(new NullableFormatter<Byte>());
        Register(new UnmanagedFormatter<Int16>());
        Register(new UnmanagedArrayFormatter<Int16>());
        Register(new NullableFormatter<Int16>());
        Register(new UnmanagedFormatter<UInt16>());
        Register(new UnmanagedArrayFormatter<UInt16>());
        Register(new NullableFormatter<UInt16>());
        Register(new UnmanagedFormatter<Int32>());
        Register(new UnmanagedArrayFormatter<Int32>());
        Register(new NullableFormatter<Int32>());
        Register(new UnmanagedFormatter<UInt32>());
        Register(new UnmanagedArrayFormatter<UInt32>());
        Register(new NullableFormatter<UInt32>());
        Register(new UnmanagedFormatter<Int64>());
        Register(new UnmanagedArrayFormatter<Int64>());
        Register(new NullableFormatter<Int64>());
        Register(new UnmanagedFormatter<UInt64>());
        Register(new UnmanagedArrayFormatter<UInt64>());
        Register(new NullableFormatter<UInt64>());
        Register(new UnmanagedFormatter<Char>());
        Register(new UnmanagedArrayFormatter<Char>());
        Register(new NullableFormatter<Char>());
        Register(new UnmanagedFormatter<Single>());
        Register(new UnmanagedArrayFormatter<Single>());
        Register(new NullableFormatter<Single>());
        Register(new UnmanagedFormatter<Double>());
        Register(new UnmanagedArrayFormatter<Double>());
        Register(new NullableFormatter<Double>());
        Register(new UnmanagedFormatter<Decimal>());
        Register(new UnmanagedArrayFormatter<Decimal>());
        Register(new NullableFormatter<Decimal>());
        Register(new UnmanagedFormatter<Boolean>());
        Register(new UnmanagedArrayFormatter<Boolean>());
        Register(new NullableFormatter<Boolean>());
        Register(new UnmanagedFormatter<IntPtr>());
        Register(new UnmanagedArrayFormatter<IntPtr>());
        Register(new NullableFormatter<IntPtr>());
        Register(new UnmanagedFormatter<UIntPtr>());
        Register(new UnmanagedArrayFormatter<UIntPtr>());
        Register(new NullableFormatter<UIntPtr>());
        Register(new UnmanagedFormatter<DateTime>());
        Register(new UnmanagedArrayFormatter<DateTime>());
        Register(new NullableFormatter<DateTime>());
        Register(new UnmanagedFormatter<DateTimeOffset>());
        Register(new UnmanagedArrayFormatter<DateTimeOffset>());
        Register(new NullableFormatter<DateTimeOffset>());
        Register(new UnmanagedFormatter<TimeSpan>());
        Register(new UnmanagedArrayFormatter<TimeSpan>());
        Register(new NullableFormatter<TimeSpan>());
        Register(new UnmanagedFormatter<Guid>());
        Register(new UnmanagedArrayFormatter<Guid>());
        Register(new NullableFormatter<Guid>());
#if NET7_0_OR_GREATER
        Register(new UnmanagedFormatter<Rune>());
        Register(new UnmanagedArrayFormatter<Rune>());
        Register(new NullableFormatter<Rune>());
        Register(new UnmanagedFormatter<DateOnly>());
        Register(new UnmanagedArrayFormatter<DateOnly>());
        Register(new NullableFormatter<DateOnly>());
        Register(new UnmanagedFormatter<TimeOnly>());
        Register(new UnmanagedArrayFormatter<TimeOnly>());
        Register(new NullableFormatter<TimeOnly>());
        Register(new UnmanagedFormatter<Half>());
        Register(new UnmanagedArrayFormatter<Half>());
        Register(new NullableFormatter<Half>());
        Register(new UnmanagedFormatter<Int128>());
        Register(new UnmanagedArrayFormatter<Int128>());
        Register(new NullableFormatter<Int128>());
        Register(new UnmanagedFormatter<UInt128>());
        Register(new UnmanagedArrayFormatter<UInt128>());
        Register(new NullableFormatter<UInt128>());
#endif
        Register(new StringFormatter());
        Register(new ArrayFormatter<String>());
    }
}
