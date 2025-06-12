namespace Infrastructure.Messaging;

public static class MessagingOptions
{
    public static readonly TimeSpan ObserverTimeout = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ResubscriptionTime = TimeSpan.FromSeconds(30);
}