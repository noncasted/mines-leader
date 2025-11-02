using shapes;

var id = Guid.Parse("34f8834d4a064f4aac0ee325c0182e26");

var raw = id.ToByteArray();

var id0 = BitConverter.ToInt64(raw, 0);
var id1 = BitConverter.ToInt64(raw, 8);

Console.WriteLine($"id: {id0}, id1: {id1}");

var a = """
        SELECT convert_from(payloadbinary, 'UTF8') AS payload_text
        FROM orleansstorage
        WHERE graintypestring = 'Auction'
        AND grainidn0 = 5763263338789921410
        AND grainidn1 = -8109836489196306007


        SELECT convert_from(payloadbinary, 'UTF8') AS payload_text
        FROM orleansstorage
        WHERE graintypestring = 'Auction_Batcher'
        AND grainid0 = 4779615048631993645
        AND grainid1 = -1727023542298368892;


        SELECT convert_from(payloadbinary, 'UTF8') AS payload_text
        FROM orleansstorage
        WHERE graintypestring = 'BuildingEmbeddableBuffArtifactState'
        AND grainidn0 = 4830066894368820805
        AND grainidn1 = -873751876417227369;

        SELECT convert_from(payloadbinary, 'UTF8') AS payload_text
        FROM orleansstorage
        WHERE graintypestring = 'GenericDelayState'
        AND grainidn0 = 5713460467791856461
        AND grainidn1 = 2751163635918376620;
        """;
        