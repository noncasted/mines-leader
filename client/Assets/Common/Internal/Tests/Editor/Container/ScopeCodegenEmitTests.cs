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
        private const string RuntimeMethod = "LoadRuntime";

        [Test]
        public void GeneratedScope_DoesNotMixFieldsWithIResolvePlan()
        {
            var type = RequireGenerated(GeneratedMethod);

            Assert.IsFalse(typeof(IResolvePlan).IsAssignableFrom(type));
            Assert.IsFalse(typeof(IInjector).IsAssignableFrom(type));
            AssertNoResolvePlan(type);
        }

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
        public void ContainerRuntimeScope_DisablesGenerationPointwise()
        {
            var generated = RequireGenerated(GeneratedMethod);
            var runtime = FindGenerated(RuntimeMethod);

            Assert.IsNull(
                runtime,
                "[ContainerRuntimeScope] on LoadRuntime must not emit " +
                ExpectedName(RuntimeMethod) +
                ".");
            Assert.IsNull(FieldOfType(generated, typeof(ScopeCodegenRuntimeOnly)));
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
        public void RuntimePlan_IsGenerated_IsFalse()
        {
            var runtime = new ContainerBuilder().Build();
            Assert.IsFalse(runtime.Diagnostics.IsGenerated);
        }

        [Test]
        public void GeneratedScope_IsGenerated_IsTrue()
        {
            var generated = CreateGenerated();
            Assert.IsTrue(generated.Diagnostics.IsGenerated);
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
                if (constructors[i].GetParameters().Length == 0)
                    chosen = constructors[i];
            }

            Assert.IsNotNull(
                chosen,
                "Hole-free fixture must emit a parameterless constructor. Have: " + Describe(constructors));

            ScopeCodegenSingleton.Instances = 0;
            ScopeCodegenScoped.Instances = 0;
            ScopeCodegenTransient.Instances = 0;
            return (IContainer)chosen.Invoke(null);
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

        private static void AssertNoResolvePlan(Type type)
        {
            var members = type.GetMembers(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member is FieldInfo field)
                    AssertNotResolvePlan(field.FieldType, type, field.Name);
                else if (member is PropertyInfo property)
                    AssertNotResolvePlan(property.PropertyType, type, property.Name);
                else if (member is MethodInfo method)
                    AssertMethodHasNoResolvePlan(type, method);
            }
        }

        private static void AssertMethodHasNoResolvePlan(Type owner, MethodInfo method)
        {
            if (method.IsSpecialName == false)
                AssertNotResolvePlan(method.ReturnType, owner, method.Name);

            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
                AssertNotResolvePlan(parameters[i].ParameterType, owner, method.Name + "." + parameters[i].Name);

            var body = method.GetMethodBody();
            if (body == null)
                return;

            var locals = body.LocalVariables;
            for (var i = 0; i < locals.Count; i++)
                AssertNotResolvePlan(locals[i].LocalType, owner, method.Name + ".local");
        }

        private static void AssertNotResolvePlan(Type candidate, Type owner, string member)
        {
            Assert.IsFalse(
                MentionsResolvePlan(candidate),
                owner.Name + "." + member + " references IResolvePlan; generated scope must be all fields or nothing.");
        }

        private static bool MentionsResolvePlan(Type type)
        {
            if (type == null)
                return false;
            if (type == typeof(IResolvePlan) || typeof(IResolvePlan).IsAssignableFrom(type))
                return true;
            if (type.IsByRef)
                return MentionsResolvePlan(type.GetElementType());
            if (type.IsArray)
                return MentionsResolvePlan(type.GetElementType());
            if (type.IsGenericType == false)
                return false;

            var arguments = type.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (MentionsResolvePlan(arguments[i]))
                    return true;
            }

            return false;
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
