# Common

Reactive primitives of both sides: lifetimes, event sources, lifetimed values and viewable properties, viewable lists and
dictionaries, ModifiableList (assembly `Framework.Common`, namespace `Internal`, C# 9 and netstandard2.1, no Unity); `Tests~/`
holds its xunit tests.

`Framework.Common` is referenced directly by everything that uses a lifetime: every asmdef that
references `Internal` (asmdef references are not transitive) and every backend csproj (no transitive project
references). The waits are platform extensions: `ReactiveUniTaskExtensions` in the core (UniTask),
`ReactiveTaskExtensions` in backend `Common` (Task). A listener of a lifetime or a handler of an event source that
throws does not stop the others: the exceptions go to `ReactiveExceptions.Handler` after the pass (the client
logs them as `Exception` of the category `Internal.ReactiveExceptions` in `ReactiveLogging`; the backend has no handler and gets the rethrow). The editor
feeds the Lifetime Graph through `LifetimeTracker` (compiled only under `UNITY_EDITOR`). The sources must stay
C# 9 and netstandard2.1: Unity compiles them as they are, and the dotnet build is pinned to the same.
