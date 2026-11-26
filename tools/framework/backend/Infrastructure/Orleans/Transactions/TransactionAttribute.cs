namespace Infrastructure;

[InvokableBaseType(typeof(GrainReference), typeof(ValueTask), typeof(TransactionRequest))]
[InvokableBaseType(typeof(GrainReference), typeof(ValueTask<>), typeof(TransactionRequest<>))]
[InvokableBaseType(typeof(GrainReference), typeof(Task), typeof(TransactionTaskRequest))]
[InvokableBaseType(typeof(GrainReference), typeof(Task<>), typeof(TransactionTaskRequest<>))]
[AttributeUsage(AttributeTargets.Method)]
public class TransactionAttribute : Attribute
{
}
