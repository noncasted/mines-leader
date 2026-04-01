namespace Tests.Messaging;

[GenerateSerializer]
public class TestMessage {
    [Id(0)] public string Text { get; set; } = string.Empty;
    [Id(1)] public int Sequence { get; set; }
}

[GenerateSerializer]
public class TestRequest {
    [Id(0)] public string Question { get; set; } = string.Empty;
}

[GenerateSerializer]
public class TestResponse {
    [Id(0)] public string Answer { get; set; } = string.Empty;
}

public class TestQueueId : Infrastructure.IDurableQueueId {
    public TestQueueId(string name) { _name = name; }
    private readonly string _name;
    public string ToRaw() => $"test-queue-{_name}";
}

public class TestPipeId : Infrastructure.IRuntimePipeId {
    public TestPipeId(string name) { _name = name; }
    private readonly string _name;
    public string ToRaw() => $"test-pipe-{_name}";
}

public class TestChannelId : Infrastructure.IRuntimeChannelId {
    public TestChannelId(string name) { _name = name; }
    private readonly string _name;
    public string ToRaw() => $"test-channel-{_name}";
}
