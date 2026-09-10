using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ContainerGenerator {
    internal static class ManifestReader {
        public static void Merge(GraphDocument document, Compilation compilation) {
            if (document == null || compilation == null)
                return;

            var existing = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                existing[document.Methods[i].Id] = document.Methods[i];

            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols) {
                foreach (var attribute in assembly.GetAttributes()) {
                    if (IsInstallerAttribute(attribute) == false)
                        continue;

                    var method = Read(attribute, compilation, assembly.Name);
                    if (method == null || string.IsNullOrEmpty(method.Id))
                        continue;
                    if (existing.TryGetValue(method.Id, out var local)) {
                        if (local.Registrations.Count == 0 && method.Registrations.Count > 0) {
                            local.Registrations.AddRange(method.Registrations);
                            local.Calls.AddRange(method.Calls);
                            local.CallOrdinals.AddRange(method.CallOrdinals);
                            local.CallTypeArguments.AddRange(method.CallTypeArguments);
                            local.CallAsServices.AddRange(method.CallAsServices);
                            local.CallHoles.AddRange(method.CallHoles);
                            if (local.ReturnedOrdinal < 0)
                                local.ReturnedOrdinal = method.ReturnedOrdinal;
                            if (string.IsNullOrEmpty(local.File))
                                local.File = method.File;
                            if (local.Line == 0)
                                local.Line = method.Line;
                        }

                        if (string.IsNullOrEmpty(local.ParentHint) && string.IsNullOrEmpty(method.ParentId) == false)
                            local.ParentHint = method.ParentId;
                        if (string.IsNullOrEmpty(local.ParentId) && string.IsNullOrEmpty(method.ParentId) == false)
                            local.ParentId = method.ParentId;
                        continue;
                    }

                    existing[method.Id] = method;
                    document.Methods.Add(method);
                }
            }
        }

        public static void ReportUnresolved(GraphDocument document) {
            if (document == null)
                return;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++)
                ids.Add(document.Methods[i].Id);

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                for (var c = 0; c < method.Calls.Count; c++) {
                    var call = method.Calls[c];
                    if (ids.Contains(call))
                        continue;
                    if (ScopePlan.IsHarvest(call))
                        continue;

                    document.Diagnostics.Add(new DiagnosticInfo(
                        GraphDescriptors.UnresolvedInstaller,
                        null,
                        call,
                        method.Id,
                        method.File ?? "",
                        method.Line.ToString(CultureInfo.InvariantCulture)));
                }
            }
        }

        private static bool IsInstallerAttribute(AttributeData attribute) {
            var type = attribute.AttributeClass;
            if (type == null)
                return false;
            if (type.Name != "ContainerInstallerAttribute")
                return false;
            var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
            return ns == "Internal";
        }

        private static GraphMethod? Read(AttributeData attribute, Compilation compilation, string assemblyName) {
            if (attribute.ConstructorArguments.Length < 8)
                return null;

            var methodId = AsString(attribute.ConstructorArguments[0]);
            var isRoot = AsBool(attribute.ConstructorArguments[1]);
            var file = AsString(attribute.ConstructorArguments[2]);
            var line = AsInt(attribute.ConstructorArguments[3]);
            var implementations = AsTypes(attribute.ConstructorArguments[4], compilation);
            var services = AsTypes(attribute.ConstructorArguments[5], compilation);
            var calls = AsStrings(attribute.ConstructorArguments[6]);
            var blob = AsString(attribute.ConstructorArguments[7]);

            var method = new GraphMethod {
                Id = methodId,
                IsRoot = isRoot,
                File = file,
                Line = line,
                AssemblyName = assemblyName ?? "",
            };
            method.Calls.AddRange(calls);
            ParseBlob(method, blob, implementations, services);
            return method;
        }

        private static void ParseBlob(
            GraphMethod method,
            string blob,
            List<string> implementations,
            List<string> services) {
            var lines = (blob ?? "").Replace("\r", "").Split('\n');
            var index = 0;
            if (lines.Length == 0)
                return;
            if (lines[0] != GraphEmitter.BlobVersion)
                return;
            index = 1;
            if (index < lines.Length && lines[index].StartsWith("parent:", StringComparison.Ordinal)) {
                method.ParentId = lines[index].Substring("parent:".Length);
                method.ParentHint = method.ParentId;
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("calls:", StringComparison.Ordinal)) {
                var raw = lines[index].Substring("calls:".Length);
                if (string.IsNullOrEmpty(raw) == false) {
                    var parts = raw.Split(',');
                    for (var i = 0; i < parts.Length; i++) {
                        if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ordinal))
                            method.CallOrdinals.Add(ordinal);
                    }
                }

                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("return:", StringComparison.Ordinal)) {
                method.ReturnedOrdinal = ParseInt(lines[index].Substring("return:".Length));
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("calltypes:", StringComparison.Ordinal)) {
                SplitCallField(method.CallTypeArguments, lines[index].Substring("calltypes:".Length));
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("callas:", StringComparison.Ordinal)) {
                SplitCallField(method.CallAsServices, lines[index].Substring("callas:".Length));
                index++;
            }

            if (index < lines.Length && lines[index].StartsWith("callholes:", StringComparison.Ordinal)) {
                SplitCallField(method.CallHoles, lines[index].Substring("callholes:".Length));
                index++;
            }

            var serviceCursor = 0;
            var implementationCursor = 0;
            while (index < lines.Length) {
                var line = lines[index];
                index++;
                if (string.IsNullOrEmpty(line))
                    continue;

                var fields = GraphEmitter.SplitFields(line);
                if (fields.Length < 11)
                    continue;

                var serviceCount = ParseInt(fields[4]);
                var armCount = ParseInt(fields[5]);
                var implementation = GraphEmitter.Unescape(fields[10]);
                if (string.IsNullOrEmpty(implementation) && implementationCursor < implementations.Count)
                    implementation = implementations[implementationCursor];
                if (implementation == GraphEmitter.VoidSentinel)
                    implementation = "";
                implementationCursor++;

                var registration = new GraphRegistration {
                    Kind = GraphEmitter.Unescape(fields[0]),
                    Lifetime = GraphEmitter.Unescape(fields[1]),
                    Origin = GraphEmitter.Unescape(fields[2]),
                    Ordinal = ParseInt(fields[3]),
                    Hole = GraphEmitter.Unescape(fields[6]),
                    Source = GraphEmitter.Unescape(fields[7]),
                    File = GraphEmitter.Unescape(fields[8]),
                    Line = ParseInt(fields[9]),
                    ImplementationType = implementation,
                    TypeMap = fields.Length > 11 ? GraphEmitter.Unescape(fields[11]) : "",
                };

                if (fields.Length > 12) {
                    var maps = GraphEmitter.Unescape(fields[12]).Split(';');
                    for (var m = 0; m < maps.Length; m++)
                        registration.ServiceMaps.Add(maps[m]);
                }

                for (var s = 0; s < serviceCount; s++) {
                    var service = "";
                    if (serviceCursor < services.Count)
                        service = services[serviceCursor];
                    serviceCursor++;
                    if (service == GraphEmitter.VoidSentinel)
                        service = "";
                    if (string.IsNullOrEmpty(service) == false)
                        registration.ServiceTypes.Add(service);
                }

                for (var a = 0; a < armCount && index < lines.Length; a++) {
                    var armFields = GraphEmitter.SplitFields(lines[index]);
                    index++;
                    var arm = new GraphSwitchArm();
                    if (armFields.Length > 0)
                        arm.Discriminant = GraphEmitter.Unescape(armFields[0]);
                    if (armFields.Length > 1)
                        arm.ImplementationType = GraphEmitter.Unescape(armFields[1]);
                    if (armFields.Length > 2)
                        arm.ParameterExpression = GraphEmitter.Unescape(armFields[2]);
                    registration.Arms.Add(arm);
                }

                method.Registrations.Add(registration);
            }
        }

        private static List<string> AsTypes(TypedConstant constant, Compilation compilation) {
            var list = new List<string>();
            if (constant.Kind != TypedConstantKind.Array)
                return list;

            foreach (var value in constant.Values) {
                if (value.Value is ITypeSymbol type)
                    list.Add(TypeNames.ForCode(type));
                else
                    list.Add("");
            }

            return list;
        }

        private static List<string> AsStrings(TypedConstant constant) {
            var list = new List<string>();
            if (constant.Kind != TypedConstantKind.Array)
                return list;
            foreach (var value in constant.Values)
                list.Add(value.Value as string ?? "");
            return list;
        }

        private static string AsString(TypedConstant constant) {
            return constant.Value as string ?? "";
        }

        private static bool AsBool(TypedConstant constant) {
            return constant.Value is bool value && value;
        }

        private static int AsInt(TypedConstant constant) {
            if (constant.Value is int value)
                return value;
            return 0;
        }

        private static int ParseInt(string value) {
            if (int.TryParse(GraphEmitter.Unescape(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return 0;
        }

        private static void SplitCallField(List<string> target, string raw) {
            if (string.IsNullOrEmpty(raw))
                return;
            var parts = raw.Split('|');
            for (var i = 0; i < parts.Length; i++)
                target.Add(GraphEmitter.Unescape(parts[i]));
        }

    }
}
