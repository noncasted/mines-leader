using System.Collections.Generic;

namespace ContainerGenerator {
    internal sealed class GraphRegistration {
        public string Kind = "Register";
        public string ImplementationType = "";
        public List<string> ServiceTypes = new List<string>();
        public string Lifetime = "Singleton";
        public string Origin = "ConstructedType";
        public string Source = "";
        public string Hole = "";
        public string File = "";
        public int Line;
        public int Ordinal;
        public LocationInfo? Location;
        public List<GraphSwitchArm> Arms = new List<GraphSwitchArm>();
        public List<GraphEdge> Dependencies = new List<GraphEdge>();

        public GraphRegistration Clone() {
            var copy = new GraphRegistration {
                Kind = Kind,
                ImplementationType = ImplementationType,
                Lifetime = Lifetime,
                Origin = Origin,
                Source = Source,
                Hole = Hole,
                File = File,
                Line = Line,
                Ordinal = Ordinal,
                Location = Location,
            };
            copy.ServiceTypes.AddRange(ServiceTypes);
            for (var i = 0; i < Arms.Count; i++)
                copy.Arms.Add(Arms[i].Clone());
            return copy;
        }
    }

    internal sealed class GraphSwitchArm {
        public string Discriminant = "";
        public string ImplementationType = "";
        public string ParameterExpression = "";

        public GraphSwitchArm Clone() {
            return new GraphSwitchArm {
                Discriminant = Discriminant,
                ImplementationType = ImplementationType,
                ParameterExpression = ParameterExpression,
            };
        }
    }

    internal sealed class GraphEdge {
        public string ParameterName = "";
        public string ParameterType = "";
        public string Source = "Constructor";
        public string Kind = "Registration";
        public int TargetIndex = -1;
        public List<int> CollectionIndices = new List<int>();
    }

    internal sealed class GraphMethod {
        public string Id = "";
        public string OriginId = "";
        public bool IsRoot;
        public string File = "";
        public int Line;
        public string ViewType = "";
        public string Variant = "";
        public List<GraphRegistration> Registrations = new List<GraphRegistration>();
        public List<string> Calls = new List<string>();
        public List<int> CallOrdinals = new List<int>();
    }

    internal sealed class GraphDocument {
        public string AssemblyName = "";
        public List<GraphMethod> Methods = new List<GraphMethod>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();
    }

    internal sealed class ScopeGraph {
        public string RootId = "";
        public string Variant = "";
        public string ViewType = "";
        public List<GraphRegistration> Registrations = new List<GraphRegistration>();
        public List<int> ConstructionOrder = new List<int>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();
    }
}
