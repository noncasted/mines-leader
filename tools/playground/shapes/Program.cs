using shapes;

var ids = new List<string>()
{
    "68b3fc8d-abe0-4272-a23f-4f73cfc92cdc",
    "af1b4fe0-e288-40ee-a68e-c04a9733bd17",
    "17020e96-3af7-4642-bf2e-05f31b02b926",
    "035cb69d-3ab8-4cce-89f8-3dc31a843231",
    "7a1679fa-0220-4217-8847-8f34d4e2cc30",
    "30129084-59b5-4267-8c1d-eb7109d2670e",
    "5efae20c-9190-4548-ab9f-c9c252c4084d",
    "5062e858-813d-4880-ac0b-00bf5fb730bd",
    "1f0a7c2a-1418-4b09-8ccb-967c7757e848",
    "7b325774-b755-46de-b36d-8ebf1434bdd9",
    "5807eadc-1e18-422e-81ba-973219f1440c",
    "ba3b6eaa-5fd8-4710-9500-14998bcdd122",
    "0efe27a6-c102-4df4-ae3a-cf1c8b5569ab",
    "cd9182ff-15a1-45dd-b948-4a1f2e37438d",
    "fe85dd6a-b201-4b07-90fe-c464eb80baf9",
    "c1d491c8-077c-44ad-a888-68b3ab9922ac",
    "77a64d14-364f-49b6-8794-fe7dd455d061",
    "c382757a-6cc8-434d-8e6b-74cac3dbec3e",
};

foreach (var id in ids)
{
    var guid = Guid.Parse(id);
    var (a, b) = GuidToTwoLongs(guid);

    var result = $"""
                  {guid}
                  DELETE FROM orleansstorage
                  WHERE graintypestring = 'GenericDelayState' 
                    AND grainidn0 = {a} 
                    AND grainidn1 = {b}
                    AND graintypehash = 1228494399;
                  """;

    Console.WriteLine("");
    Console.WriteLine("");
    Console.WriteLine("");
    Console.WriteLine(result);
}


return 0;

static (long, long) GuidToTwoLongs(Guid guid)
{
    byte[] bytes = guid.ToByteArray();

    long long1 = BitConverter.ToInt64(bytes, 0);
    long long2 = BitConverter.ToInt64(bytes, 8);

    return (long1, long2);
}

var r4 = PatternShapes.Rhombus(4);
var r2 = PatternShapes.Rhombus(2);
var r3 = PatternShapes.Rhombus(3);
var r1 = PatternShapes.Rhombus(1);
var r5 = PatternShapes.Rhombus(5);
var r6 = PatternShapes.Rhombus(6);

r1.Print();
r2.Print();
r3.Print();
r4.Print();
r5.Print();
r6.Print();