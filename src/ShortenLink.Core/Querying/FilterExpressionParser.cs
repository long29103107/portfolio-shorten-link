using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace ShortenLink.Core.Querying;

public static class FilterExpressionParser
{
    public static Expression<Func<T, bool>> Parse<T>(string expression, IEnumerable<string> allowedProperties)
    {
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("Filter expression is required.", nameof(expression));
        var parser = new Parser<T>(expression, allowedProperties);
        return parser.Parse();
    }

    public static IQueryable<T> ApplyFilter<T>(
        this IQueryable<T> source,
        string? expression,
        IEnumerable<string> allowedProperties)
    {
        ArgumentNullException.ThrowIfNull(source);
        return string.IsNullOrWhiteSpace(expression) ? source : source.Where(Parse<T>(expression, allowedProperties));
    }

    private sealed class Parser<T>(string source, IEnumerable<string> allowedProperties)
    {
        private readonly HashSet<string> _allowed = new(allowedProperties, StringComparer.OrdinalIgnoreCase);
        private readonly ParameterExpression _parameter = Expression.Parameter(typeof(T), "item");
        private int _position;

        public Expression<Func<T, bool>> Parse()
        {
            var body = ParseOr();
            SkipWhitespace();
            if (_position != source.Length) Fail("Unexpected input");
            return Expression.Lambda<Func<T, bool>>(body, _parameter);
        }

        private Expression ParseOr()
        {
            var left = ParseAnd();
            while (TryConsume('|')) left = Expression.OrElse(left, ParseAnd());
            return left;
        }

        private Expression ParseAnd()
        {
            var left = ParseUnary();
            while (TryConsume('&')) left = Expression.AndAlso(left, ParseUnary());
            return left;
        }

        private Expression ParseUnary()
        {
            if (TryConsume('!')) return Expression.Not(ParseUnary());
            return ParsePrimary();
        }

        private Expression ParsePrimary()
        {
            SkipWhitespace();
            if (!TryConsume('(')) Fail("Expected '('");
            SkipWhitespace();
            if (_position < source.Length && (source[_position] == '(' || source[_position] == '!'))
            {
                var grouped = ParseOr();
                if (!TryConsume(')')) Fail("Expected ')' after group");
                return grouped;
            }
            var propertyName = ReadToken();
            var operation = ReadToken();
            var rawValue = ReadQuotedValue();
            if (!TryConsume(')')) Fail("Expected ')'");
            return BuildCondition(propertyName, operation, rawValue);
        }

        private Expression BuildCondition(string propertyName, string operation, string rawValue)
        {
            if (!_allowed.Contains(propertyName)) Fail($"Filtering by '{propertyName}' is not allowed");
            Expression property = _parameter;
            var type = typeof(T);
            foreach (var segment in propertyName.Split('.'))
            {
                var info = type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                    ?? throw new ArgumentException($"Property '{propertyName}' does not exist.", nameof(source));
                property = Expression.Property(property, info);
                type = info.PropertyType;
            }

            if (operation.Equals("in", StringComparison.OrdinalIgnoreCase))
            {
                var values = rawValue.Trim().TrimStart('[').TrimEnd(']')
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => ConvertValue(value, type)).ToArray();
                var array = Array.CreateInstance(type, values.Length);
                for (var index = 0; index < values.Length; index++) array.SetValue(values[index], index);
                return Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [type], Expression.Constant(array), property);
            }

            var convertedValue = ConvertValue(rawValue, type);
            Expression constant = convertedValue is null
                ? Expression.Constant(null, type)
                : Nullable.GetUnderlyingType(type) is { } nullableType
                    ? Expression.Convert(Expression.Constant(convertedValue, nullableType), type)
                    : Expression.Constant(convertedValue, type);
            return operation.ToLowerInvariant() switch
            {
                "eq" => Expression.Equal(property, constant),
                "ne" => Expression.NotEqual(property, constant),
                "gt" => Expression.GreaterThan(property, constant),
                "ge" => Expression.GreaterThanOrEqual(property, constant),
                "lt" => Expression.LessThan(property, constant),
                "le" => Expression.LessThanOrEqual(property, constant),
                "contains" when type == typeof(string) => Expression.Call(property, nameof(string.Contains), Type.EmptyTypes, constant),
                "startswith" when type == typeof(string) => Expression.Call(property, nameof(string.StartsWith), Type.EmptyTypes, constant),
                _ => throw new ArgumentException($"Operator '{operation}' is not supported for '{propertyName}'.", nameof(source))
            };
        }

        private static object? ConvertValue(string value, Type targetType)
        {
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (value.Equals("null", StringComparison.OrdinalIgnoreCase) && Nullable.GetUnderlyingType(targetType) is not null) return null;
            if (underlying == typeof(string)) return value;
            if (underlying == typeof(Guid)) return Guid.Parse(value);
            if (underlying == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
            if (underlying == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
            if (underlying.IsEnum) return Enum.Parse(underlying, value, true);
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }

        private string ReadToken()
        {
            SkipWhitespace();
            var start = _position;
            while (_position < source.Length && !char.IsWhiteSpace(source[_position]) && source[_position] != ')') _position++;
            if (start == _position) Fail("Expected token");
            return source[start.._position];
        }

        private string ReadQuotedValue()
        {
            SkipWhitespace();
            if (_position >= source.Length || source[_position] != '`') Fail("Filter values must be wrapped in backticks");
            _position++;
            var start = _position;
            while (_position < source.Length && source[_position] != '`') _position++;
            if (_position >= source.Length) Fail("Unterminated filter value");
            var value = source[start.._position];
            _position++;
            return value;
        }

        private bool TryConsume(char expected)
        {
            SkipWhitespace();
            if (_position >= source.Length || source[_position] != expected) return false;
            _position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < source.Length && char.IsWhiteSpace(source[_position])) _position++;
        }

        private void Fail(string message) => throw new ArgumentException($"{message} at position {_position}.", nameof(source));
    }
}
