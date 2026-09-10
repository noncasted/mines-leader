using System;
using System.Collections.Generic;

namespace ContainerGenerator {
    internal static class EntityAssets {
        public static void Bind(GraphDocument document, EntityAssetIndex index) {
            if (document == null || index == null)
                return;

            var registers = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (TryRegisterType(method.Id, out var type) == false)
                    continue;
                registers[EntityAssetIndex.Normalize(type)] = method.Id;
            }

            for (var i = 0; i < document.Methods.Count; i++) {
                var method = document.Methods[i];
                if (string.IsNullOrEmpty(method.ViewType))
                    continue;

                var asset = index.Find(method.ViewType);
                if (asset == null)
                    continue;

                var ordinal = NextOrdinal(method);
                for (var c = 0; c < asset.ComponentTypes.Count; c++) {
                    var component = EntityAssetIndex.Normalize(asset.ComponentTypes[c]);
                    if (string.IsNullOrEmpty(component))
                        continue;
                    if (registers.TryGetValue(component, out var callId) == false)
                        continue;
                    if (method.Calls.Contains(callId))
                        continue;

                    method.Calls.Add(callId);
                    method.CallOrdinals.Add(ordinal++);
                }
            }
        }

        private static int NextOrdinal(GraphMethod method) {
            var max = -1;
            for (var i = 0; i < method.CallOrdinals.Count; i++) {
                if (method.CallOrdinals[i] > max)
                    max = method.CallOrdinals[i];
            }

            for (var i = 0; i < method.Registrations.Count; i++) {
                if (method.Registrations[i].Ordinal > max)
                    max = method.Registrations[i].Ordinal;
            }

            return max + 1;
        }

        private static bool TryRegisterType(string methodId, out string type) {
            type = "";
            if (string.IsNullOrEmpty(methodId))
                return false;

            const string suffix = ".Register";
            if (methodId.EndsWith(suffix, StringComparison.Ordinal) == false)
                return false;

            type = methodId.Substring(0, methodId.Length - suffix.Length);
            return string.IsNullOrEmpty(type) == false;
        }
    }
}
