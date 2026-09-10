using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ContainerGenerator {
    internal sealed class LocationInfo : IEquatable<LocationInfo> {
        public string FilePath { get; }
        public TextSpan TextSpan { get; }
        public LinePositionSpan LineSpan { get; }

        private LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan) {
            FilePath = filePath;
            TextSpan = textSpan;
            LineSpan = lineSpan;
        }

        public Location ToLocation() {
            return Location.Create(FilePath, TextSpan, LineSpan);
        }

        public static LocationInfo? CreateFrom(Location? location) {
            if (location?.SourceTree == null)
                return null;

            return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
        }

        public bool Equals(LocationInfo? other) {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return FilePath == other.FilePath &&
                   TextSpan.Equals(other.TextSpan) &&
                   LineSpan.Equals(other.LineSpan);
        }

        public override bool Equals(object? obj) {
            return Equals(obj as LocationInfo);
        }

        public override int GetHashCode() {
            return HashCodes.Combine(HashCodes.Of(FilePath), TextSpan.GetHashCode(), LineSpan.GetHashCode());
        }
    }
}
