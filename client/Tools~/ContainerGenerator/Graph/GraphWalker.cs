using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ContainerGenerator {
    internal sealed class GraphWalker {
        private readonly Compilation _compilation;
        private readonly ReferenceSymbols _references;
        private readonly GraphDocument _document;
        private readonly Dictionary<string, GraphMethod> _methods;

        public GraphWalker(Compilation compilation, ReferenceSymbols references) {
            _compilation = compilation;
            _references = references;
            _document = new GraphDocument { AssemblyName = compilation.AssemblyName ?? "Assembly" };
            _methods = new Dictionary<string, GraphMethod>(StringComparer.Ordinal);
        }

        public GraphDocument Walk() {
            foreach (var tree in _compilation.SyntaxTrees) {
                var model = _compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                foreach (var node in root.DescendantNodes()) {
                    IMethodSymbol? method = null;
                    if (node is MethodDeclarationSyntax methodSyntax)
                        method = model.GetDeclaredSymbol(methodSyntax) as IMethodSymbol;
                    else if (node is LocalFunctionStatementSyntax localSyntax)
                        method = model.GetDeclaredSymbol(localSyntax) as IMethodSymbol;

                    if (method == null)
                        continue;
                    if (SymbolEqualityComparer.Default.Equals(method.ContainingAssembly, _compilation.Assembly) == false)
                        continue;

                    var isRoot = IsRoot(method);
                    var isAssetInstaller = IsEntityOrSceneInstaller(method);
                    if (isRoot == false && isAssetInstaller == false)
                        continue;

                    WalkMethod(method, isRoot || isAssetInstaller);
                }
            }

            foreach (var pair in _methods)
                _document.Methods.Add(pair.Value);

            _document.Methods.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return _document;
        }

        private bool IsRoot(IMethodSymbol method) {
            if (HasGraphRootAttribute(method))
                return true;

            if (method.Name != "Construct" && method.Name != "Build")
                return false;

            return HasBuilderParameter(method);
        }

        private bool HasGraphRootAttribute(IMethodSymbol method) {
            if (_references.GraphRootAttribute == null)
                return false;

            foreach (var attribute in method.GetAttributes()) {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _references.GraphRootAttribute))
                    return true;
            }

            return false;
        }

        private static IMethodSymbol Normalize(IMethodSymbol method) {
            if (method.ReducedFrom != null)
                return method.ReducedFrom;
            return method.OriginalDefinition ?? method;
        }

        private bool IsInstaller(IMethodSymbol method) {
            method = Normalize(method);
            if (method.MethodKind != MethodKind.Ordinary &&
                method.MethodKind != MethodKind.LocalFunction &&
                method.MethodKind != MethodKind.ReducedExtension)
                return false;

            if (HasBuilderParameter(method))
                return true;
            if (method.MethodKind == MethodKind.ReducedExtension && IsBuilderLike(method.ReceiverType))
                return true;

            if (method.Name == "Register" && method.Parameters.Length == 1 && IsEntityBuilder(method.Parameters[0].Type))
                return true;

            if (method.Name == "Create" && method.Parameters.Length == 1 && IsScopeBuilder(method.Parameters[0].Type))
                return true;

            return false;
        }

        private bool IsEntityOrSceneInstaller(IMethodSymbol method) {
            if (method.IsStatic)
                return false;
            if (method.Name == "Register" && method.Parameters.Length == 1 && IsEntityBuilder(method.Parameters[0].Type))
                return method.ContainingType != null && method.ContainingType.Implements(_references.EntityComponent);
            if (method.Name == "Create" && method.Parameters.Length == 1 && IsScopeBuilder(method.Parameters[0].Type))
                return method.ContainingType != null && method.ContainingType.Implements(_references.SceneService);
            return false;
        }

        private bool HasBuilderParameter(IMethodSymbol method) {
            foreach (var parameter in method.Parameters) {
                if (IsBuilderLike(parameter.Type))
                    return true;
            }

            return false;
        }

        private GraphMethod WalkMethod(IMethodSymbol method, bool isRoot) {
            var id = MethodId(method);
            if (_methods.TryGetValue(id, out var existing)) {
                if (isRoot)
                    existing.IsRoot = true;
                return existing;
            }

            var original = method.OriginalDefinition ?? method;
            var result = new GraphMethod {
                Id = id,
                IsRoot = isRoot,
            };
            _methods[id] = result;

            var syntaxSource = method.ReducedFrom ?? original;
            SyntaxNode? syntax = null;
            SemanticModel? model = null;
            foreach (var reference in syntaxSource.DeclaringSyntaxReferences) {
                syntax = reference.GetSyntax();
                model = _compilation.GetSemanticModel(syntax.SyntaxTree);
                break;
            }

            if (syntax == null || model == null) {
                result.File = "";
                result.Line = 0;
                return result;
            }

            var location = syntax.GetLocation().GetLineSpan();
            result.File = syntax.SyntaxTree.FilePath ?? "";
            result.Line = location.StartLinePosition.Line + 1;

            var state = new State(method, model, result);
            foreach (var parameter in method.Parameters) {
                state.Locals[parameter.Name] = new Local {
                    Name = parameter.Name,
                    Type = parameter.Type,
                    IsBuilder = IsBuilderLike(parameter.Type),
                };
                if (IsBuilderLike(parameter.Type))
                    state.TaintedBuilder.Add(parameter.Name);
            }

            for (var i = 0; i < original.TypeParameters.Length && i < method.TypeArguments.Length; i++)
                state.Substitution[original.TypeParameters[i]] = method.TypeArguments[i];

            if (syntax is MethodDeclarationSyntax methodSyntax) {
                if (methodSyntax.Body != null)
                    WalkStatement(state, methodSyntax.Body);
                else if (methodSyntax.ExpressionBody != null)
                    WalkExpression(state, methodSyntax.ExpressionBody.Expression);
            }
            else if (syntax is LocalFunctionStatementSyntax localSyntax) {
                if (localSyntax.Body != null)
                    WalkStatement(state, localSyntax.Body);
                else if (localSyntax.ExpressionBody != null)
                    WalkExpression(state, localSyntax.ExpressionBody.Expression);
            }
            else if (syntax is AccessorDeclarationSyntax)
                Error(state, syntax, "accessor");

            return result;
        }

        private void WalkStatement(State state, StatementSyntax statement) {
            switch (statement.Kind()) {
                case SyntaxKind.Block:
                    foreach (var child in ((BlockSyntax)statement).Statements)
                        WalkStatement(state, child);
                    return;
                case SyntaxKind.LocalDeclarationStatement:
                    WalkLocalDeclaration(state, (LocalDeclarationStatementSyntax)statement);
                    return;
                case SyntaxKind.ExpressionStatement:
                    WalkExpression(state, ((ExpressionStatementSyntax)statement).Expression);
                    return;
                case SyntaxKind.ReturnStatement:
                    if (((ReturnStatementSyntax)statement).Expression != null)
                        WalkExpression(state, ((ReturnStatementSyntax)statement).Expression!);
                    return;
                case SyntaxKind.IfStatement:
                    WalkIf(state, (IfStatementSyntax)statement);
                    return;
                case SyntaxKind.SwitchStatement:
                    WalkSwitchStatement(state, (SwitchStatementSyntax)statement);
                    return;
                case SyntaxKind.UsingStatement:
                    WalkUsing(state, (UsingStatementSyntax)statement);
                    return;
                case SyntaxKind.TryStatement:
                    WalkTry(state, (TryStatementSyntax)statement);
                    return;
                case SyntaxKind.ThrowStatement:
                    if (((ThrowStatementSyntax)statement).Expression != null)
                        WalkExpression(state, ((ThrowStatementSyntax)statement).Expression!, allowUnknown: true);
                    return;
                case SyntaxKind.LocalFunctionStatement:
                    return;
                case SyntaxKind.EmptyStatement:
                case SyntaxKind.BreakStatement:
                case SyntaxKind.ContinueStatement:
                case SyntaxKind.CheckedStatement:
                case SyntaxKind.UncheckedStatement:
                    return;
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                    WalkLoop(state, statement);
                    return;
                default:
                    if (statement is LabeledStatementSyntax labeled) {
                        WalkStatement(state, labeled.Statement);
                        return;
                    }

                    Error(state, statement, statement.Kind().ToString());
                    return;
            }
        }

        private void WalkLoop(State state, StatementSyntax statement) {
            StatementSyntax? body = statement switch {
                ForStatementSyntax forStatement => forStatement.Statement,
                ForEachStatementSyntax forEach => forEach.Statement,
                WhileStatementSyntax whileStatement => whileStatement.Statement,
                DoStatementSyntax doStatement => doStatement.Statement,
                _ => null,
            };
            if (body != null)
                WalkStatement(state, body);
        }

        private void WalkIf(State state, IfStatementSyntax syntax) {
            WalkExpression(state, syntax.Condition, allowUnknown: true);
            state.BranchDepth++;
            WalkStatement(state, syntax.Statement);
            if (syntax.Else != null)
                WalkStatement(state, syntax.Else.Statement);
            state.BranchDepth--;
        }

        private void WalkSwitchStatement(State state, SwitchStatementSyntax syntax) {
            WalkExpression(state, syntax.Expression, allowUnknown: true);
            state.BranchDepth++;
            foreach (var section in syntax.Sections) {
                foreach (var statement in section.Statements)
                    WalkStatement(state, statement);
            }

            state.BranchDepth--;
        }

        private void WalkUsing(State state, UsingStatementSyntax syntax) {
            if (syntax.Declaration != null)
                WalkVariableDeclaration(state, syntax.Declaration);
            if (syntax.Expression != null)
                WalkExpression(state, syntax.Expression, allowUnknown: true);
            WalkStatement(state, syntax.Statement);
        }

        private void WalkTry(State state, TryStatementSyntax syntax) {
            WalkStatement(state, syntax.Block);
            foreach (var catchClause in syntax.Catches)
                WalkStatement(state, catchClause.Block);
            if (syntax.Finally != null)
                WalkStatement(state, syntax.Finally.Block);
        }

        private void WalkLocalDeclaration(State state, LocalDeclarationStatementSyntax syntax) {
            WalkVariableDeclaration(state, syntax.Declaration);
        }

        private void WalkVariableDeclaration(State state, VariableDeclarationSyntax syntax) {
            foreach (var variable in syntax.Variables) {
                var name = variable.Identifier.Text;
                var local = new Local { Name = name };
                if (variable.Initializer != null) {
                    var value = Unwrap(variable.Initializer.Value);
                    if (value is SwitchExpressionSyntax switchExpression && TryReadRegisterSwitch(state, switchExpression, out var registration)) {
                        local.IsSwitch = true;
                        local.Registration = registration;
                        state.Result.Registrations.Add(registration);
                    }
                    else {
                        WalkExpression(state, variable.Initializer.Value);
                        ApplyAssignment(state, local, variable.Initializer.Value);
                    }
                }

                state.Locals[name] = local;
            }
        }

        private void WalkExpression(State state, ExpressionSyntax expression, bool allowUnknown = false) {
            expression = Unwrap(expression);
            switch (expression.Kind()) {
                case SyntaxKind.InvocationExpression:
                    WalkInvocation(state, (InvocationExpressionSyntax)expression);
                    return;
                case SyntaxKind.SimpleAssignmentExpression:
                case SyntaxKind.AddAssignmentExpression:
                    WalkAssignment(state, (AssignmentExpressionSyntax)expression);
                    return;
                case SyntaxKind.SwitchExpression:
                    WalkSwitchExpression(state, (SwitchExpressionSyntax)expression);
                    return;
                case SyntaxKind.ConditionalExpression:
                    var conditional = (ConditionalExpressionSyntax)expression;
                    WalkExpression(state, conditional.Condition, true);
                    state.BranchDepth++;
                    WalkExpression(state, conditional.WhenTrue);
                    WalkExpression(state, conditional.WhenFalse);
                    state.BranchDepth--;
                    return;
                case SyntaxKind.SimpleLambdaExpression:
                case SyntaxKind.ParenthesizedLambdaExpression:
                case SyntaxKind.AnonymousMethodExpression:
                    WalkLambda(state, expression);
                    return;
                case SyntaxKind.AwaitExpression:
                    WalkExpression(state, ((AwaitExpressionSyntax)expression).Expression);
                    return;
                case SyntaxKind.ObjectCreationExpression:
                case SyntaxKind.ImplicitObjectCreationExpression:
                    WalkObjectCreation(state, expression, allowUnknown);
                    return;
                case SyntaxKind.IdentifierName:
                case SyntaxKind.GenericName:
                case SyntaxKind.ThisExpression:
                case SyntaxKind.BaseExpression:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.NullLiteralExpression:
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.DefaultLiteralExpression:
                case SyntaxKind.DefaultExpression:
                case SyntaxKind.TypeOfExpression:
                case SyntaxKind.SizeOfExpression:
                case SyntaxKind.PredefinedType:
                    return;
                case SyntaxKind.SimpleMemberAccessExpression:
                    WalkExpression(state, ((MemberAccessExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.ElementAccessExpression:
                    var access = (ElementAccessExpressionSyntax)expression;
                    WalkExpression(state, access.Expression, true);
                    foreach (var argument in access.ArgumentList.Arguments)
                        WalkExpression(state, argument.Expression, true);
                    return;
                case SyntaxKind.CastExpression:
                    WalkExpression(state, ((CastExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.AsExpression:
                case SyntaxKind.IsExpression:
                    WalkExpression(state, ((BinaryExpressionSyntax)expression).Left, true);
                    return;
                case SyntaxKind.IsPatternExpression:
                    WalkExpression(state, ((IsPatternExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.ConditionalAccessExpression:
                    WalkExpression(state, ((ConditionalAccessExpressionSyntax)expression).Expression, true);
                    return;
                case SyntaxKind.SuppressNullableWarningExpression:
                    WalkExpression(state, ((PostfixUnaryExpressionSyntax)expression).Operand, allowUnknown);
                    return;
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.LogicalNotExpression:
                case SyntaxKind.EqualsExpression:
                case SyntaxKind.NotEqualsExpression:
                case SyntaxKind.AddExpression:
                case SyntaxKind.SubtractExpression:
                case SyntaxKind.MultiplyExpression:
                case SyntaxKind.DivideExpression:
                case SyntaxKind.ModuloExpression:
                case SyntaxKind.BitwiseAndExpression:
                case SyntaxKind.BitwiseOrExpression:
                case SyntaxKind.ExclusiveOrExpression:
                case SyntaxKind.LeftShiftExpression:
                case SyntaxKind.RightShiftExpression:
                case SyntaxKind.LessThanExpression:
                case SyntaxKind.LessThanOrEqualExpression:
                case SyntaxKind.GreaterThanExpression:
                case SyntaxKind.GreaterThanOrEqualExpression:
                case SyntaxKind.CoalesceExpression:
                case SyntaxKind.UnaryMinusExpression:
                case SyntaxKind.UnaryPlusExpression:
                case SyntaxKind.BitwiseNotExpression:
                case SyntaxKind.PreIncrementExpression:
                case SyntaxKind.PreDecrementExpression:
                case SyntaxKind.PostIncrementExpression:
                case SyntaxKind.PostDecrementExpression:
                    foreach (var child in expression.ChildNodes()) {
                        if (child is ExpressionSyntax childExpression)
                            WalkExpression(state, childExpression, true);
                    }

                    return;
                case SyntaxKind.InterpolatedStringExpression:
                case SyntaxKind.TupleExpression:
                case SyntaxKind.AnonymousObjectCreationExpression:
                case SyntaxKind.ImplicitArrayCreationExpression:
                case SyntaxKind.ArrayCreationExpression:
                    foreach (var child in expression.ChildNodes()) {
                        if (child is ExpressionSyntax childExpression)
                            WalkExpression(state, childExpression, true);
                    }

                    return;
                default:
                    if (allowUnknown)
                        return;
                    if (IsGraphRelevant(state, expression) == false)
                        return;
                    Error(state, expression, expression.Kind().ToString());
                    return;
            }
        }

        private void WalkAssignment(State state, AssignmentExpressionSyntax syntax) {
            WalkExpression(state, syntax.Right);
            if (syntax.Left is IdentifierNameSyntax identifier && state.Locals.TryGetValue(identifier.Identifier.Text, out var local))
                ApplyAssignment(state, local, syntax.Right);
            else if (syntax.Left.ToString() == "_") {
                if (Unwrap(syntax.Right) is SwitchExpressionSyntax switchExpression)
                    TryApplyParameterSwitch(state, switchExpression);
            }
        }

        private void WalkLambda(State state, ExpressionSyntax expression) {
            CSharpSyntaxNode? body = expression switch {
                SimpleLambdaExpressionSyntax simple => simple.Body,
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.Body,
                AnonymousMethodExpressionSyntax anonymous => anonymous.Body,
                _ => null,
            };
            if (body is StatementSyntax statement)
                WalkStatement(state, statement);
            else if (body is ExpressionSyntax bodyExpression)
                WalkExpression(state, bodyExpression);
        }

        private void WalkObjectCreation(State state, ExpressionSyntax expression, bool allowUnknown) {
            ArgumentListSyntax? arguments = expression switch {
                ObjectCreationExpressionSyntax creation => creation.ArgumentList,
                ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.ArgumentList,
                _ => null,
            };
            if (arguments == null)
                return;

            foreach (var argument in arguments.Arguments) {
                var value = Unwrap(argument.Expression);
                if (value is SimpleLambdaExpressionSyntax || value is ParenthesizedLambdaExpressionSyntax || value is AnonymousMethodExpressionSyntax)
                    WalkLambda(state, value);
                else
                    WalkExpression(state, argument.Expression, allowUnknown: true);
            }
        }

        private void WalkSwitchExpression(State state, SwitchExpressionSyntax syntax) {
            if (TryReadRegisterSwitch(state, syntax, out var registration)) {
                state.Result.Registrations.Add(registration);
                return;
            }

            if (TryApplyParameterSwitch(state, syntax))
                return;

            WalkExpression(state, syntax.GoverningExpression, true);
            state.BranchDepth++;
            foreach (var arm in syntax.Arms)
                WalkExpression(state, arm.Expression);
            state.BranchDepth--;
        }

        private void WalkInvocation(State state, InvocationExpressionSyntax invocation) {
            var symbol = state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            var name = symbol != null ? symbol.OriginalDefinition.Name : MethodName(invocation);

            foreach (var argument in invocation.ArgumentList.Arguments) {
                var value = Unwrap(argument.Expression);
                if (value is SimpleLambdaExpressionSyntax || value is ParenthesizedLambdaExpressionSyntax || value is AnonymousMethodExpressionSyntax)
                    WalkLambda(state, value);
            }

            if (name == "Register") {
                HandleRegister(state, invocation, symbol);
                return;
            }

            if (name == "RegisterInstance") {
                HandleRegisterInstance(state, invocation, symbol);
                return;
            }

            if (name == "RegisterComponent") {
                HandleRegisterComponent(state, invocation, symbol);
                return;
            }

            if (name == "As") {
                HandleAs(state, invocation, symbol);
                return;
            }

            if (name == "AsSelf") {
                HandleCurrent(state, invocation, registration => {
                    if (string.IsNullOrEmpty(registration.ImplementationType) == false)
                        AddService(registration, registration.ImplementationType);
                });
                return;
            }

            if (name == "AsSelfResolvable") {
                WalkReceiver(state, invocation);
                return;
            }

            if (name == "WithParameter") {
                HandleWithParameter(state, invocation, symbol);
                return;
            }

            if (name == "WithScopeLifetime") {
                HandleCurrent(state, invocation, registration => {
                    registration.Hole = CombineHole(registration.Hole, "builder.Lifetime");
                    if (registration.Origin == "ConstructedType")
                        registration.Origin = "ParameterHole";
                });
                return;
            }

            if (name == "Inject") {
                HandleInject(state, invocation);
                return;
            }

            if (name == "Instantiate") {
                HandleInstantiate(state, invocation);
                return;
            }

            if (name == "FindOrLoadSceneWithServices") {
                HandleSceneServices(state, invocation);
                return;
            }

            if (name == "AddSnapshotHandler" || name == "AddCardActionSyncResolver") {
                HandleKnownGenericInstaller(state, invocation, symbol);
                return;
            }

            if (IsBenign(name)) {
                WalkReceiver(state, invocation);
                foreach (var argument in invocation.ArgumentList.Arguments)
                    WalkExpression(state, argument.Expression, allowUnknown: true);
                return;
            }

            if (symbol == null && IsAddInstaller(name, state, invocation)) {
                WalkReceiver(state, invocation);
                if (state.Result.Calls.Contains(name) == false)
                    state.Result.Calls.Add(name);
                return;
            }

            if (symbol != null && (IsInstaller(symbol) || IsAddInstaller(name, state, invocation))) {
                WalkReceiver(state, invocation);
                var constructed = Constructed(Normalize(symbol), invocation, state);
                var callee = WalkMethod(constructed, false);
                if (state.Result.Calls.Contains(callee.Id) == false)
                    state.Result.Calls.Add(callee.Id);
                state.LastRegistration = null;
                return;
            }

            if (symbol != null && HasSyntax(symbol) == false && ReceiverIsGraphRelevant(state, invocation)) {
                var id = MethodId(symbol);
                if (state.Result.Calls.Contains(id) == false)
                    state.Result.Calls.Add(id);
                return;
            }

            if (ReceiverIsGraphRelevant(state, invocation) == false) {
                WalkReceiver(state, invocation);
                return;
            }

            if (symbol == null) {
                Error(state, invocation, "unresolved invocation '" + invocation.ToString() + "'", GraphDescriptors.UnresolvedInstaller);
                return;
            }

            Error(state, invocation, "invocation '" + name + "'");
        }

        private bool IsAddInstaller(string name, State state, InvocationExpressionSyntax invocation) {
            if (name.StartsWith("Add", StringComparison.Ordinal) == false)
                return false;
            return ReceiverIsGraphRelevant(state, invocation);
        }

        private void HandleRegister(State state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            WalkReceiver(state, invocation);
            var implementation = "";
            var service = "";
            if (symbol != null && symbol.TypeArguments.Length == 1)
                implementation = Format(state, symbol.TypeArguments[0]);
            else if (symbol != null && symbol.TypeArguments.Length >= 2) {
                service = Format(state, symbol.TypeArguments[0]);
                implementation = Format(state, symbol.TypeArguments[1]);
            }

            var registration = NewRegistration(state, invocation, "Register", implementation, "ConstructedType");
            registration.Lifetime = ParseLifetime(invocation, 0);
            if (string.IsNullOrEmpty(service) == false)
                AddService(registration, service);

            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
        }

        private void HandleRegisterInstance(State state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var implementation = symbol != null && symbol.TypeArguments.Length > 0
                ? Format(state, symbol.TypeArguments[0])
                : argument != null ? Format(state, state.Model.GetTypeInfo(argument).Type) : "";
            var origin = ClassifyValue(state, argument);
            var registration = NewRegistration(state, invocation, "RegisterInstance", implementation, origin);
            registration.Hole = argument != null ? argument.ToString() : "";
            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
        }

        private void HandleRegisterComponent(State state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var implementation = symbol != null && symbol.TypeArguments.Length > 0
                ? Format(state, symbol.TypeArguments[0])
                : argument != null ? Format(state, state.Model.GetTypeInfo(argument).Type) : "";
            var origin = ClassifyValue(state, argument);
            if (origin == "InstanceHole" && argument != null && LooksLikePrefabAsset(argument))
                origin = "PrefabAsset";
            var registration = NewRegistration(state, invocation, "RegisterComponent", implementation, origin);
            registration.Lifetime = ParseLifetime(invocation, 1);
            registration.Hole = argument != null ? argument.ToString() : "";
            if (string.IsNullOrEmpty(implementation) == false)
                AddService(registration, implementation);
            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
        }

        private void HandleAs(State state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            WalkReceiver(state, invocation);
            var service = "";
            if (symbol != null && symbol.TypeArguments.Length > 0)
                service = Format(state, symbol.TypeArguments[0]);
            else if (invocation.ArgumentList.Arguments.Count > 0) {
                var argument = invocation.ArgumentList.Arguments[0].Expression;
                if (argument is TypeOfExpressionSyntax typeOf)
                    service = Format(state, state.Model.GetTypeInfo(typeOf.Type).Type);
            }

            var target = CurrentRegistration(state, invocation);
            if (target != null && string.IsNullOrEmpty(service) == false)
                AddService(target, service);
        }

        private void HandleWithParameter(State state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var hole = argument != null ? argument.ToString() : "";
            var target = CurrentRegistration(state, invocation);
            if (target == null)
                return;

            target.Hole = CombineHole(target.Hole, hole);
            if (target.Arms.Count == 0 && target.Origin == "ConstructedType")
                target.Origin = "ParameterHole";
        }

        private void HandleInject(State state, InvocationExpressionSyntax invocation) {
            WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0 ? invocation.ArgumentList.Arguments[0].Expression : null;
            var implementation = argument != null ? Format(state, state.Model.GetTypeInfo(argument).Type) : "";
            var registration = NewRegistration(state, invocation, "Inject", implementation, "InjectExisting");
            registration.Hole = argument != null ? argument.ToString() : "";
            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
        }

        private void HandleInstantiate(State state, InvocationExpressionSyntax invocation) {
            WalkReceiver(state, invocation);
            foreach (var argument in invocation.ArgumentList.Arguments)
                WalkExpression(state, argument.Expression, true);
            state.LastInstantiate = invocation;
        }

        private void HandleSceneServices(State state, InvocationExpressionSyntax invocation) {
            WalkReceiver(state, invocation);
            var argument = invocation.ArgumentList.Arguments.Count > 0
                ? invocation.ArgumentList.Arguments[0].Expression.ToString()
                : "";
            var registration = NewRegistration(state, invocation, "SceneServices", "", "SceneServices");
            registration.Hole = argument;
            state.Result.Registrations.Add(registration);
        }

        private void HandleKnownGenericInstaller(State state, InvocationExpressionSyntax invocation, IMethodSymbol? symbol) {
            WalkReceiver(state, invocation);
            if (symbol != null && HasSyntax(symbol.OriginalDefinition)) {
                var constructed = Constructed(symbol, invocation, state);
                var callee = WalkMethod(constructed, false);
                if (state.Result.Calls.Contains(callee.Id) == false)
                    state.Result.Calls.Add(callee.Id);
                return;
            }

            if (symbol == null || symbol.TypeArguments.Length == 0)
                return;

            var implementation = Format(state, symbol.TypeArguments[0]);
            var registration = NewRegistration(state, invocation, "Register", implementation, "ConstructedType");
            if (symbol.TypeArguments.Length > 1)
                AddService(registration, "ISnapshotHandler<" + Format(state, symbol.TypeArguments[1]) + ">");
            state.Result.Registrations.Add(registration);
            state.LastRegistration = registration;
        }

        private void HandleCurrent(State state, InvocationExpressionSyntax invocation, Action<GraphRegistration> apply) {
            WalkReceiver(state, invocation);
            var target = CurrentRegistration(state, invocation);
            if (target != null)
                apply(target);
        }

        private GraphRegistration? CurrentRegistration(State state, InvocationExpressionSyntax invocation) {
            if (state.LastRegistration != null)
                return state.LastRegistration;

            var receiver = GetReceiver(invocation);
            if (receiver is IdentifierNameSyntax identifier && state.Locals.TryGetValue(identifier.Identifier.Text, out var local))
                return local.Registration;

            return null;
        }

        private bool TryReadRegisterSwitch(State state, SwitchExpressionSyntax syntax, out GraphRegistration registration) {
            registration = NewRegistration(state, syntax, "SwitchFactory", "", "SwitchFactory");
            var any = false;
            foreach (var arm in syntax.Arms) {
                var expression = Unwrap(arm.Expression);
                if (expression is ThrowExpressionSyntax)
                    continue;
                if (expression is not InvocationExpressionSyntax invocation)
                    return false;

                var symbol = state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol == null || symbol.OriginalDefinition.Name != "Register" || symbol.TypeArguments.Length == 0)
                    return false;

                var implementation = Format(state, symbol.TypeArguments[symbol.TypeArguments.Length == 1 ? 0 : 1]);
                registration.Arms.Add(new GraphSwitchArm {
                    Discriminant = arm.Pattern.ToString(),
                    ImplementationType = implementation,
                });
                any = true;
            }

            if (any == false)
                return false;

            state.LastRegistration = registration;
            return true;
        }

        private bool TryApplyParameterSwitch(State state, SwitchExpressionSyntax syntax) {
            var applied = false;
            foreach (var arm in syntax.Arms) {
                var expression = Unwrap(arm.Expression);
                if (expression is ThrowExpressionSyntax)
                    continue;
                if (expression is not InvocationExpressionSyntax invocation)
                    return false;

                var symbol = state.Model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol == null || symbol.OriginalDefinition.Name != "WithParameter")
                    return false;

                var target = CurrentRegistration(state, invocation);
                if (target == null || target.Origin != "SwitchFactory" && target.Arms.Count == 0)
                    return false;

                var argument = invocation.ArgumentList.Arguments.Count > 0
                    ? invocation.ArgumentList.Arguments[0].Expression.ToString()
                    : "";
                var discriminant = arm.Pattern.ToString();
                foreach (var existing in target.Arms) {
                    if (existing.Discriminant == discriminant)
                        existing.ParameterExpression = argument;
                }

                applied = true;
            }

            return applied;
        }

        private void ApplyAssignment(State state, Local local, ExpressionSyntax value) {
            value = Unwrap(value);
            local.Type = state.Model.GetTypeInfo(value).Type;
            if (value is InvocationExpressionSyntax invocation) {
                var name = MethodName(invocation);
                if (name == "Instantiate") {
                    local.FromInstantiate = true;
                    local.IsBuilder = false;
                    return;
                }

                if (name == "Register" || name == "RegisterInstance" || name == "RegisterComponent" || name == "As" || name == "WithParameter")
                    local.Registration = state.LastRegistration;
                if (IsBuilderLike(local.Type))
                    local.IsBuilder = true;
            }

            if (local.IsBuilder)
                state.TaintedBuilder.Add(local.Name);
        }

        private string ClassifyValue(State state, ExpressionSyntax? expression) {
            if (expression == null)
                return "InstanceHole";

            expression = Unwrap(expression);
            if (expression is IdentifierNameSyntax identifier && state.Locals.TryGetValue(identifier.Identifier.Text, out var local)) {
                if (local.FromInstantiate)
                    return "PrefabInstance";
            }

            if (LooksLikePrefabAsset(expression))
                return "PrefabAsset";

            return "InstanceHole";
        }

        private static bool LooksLikePrefabAsset(ExpressionSyntax expression) {
            var text = expression.ToString();
            return text.IndexOf("Prefabs.", StringComparison.Ordinal) >= 0 ||
                   text.IndexOf("Prefab", StringComparison.Ordinal) >= 0 && text.IndexOf("Instantiate", StringComparison.Ordinal) < 0;
        }

        private GraphRegistration NewRegistration(
            State state,
            SyntaxNode node,
            string kind,
            string implementation,
            string origin) {
            var span = node.GetLocation().GetLineSpan();
            if (state.BranchDepth > 0 && origin == "ConstructedType")
                origin = "Alternative";

            return new GraphRegistration {
                Kind = kind,
                ImplementationType = implementation,
                Lifetime = "Singleton",
                Origin = origin,
                Source = Trim(node.ToString()),
                File = node.SyntaxTree.FilePath ?? "",
                Line = span.StartLinePosition.Line + 1,
            };
        }

        private void WalkReceiver(State state, InvocationExpressionSyntax invocation) {
            var receiver = GetReceiver(invocation);
            if (receiver != null)
                WalkExpression(state, receiver, true);
        }

        private static ExpressionSyntax? GetReceiver(InvocationExpressionSyntax invocation) {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
                return member.Expression;
            if (invocation.Expression is MemberBindingExpressionSyntax &&
                invocation.Parent is ConditionalAccessExpressionSyntax conditional)
                return conditional.Expression;
            return null;
        }

        private static string MethodName(InvocationExpressionSyntax invocation) {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
                return member.Name.Identifier.ValueText;
            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;
            if (invocation.Expression is GenericNameSyntax generic)
                return generic.Identifier.ValueText;
            if (invocation.Expression is MemberBindingExpressionSyntax binding)
                return binding.Name.Identifier.ValueText;
            return invocation.Expression.ToString();
        }

        private IMethodSymbol Constructed(IMethodSymbol symbol, InvocationExpressionSyntax invocation, State state) {
            return symbol;
        }

        private bool HasSyntax(IMethodSymbol method) {
            return method.OriginalDefinition.DeclaringSyntaxReferences.Length > 0;
        }

        private bool ReceiverIsGraphRelevant(State state, InvocationExpressionSyntax invocation) {
            var receiver = GetReceiver(invocation);
            if (receiver == null)
                return false;
            return IsGraphRelevant(state, receiver);
        }

        private bool IsGraphRelevant(State state, ExpressionSyntax expression) {
            expression = Unwrap(expression);
            if (expression is IdentifierNameSyntax identifier) {
                if (state.TaintedBuilder.Contains(identifier.Identifier.Text))
                    return true;
                if (state.Locals.TryGetValue(identifier.Identifier.Text, out var local) && (local.IsBuilder || local.Registration != null))
                    return true;
            }

            var type = state.Model.GetTypeInfo(expression).Type;
            return IsBuilderLike(type) || IsRegistrationLike(type);
        }

        private bool IsBuilderLike(ITypeSymbol? type) {
            if (type == null)
                return false;
            if (IsScopeBuilder(type) || IsEntityBuilder(type))
                return true;
            if (type.Name == "IBuilder")
                return true;
            return type.Implements(_references.Builder) || type.Implements(_references.ScopeBuilder) || type.Implements(_references.EntityBuilder);
        }

        private bool IsRegistrationLike(ITypeSymbol? type) {
            if (type == null)
                return false;
            if (type.Name == "IRegistration" || type.Name == "IServiceRegistration")
                return true;
            return type.Implements(_references.Registration) || type.Implements(_references.ServiceRegistration);
        }

        private bool IsScopeBuilder(ITypeSymbol type) {
            return type.Name == "IScopeBuilder" || type.Implements(_references.ScopeBuilder);
        }

        private bool IsEntityBuilder(ITypeSymbol type) {
            return type.Name == "IEntityBuilder" || type.Implements(_references.EntityBuilder);
        }

        private string Format(State state, ITypeSymbol? type) {
            if (type == null)
                return "";
            return TypeNames.ForCode(Substitute(state, type));
        }

        private static ITypeSymbol Substitute(State state, ITypeSymbol type) {
            if (type is ITypeParameterSymbol parameter && state.Substitution.TryGetValue(parameter, out var mapped))
                return mapped;
            return type;
        }

        private static string ParseLifetime(InvocationExpressionSyntax invocation, int argumentIndex) {
            if (invocation.ArgumentList.Arguments.Count <= argumentIndex)
                return "Singleton";

            var text = invocation.ArgumentList.Arguments[argumentIndex].Expression.ToString();
            if (text.IndexOf("Transient", StringComparison.Ordinal) >= 0)
                return "Transient";
            if (text.IndexOf("Scoped", StringComparison.Ordinal) >= 0)
                return "Scoped";
            return "Singleton";
        }

        private static void AddService(GraphRegistration registration, string service) {
            if (string.IsNullOrEmpty(service))
                return;
            if (registration.ServiceTypes.Contains(service) == false)
                registration.ServiceTypes.Add(service);
        }

        private static string CombineHole(string existing, string next) {
            if (string.IsNullOrEmpty(existing))
                return next;
            if (string.IsNullOrEmpty(next))
                return existing;
            return existing + "; " + next;
        }

        private static bool IsBenign(string name) {
            switch (name) {
                case "Measure":
                case "Scope":
                case "Start":
                case "Stop":
                case "Dispose":
                case "LoadPrefabGroup":
                case "RequestPrefabGroup":
                case "RequestSpriteGroup":
                case "LoadSpriteGroup":
                case "LoadAssetGroup":
                case "RequestAssetGroup":
                case "FindOrLoadScene":
                case "FindOrLoadSceneWithServices":
                case "WhenAll":
                case "GetComponent":
                case "SetActive":
                case "EnsureLoaded":
                case "NoAwait":
                case "Advise":
                case "KeepAlive":
                case "MoveToModules":
                case "GetType":
                case "ToString":
                case "Equals":
                case "GetHashCode":
                    return true;
                default:
                    return false;
            }
        }

        private static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
            while (true) {
                if (expression is ParenthesizedExpressionSyntax parenthesized)
                    expression = parenthesized.Expression;
                else if (expression is PostfixUnaryExpressionSyntax postfix && postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                    expression = postfix.Operand;
                else
                    return expression;
            }
        }

        private static string MethodId(IMethodSymbol method) {
            var local = method;
            if (method.MethodKind == MethodKind.LocalFunction && method.ContainingSymbol is IMethodSymbol parent)
                return MethodId(parent) + "+" + method.Name;

            var definition = method.ReducedFrom ?? method.OriginalDefinition ?? method;
            var type = definition.ContainingType != null ? TypeNames.ForMetadata(definition.ContainingType) : "";
            var name = string.IsNullOrEmpty(type) ? definition.Name : type + "." + definition.Name;
            if (local.TypeArguments.Length == 0)
                return name;

            var arguments = new string[local.TypeArguments.Length];
            for (var i = 0; i < local.TypeArguments.Length; i++)
                arguments[i] = TypeNames.ForMetadata(local.TypeArguments[i]);
            return name + "<" + string.Join(", ", arguments) + ">";
        }

        private static string Trim(string value) {
            var compact = value.Replace("\r", " ").Replace("\n", " ");
            while (compact.IndexOf("  ", StringComparison.Ordinal) >= 0)
                compact = compact.Replace("  ", " ");
            if (compact.Length > 240)
                compact = compact.Substring(0, 240) + "...";
            return compact.Trim();
        }

        private void Error(State state, SyntaxNode node, string what, DiagnosticDescriptor? descriptor = null) {
            var span = node.GetLocation().GetLineSpan();
            var file = node.SyntaxTree.FilePath ?? "";
            var line = (span.StartLinePosition.Line + 1).ToString();
            _document.Diagnostics.Add(new DiagnosticInfo(
                descriptor ?? GraphDescriptors.UncoveredSyntax,
                LocationInfo.CreateFrom(node.GetLocation()),
                what,
                state.Result.Id,
                file,
                line));
        }

        private sealed class State {
            public IMethodSymbol Method;
            public SemanticModel Model;
            public GraphMethod Result;
            public Dictionary<string, Local> Locals = new Dictionary<string, Local>(StringComparer.Ordinal);
            public HashSet<string> TaintedBuilder = new HashSet<string>(StringComparer.Ordinal);
            public Dictionary<ITypeParameterSymbol, ITypeSymbol> Substitution = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
            public GraphRegistration? LastRegistration;
            public InvocationExpressionSyntax? LastInstantiate;
            public int BranchDepth;

            public State(IMethodSymbol method, SemanticModel model, GraphMethod result) {
                Method = method;
                Model = model;
                Result = result;
            }
        }

        private sealed class Local {
            public string Name = "";
            public ITypeSymbol? Type;
            public bool IsBuilder;
            public bool FromInstantiate;
            public bool IsSwitch;
            public GraphRegistration? Registration;
        }
    }
}
