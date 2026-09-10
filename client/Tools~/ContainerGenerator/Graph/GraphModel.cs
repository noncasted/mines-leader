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
        public string TypeMap = "";
        public List<string> ServiceMaps = new List<string>();
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
                TypeMap = TypeMap,
                Location = Location,
            };
            copy.ServiceTypes.AddRange(ServiceTypes);
            copy.ServiceMaps.AddRange(ServiceMaps);
            for (var i = 0; i < Arms.Count; i++)
                copy.Arms.Add(Arms[i].Clone());
            return copy;
        }
    }

    internal sealed class GraphSwitchArm {
        public string Discriminant = "";
        public string ImplementationType = "";
        public string ParameterExpression = "";
        public string ParameterType = "";

        public GraphSwitchArm Clone() {
            return new GraphSwitchArm {
                Discriminant = Discriminant,
                ImplementationType = ImplementationType,
                ParameterExpression = ParameterExpression,
                ParameterType = ParameterType,
            };
        }
    }

    internal sealed class GraphEdge {
        public string ParameterName = "";
        public string ParameterType = "";
        public string Source = "Constructor";
        public string Kind = "Registration";
        public int Arm = -1;
        public int TargetIndex = -1;
        public string ParentRootId = "";
        public List<int> CollectionIndices = new List<int>();
    }

    internal sealed class GraphMethod {
        public string Id = "";
        public string OriginId = "";
        public string ParentHint = "";
        public string ParentId = "";
        public string ParentAssembly = "";
        public string AssemblyName = "";
        public bool ParentUnresolved;
        public bool IsRoot;
        public string File = "";
        public int Line;
        public string ViewType = "";
        public string Variant = "";
        public List<GraphRegistration> Registrations = new List<GraphRegistration>();
        public List<string> Calls = new List<string>();
        public List<int> CallOrdinals = new List<int>();
        public List<string> CallTypeArguments = new List<string>();
        public List<string> CallAsServices = new List<string>();
        public List<string> CallHoles = new List<string>();
        public int ReturnedOrdinal = -1;

        public void AddCall(
            string id,
            int ordinal,
            string typeArguments = "",
            string asServices = "",
            string hole = "") {
            if (string.IsNullOrEmpty(id))
                return;

            Calls.Add(id);
            CallOrdinals.Add(ordinal);
            CallTypeArguments.Add(typeArguments ?? "");
            CallAsServices.Add(asServices ?? "");
            CallHoles.Add(hole ?? "");
        }

        public void PadCallLists() {
            while (CallOrdinals.Count < Calls.Count)
                CallOrdinals.Add(int.MaxValue);
            while (CallTypeArguments.Count < Calls.Count)
                CallTypeArguments.Add("");
            while (CallAsServices.Count < Calls.Count)
                CallAsServices.Add("");
            while (CallHoles.Count < Calls.Count)
                CallHoles.Add("");
        }

        public string CallTypesAt(int index) {
            PadCallLists();
            if (index < 0 || index >= CallTypeArguments.Count)
                return "";
            return CallTypeArguments[index];
        }

        public string CallAsAt(int index) {
            PadCallLists();
            if (index < 0 || index >= CallAsServices.Count)
                return "";
            return CallAsServices[index];
        }

        public string CallHoleAt(int index) {
            PadCallLists();
            if (index < 0 || index >= CallHoles.Count)
                return "";
            return CallHoles[index];
        }

        public void AddAsToCall(int index, string service) {
            PadCallLists();
            if (index < 0 || index >= CallAsServices.Count)
                return;
            if (string.IsNullOrEmpty(service))
                return;
            if (string.IsNullOrEmpty(CallAsServices[index])) {
                CallAsServices[index] = service;
                return;
            }

            if (CallAsServices[index].IndexOf(service, System.StringComparison.Ordinal) >= 0)
                return;
            CallAsServices[index] = CallAsServices[index] + ";" + service;
        }

        public void AddHoleToCall(int index, string hole) {
            PadCallLists();
            if (index < 0 || index >= CallHoles.Count)
                return;
            if (string.IsNullOrEmpty(hole))
                return;
            if (string.IsNullOrEmpty(CallHoles[index])) {
                CallHoles[index] = hole;
                return;
            }

            CallHoles[index] = CallHoles[index] + "; " + hole;
        }
    }

    internal sealed class GraphDocument {
        public string AssemblyName = "";
        public List<GraphMethod> Methods = new List<GraphMethod>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();
    }

    internal sealed class ScopeGraph {
        public string RootId = "";
        public string ParentId = "";
        public bool ParentMissing;
        public string Variant = "";
        public string ViewType = "";
        public List<GraphRegistration> Registrations = new List<GraphRegistration>();
        public List<int> ConstructionOrder = new List<int>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();
    }
}
