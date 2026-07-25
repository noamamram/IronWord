namespace TypingDefense.Domain.Common
{
    public readonly struct ThreatId : System.IEquatable<ThreatId>
    {
        public int Value { get; }

        public ThreatId(int value) => Value = value;

        public bool Equals(ThreatId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ThreatId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(ThreatId left, ThreatId right) => left.Equals(right);
        public static bool operator !=(ThreatId left, ThreatId right) => !left.Equals(right);
    }

    public readonly struct InterceptorId : System.IEquatable<InterceptorId>
    {
        public int Value { get; }

        public InterceptorId(int value) => Value = value;

        public bool Equals(InterceptorId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is InterceptorId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();

        public static bool operator ==(InterceptorId left, InterceptorId right) => left.Equals(right);
        public static bool operator !=(InterceptorId left, InterceptorId right) => !left.Equals(right);
    }

    public readonly struct DefinitionId : System.IEquatable<DefinitionId>
    {
        public string Value { get; }

        public DefinitionId(string value) => Value = value ?? string.Empty;

        public bool Equals(DefinitionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is DefinitionId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value;

        public static bool operator ==(DefinitionId left, DefinitionId right) => left.Equals(right);
        public static bool operator !=(DefinitionId left, DefinitionId right) => !left.Equals(right);
    }
}
