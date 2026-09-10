using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Internal.Tests
{
    [Category("Container")]
    public class ScopeCodegenEmitTests
    {
        private const string GeneratedMethod = "LoadGenerated";

        [Test]
        public void MarkerArray_PreservesRegistrationOrder()
        {
            var container = CreateGenerated();

            var setups = container.ResolveAll<IScopeSetup>();

            Assert.IsNotNull(setups);
            Assert.AreEqual(3, setups.Count);
            Assert.IsInstanceOf<ScopeCodegenFirstSetup>(setups[0]);
            Assert.IsInstanceOf<ScopeCodegenSecondSetup>(setups[1]);
            Assert.IsInstanceOf<ScopeCodegenThirdSetup>(setups[2]);
        }

        [Test]
        public void Collection_IReadOnlyList_PreservesRegistrationOrder()
        {
            var container = CreateGenerated();

            var row = container.Resolve<ScopeCodegenRow>();
            var tiers = container.ResolveAll<IScopeCodegenTier>();

            Assert.IsNotNull(row);
            Assert.IsNotNull(row.Tiers);
            Assert.AreEqual(2, row.Tiers.Count);
            Assert.IsInstanceOf<ScopeCodegenBronze>(row.Tiers[0]);
            Assert.IsInstanceOf<ScopeCodegenGold>(row.Tiers[1]);
            Assert.AreEqual(2, tiers.Count);
            Assert.IsInstanceOf<ScopeCodegenBronze>(tiers[0]);
            Assert.IsInstanceOf<ScopeCodegenGold>(tiers[1]);
        }

        [Test]
        public void Transient_IsMethodNotField()
        {
            var type = RequireGenerated(GeneratedMethod);

            Assert.IsNull(
                FieldOfType(type, typeof(ScopeCodegenTransient)),
                "Transient must not be stored as a field.");
            Assert.IsNotNull(
                CreateMethod(type, typeof(ScopeCodegenTransient)),
                "Transient must be a private Create{Type}() method.");
        }

        [Test]
        public void SingletonAndScoped_AreFields()
        {
            var type = RequireGenerated(GeneratedMethod);

            Assert.IsNotNull(
                FieldOfType(type, typeof(ScopeCodegenSingleton)),
                "Singleton must be a field.");
            Assert.IsNotNull(
                FieldOfType(type, typeof(ScopeCodegenScoped)),
                "Scoped must be a field.");
            Assert.IsNull(CreateMethod(type, typeof(ScopeCodegenSingleton)));
            Assert.IsNull(CreateMethod(type, typeof(ScopeCodegenScoped)));
        }

        [Test]
        public void GeneratedScope_Resolve_DoesNotConstructNewSingletons()
        {
            ScopeCodegenSingleton.Instances = 0;
            ScopeCodegenScoped.Instances = 0;
            ScopeCodegenTransient.Instances = 0;

            var container = CreateGenerated();

            Assert.AreEqual(1, ScopeCodegenSingleton.Instances);
            Assert.AreEqual(1, ScopeCodegenScoped.Instances);
            Assert.AreEqual(0, ScopeCodegenTransient.Instances);

            var first = container.Resolve<ScopeCodegenSingleton>();
            var second = container.Resolve<ScopeCodegenSingleton>();
            var scoped = container.Resolve<ScopeCodegenScoped>();

            Assert.AreSame(first, second);
            Assert.AreSame(first, container.Resolve(typeof(ScopeCodegenSingleton)));
            Assert.IsNotNull(scoped);
            Assert.AreEqual(1, ScopeCodegenSingleton.Instances);
            Assert.AreEqual(1, ScopeCodegenScoped.Instances);
        }

        [Test]
        public void GeneratedScope_IsSealedIContainer_InRootNamespace()
        {
            var type = RequireGenerated(GeneratedMethod);

            Assert.IsTrue(type.IsSealed);
            Assert.IsTrue(typeof(IContainer).IsAssignableFrom(type));
            Assert.AreEqual(typeof(ScopeCodegenRoots).Namespace, type.Namespace);
        }

        private static IContainer CreateGenerated()
        {
            var type = RequireGenerated(GeneratedMethod);
            Assert.IsTrue(typeof(IContainer).IsAssignableFrom(type), type.FullName + " must implement IContainer");

            var constructors = type.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ConstructorInfo chosen = null;
            for (var i = 0; i < constructors.Length; i++)
            {
                if (IsLoaderOnlyConstructor(constructors[i].GetParameters()))
                    chosen = constructors[i];
            }

            // Без дырок в графе конструктор получает только то, что даёт загрузчик скоупа.
            Assert.IsNotNull(
                chosen,
                "Hole-free fixture must emit (ILifetime, IContainer, IEventLoop) constructor. Have: " +
                Describe(constructors));

            ScopeCodegenSingleton.Instances = 0;
            ScopeCodegenScoped.Instances = 0;
            ScopeCodegenTransient.Instances = 0;
            return (IContainer)chosen.Invoke(new object[] { new Lifetime(), null, new EventLoop() });
        }

        private static bool IsLoaderOnlyConstructor(ParameterInfo[] parameters)
        {
            return parameters.Length == 3 &&
                   parameters[0].ParameterType == typeof(ILifetime) &&
                   parameters[1].ParameterType == typeof(IContainer) &&
                   parameters[2].ParameterType == typeof(IEventLoop);
        }

        private static Type RequireGenerated(string methodName)
        {
            var type = FindGenerated(methodName);
            Assert.IsNotNull(
                type,
                "G has not emitted " + ExpectedName(methodName) +
                ". Keep this test; do not weaken it and do not change production to match.");
            return type;
        }

        private static Type FindGenerated(string methodName)
        {
            var expected = ExpectedName(methodName);
            var assembly = typeof(ScopeCodegenRoots).Assembly;
            var direct = assembly.GetType(typeof(ScopeCodegenRoots).Namespace + "." + expected);
            if (direct != null)
                return direct;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var candidate = types[i];
                if (candidate != null && candidate.Name == expected)
                    return candidate;
            }

            return null;
        }

        private static string ExpectedName(string methodName)
        {
            return nameof(ScopeCodegenRoots) + methodName + "Container";
        }

        private static FieldInfo FieldOfType(Type container, Type serviceType)
        {
            var fields = container.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == serviceType)
                    return fields[i];
            }

            return null;
        }

        private static MethodInfo CreateMethod(Type container, Type serviceType)
        {
            var name = "Create" + serviceType.Name;
            var methods = container.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != name)
                    continue;
                if (method.ReturnType != serviceType)
                    continue;
                if (method.GetParameters().Length != 0)
                    continue;
                return method;
            }

            return null;
        }

        private static string Describe(IReadOnlyList<ConstructorInfo> constructors)
        {
            if (constructors.Count == 0)
                return "(none)";

            var parts = new string[constructors.Count];
            for (var i = 0; i < constructors.Count; i++)
            {
                var parameters = constructors[i].GetParameters();
                var types = new string[parameters.Length];
                for (var p = 0; p < parameters.Length; p++)
                    types[p] = parameters[p].ParameterType.FullName;
                parts[i] = "(" + string.Join(", ", types) + ")";
            }

            return string.Join("; ", parts);
        }
    }
}
