namespace Apollo.Testing;

/// <summary>
/// Base for fluent test data builders: <see cref="Build"/> starts from <see cref="CreateDefault"/> (a valid object
/// with sensible defaults) and applies every override in the order it was added. Each call builds a new object.
/// </summary>
/// <typeparam name="TBuilder">The derived builder, so that <see cref="With"/> chains with its own methods.</typeparam>
/// <typeparam name="T">The type being built, typically a record.</typeparam>
/// <example>
/// <code>
/// public sealed class MessageEventBuilder : TestDataBuilder&lt;MessageEventBuilder, MessageEvent&gt;
/// {
///     public MessageEventBuilder ByUser(UserId user) => With(e => e with { UserId = user });
///
///     public MessageEventBuilder At(DateTimeOffset occurredAt) => With(e => e with { OccurredAt = occurredAt });
///
///     protected override MessageEvent CreateDefault() => new(UserId: new UserId(1), OccurredAt: TestTime.Now);
/// }
///
/// var message = new MessageEventBuilder().ByUser(alice).Build();
/// var late = new MessageEventBuilder().With(e => e with { OccurredAt = TestTime.Now.AddHours(20) }).Build();
/// </code>
/// </example>
public abstract class TestDataBuilder<TBuilder, T>
    where TBuilder : TestDataBuilder<TBuilder, T>
{
    private readonly List<Func<T, T>> _overrides = [];

    /// <summary>
    /// Adds an override. Derived builders expose named methods built on this; tests may also call it directly.
    /// </summary>
    public TBuilder With(Func<T, T> change)
    {
        _overrides.Add(change);
        return (TBuilder)this;
    }

    public T Build() => _overrides.Aggregate(CreateDefault(), (value, change) => change(value));

    /// <summary>
    /// A new, valid object with defaults. Called once per <see cref="Build"/>.
    /// </summary>
    protected abstract T CreateDefault();
}
