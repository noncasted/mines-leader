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
        public List<GraphSwitchArm> Arms = new List<GraphSwitchArm>();
    }

    internal sealed class GraphSwitchArm {
        public string Discriminant = "";
        public string ImplementationType = "";
        public string ParameterExpression = "";
    }

    internal sealed class GraphMethod {
        public string Id = "";
        public bool IsRoot;
        public string File = "";
        public int Line;
        public List<GraphRegistration> Registrations = new List<GraphRegistration>();
        public List<string> Calls = new List<string>();
    }

    internal sealed class GraphDocument {
        public string AssemblyName = "";
        public List<GraphMethod> Methods = new List<GraphMethod>();
        public List<DiagnosticInfo> Diagnostics = new List<DiagnosticInfo>();
    }
}
