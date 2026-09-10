using System;

namespace ContainerGenerator {
    internal sealed class ParameterModel : IEquatable<ParameterModel> {
        public string TypeFullName { get; }
        public string Name { get; }

        public ParameterModel(string typeFullName, string name) {
            TypeFullName = typeFullName;
            Name = name;
        }

        public bool Equals(ParameterModel? other) {
            return other != null && TypeFullName == other.TypeFullName && Name == other.Name;
        }

        public override bool Equals(object? obj) {
            return Equals(obj as ParameterModel);
        }

        public override int GetHashCode() {
            return HashCodes.Combine(HashCodes.Of(TypeFullName), HashCodes.Of(Name));
        }
    }

    internal sealed class InjectorModel : IEquatable<InjectorModel> {
        public string TypeName { get; }
        public string FullTypeName { get; }
        public string Namespace { get; }
        public string SymbolName { get; }
        public bool IsNested { get; }
        public bool IsAbstract { get; }
        public bool IsGeneric { get; }
        public bool IsUnityObject { get; }
        public bool HasConstruct { get; }
        public bool ConstructAccessible { get; }
        public bool ConstructIsGeneric { get; }
        public bool MultipleConstruct { get; }
        public bool ConstructorFound { get; }
        public bool ConstructorAccessible { get; }
        public EquatableArray<ParameterModel> ConstructorParameters { get; }
        public EquatableArray<ParameterModel> ConstructParameters { get; }
        public LocationInfo? Location { get; }

        public InjectorModel(
            string typeName,
            string fullTypeName,
            string ns,
            string symbolName,
            bool isNested,
            bool isAbstract,
            bool isGeneric,
            bool isUnityObject,
            bool hasConstruct,
            bool constructAccessible,
            bool constructIsGeneric,
            bool multipleConstruct,
            bool constructorFound,
            bool constructorAccessible,
            EquatableArray<ParameterModel> constructorParameters,
            EquatableArray<ParameterModel> constructParameters,
            LocationInfo? location) {
            TypeName = typeName;
            FullTypeName = fullTypeName;
            Namespace = ns;
            SymbolName = symbolName;
            IsNested = isNested;
            IsAbstract = isAbstract;
            IsGeneric = isGeneric;
            IsUnityObject = isUnityObject;
            HasConstruct = hasConstruct;
            ConstructAccessible = constructAccessible;
            ConstructIsGeneric = constructIsGeneric;
            MultipleConstruct = multipleConstruct;
            ConstructorFound = constructorFound;
            ConstructorAccessible = constructorAccessible;
            ConstructorParameters = constructorParameters;
            ConstructParameters = constructParameters;
            Location = location;
        }

        public string HintName => TypeNames.SafeHint(FullTypeName) + "GeneratedInjector.g.cs";

        public string InjectorTypeName => TypeNames.SafeHint(FullTypeName) + "GeneratedInjector";

        public bool Equals(InjectorModel? other) {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return TypeName == other.TypeName &&
                   FullTypeName == other.FullTypeName &&
                   Namespace == other.Namespace &&
                   SymbolName == other.SymbolName &&
                   IsNested == other.IsNested &&
                   IsAbstract == other.IsAbstract &&
                   IsGeneric == other.IsGeneric &&
                   IsUnityObject == other.IsUnityObject &&
                   HasConstruct == other.HasConstruct &&
                   ConstructAccessible == other.ConstructAccessible &&
                   ConstructIsGeneric == other.ConstructIsGeneric &&
                   MultipleConstruct == other.MultipleConstruct &&
                   ConstructorFound == other.ConstructorFound &&
                   ConstructorAccessible == other.ConstructorAccessible &&
                   ConstructorParameters.Equals(other.ConstructorParameters) &&
                   ConstructParameters.Equals(other.ConstructParameters);
        }

        public override bool Equals(object? obj) {
            return Equals(obj as InjectorModel);
        }

        public override int GetHashCode() {
            var hash = HashCodes.Of(FullTypeName);
            hash = HashCodes.Combine(hash, HashCodes.Of(IsNested));
            hash = HashCodes.Combine(hash, HashCodes.Of(IsGeneric));
            hash = HashCodes.Combine(hash, HashCodes.Of(HasConstruct));
            hash = HashCodes.Combine(hash, ConstructorParameters.GetHashCode());
            hash = HashCodes.Combine(hash, ConstructParameters.GetHashCode());
            return hash;
        }
    }

    internal sealed class GeneratedInjectorSource : IEquatable<GeneratedInjectorSource> {
        public string HintName { get; }
        public string? Source { get; }
        public InjectorModel Model { get; }
        public EquatableArray<DiagnosticInfo> Diagnostics { get; }

        public GeneratedInjectorSource(
            string hintName,
            string? source,
            InjectorModel model,
            EquatableArray<DiagnosticInfo> diagnostics) {
            HintName = hintName;
            Source = source;
            Model = model;
            Diagnostics = diagnostics;
        }

        public bool Equals(GeneratedInjectorSource? other) {
            if (other == null)
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return HintName == other.HintName &&
                   Source == other.Source &&
                   Model.Equals(other.Model) &&
                   Diagnostics.Equals(other.Diagnostics);
        }

        public override bool Equals(object? obj) {
            return Equals(obj as GeneratedInjectorSource);
        }

        public override int GetHashCode() {
            return HashCodes.Combine(HashCodes.Of(HintName), HashCodes.Of(Source), Model.GetHashCode(), Diagnostics.GetHashCode());
        }
    }

    internal sealed class AssemblyEmitContext : IEquatable<AssemblyEmitContext> {
        public string AssemblyName { get; }
        public bool ShouldProcess { get; }
        public bool HasUnityInit { get; }

        public AssemblyEmitContext(string assemblyName, bool shouldProcess, bool hasUnityInit) {
            AssemblyName = assemblyName;
            ShouldProcess = shouldProcess;
            HasUnityInit = hasUnityInit;
        }

        public bool Equals(AssemblyEmitContext? other) {
            return other != null &&
                   AssemblyName == other.AssemblyName &&
                   ShouldProcess == other.ShouldProcess &&
                   HasUnityInit == other.HasUnityInit;
        }

        public override bool Equals(object? obj) {
            return Equals(obj as AssemblyEmitContext);
        }

        public override int GetHashCode() {
            return HashCodes.Combine(HashCodes.Of(AssemblyName), HashCodes.Of(ShouldProcess), HashCodes.Of(HasUnityInit));
        }
    }
}
