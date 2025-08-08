using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using OpenVpn.Options.Converters;
using PinkSystem.Runtime;

namespace OpenVpn.Options
{
    internal sealed class OptionsSerializer
    {
        private static readonly Dictionary<Type, IOptionConverter> _defaultConverters = new()
        {
            { typeof(int), new ParseOptionConverter<int>() },
            { typeof(uint), new ParseOptionConverter<uint>() },
            { typeof(long), new ParseOptionConverter<long>() },
            { typeof(ulong), new ParseOptionConverter<ulong>() },
            { typeof(bool), new BoolOptionConverter() },
            { typeof(string), new StringOptionConverter() },
            { typeof(Enum), new EnumOptionConverter() }
        };
        private IEnumerator<KeyValuePair<string, IReadOnlyList<string>?>> _enumerator = null!;
        private object _instance = null!;
        private ImmutableDictionary<string, Property> _properties = null!;
        private Dictionary<string, IReadOnlyList<string>?> _unknownOptions = null!;

        private sealed class Property
        {
            public required Type Type { get; init; }
            public required bool Required { get; init; }
            public required IOptionConverter Converter { get; init; }
            public required IReadOnlyList<string> Names { get; init; }
            public required MemberAccessor Accessor { get; init; }
        }

        public IReadOnlyDictionary<string, IReadOnlyList<string>?> UnknownOptions => _unknownOptions;
        private string Name => _enumerator.Current.Key;
        private IReadOnlyList<string>? Value => _enumerator.Current.Value;

        public T Serialize<T>(IReadOnlyDictionary<string, IReadOnlyList<string>?> options)
        {
            _enumerator = options.GetEnumerator();
            _instance = ObjectAccessor.Create(typeof(T)).Instance!;

            _properties = typeof(T)
                .GetProperties()
                .Select(x =>
                {
                    var targetType = x.PropertyType;

                    var required = x.GetCustomAttribute<RequiredAttribute>() != null;

                    var converterAttribute = x.GetCustomAttribute<ConverterAttribute>();
                    var converter = (IOptionConverter?)null;

                    if (converterAttribute != null)
                    {
                        converter = (IOptionConverter)ObjectAccessor.Create(converterAttribute.ConverterType, converterAttribute.Args).Instance!;
                    }
                    else
                    {
                        var underlyingType = Nullable.GetUnderlyingType(targetType);

                        if (underlyingType == null)
                            underlyingType = targetType;

                        if (underlyingType.IsEnum)
                            underlyingType = typeof(Enum);

                        if (!_defaultConverters.TryGetValue(underlyingType, out converter))
                            throw new NotSupportedException($"Cannot find default option converter for type {underlyingType}");
                    }

                    var names = x.GetCustomAttributes<NameAttribute>().Select(x => x.Name).ToArray();

                    if (names.Length == 0)
                        throw new NotSupportedException("Property should have 1 or more names");

                    return new Property()
                    {
                        Type = targetType,
                        Names = names,
                        Converter = converter,
                        Required = required,
                        Accessor = MemberAccessorsCache.Shared.Create(x)
                    };
                })
                .SelectMany(x => x.Names.Select(c => new KeyValuePair<string, Property>(c, x)))
                .ToImmutableDictionary();

            _unknownOptions = new();

            while (_enumerator.MoveNext())
                SerializeCurrent();

            return (T)_instance;
        }

        private void SerializeCurrent()
        {
            if (!_properties.TryGetValue(Name, out var property))
            {
                _unknownOptions.Add(Name, Value);
                return;
            }

            var value = property.Converter.Convert(Name, Value, property.Type);

            if (property.Required && value == null)
                throw new FormatException($"'{Name}' option cannot be null or empty");

            property.Accessor.SetValue(_instance, value);
        }
    }
}
