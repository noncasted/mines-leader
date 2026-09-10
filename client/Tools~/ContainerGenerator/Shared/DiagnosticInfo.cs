using System;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo> {
        public DiagnosticDescriptor Descriptor { get; }
        public LocationInfo? Location { get; }
        public EquatableArray<string> MessageArgs { get; }

        public DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] messageArgs) {
            Descriptor = descriptor;
            Location = location;
            MessageArgs = new EquatableArray<string>(messageArgs ?? Array.Empty<string>());
        }

        public Diagnostic ToDiagnostic() {
            var location = Location != null ? Location.ToLocation() : Microsoft.CodeAnalysis.Location.None;
            var args = new object[MessageArgs.Count];
            for (var i = 0; i < args.Length; i++)
                args[i] = MessageArgs[i];

            return Diagnostic.Create(Descriptor, location, args);
        }

        public bool Equals(DiagnosticInfo? other) {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return ReferenceEquals(Descriptor, other.Descriptor) &&
                   Equals(Location, other.Location) &&
                   MessageArgs.Equals(other.MessageArgs);
        }

        public override bool Equals(object? obj) {
            return Equals(obj as DiagnosticInfo);
        }

        public override int GetHashCode() {
            return HashCodes.Combine(Descriptor.GetHashCode(), Location == null ? 0 : Location.GetHashCode(), MessageArgs.GetHashCode());
        }
    }
}
