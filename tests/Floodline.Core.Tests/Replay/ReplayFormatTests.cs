#pragma warning disable JSON002

using Floodline.Core.Movement;
using Floodline.Core.Replay;
using Xunit;

namespace Floodline.Core.Tests.Replay;

public class ReplayFormatTests
{
    [Fact]
    public void Serialize_ProducesStableJson()
    {
        ReplayFile replay = new(
            new ReplayMeta(
                ReplayFormat.ReplayVersion,
                "0.2.0",
                "level-1",
                "hash-abc",
                12345,
                60,
                "Windows",
                ReplayFormat.InputEncoding),
            [
                new(2, InputCommand.MoveLeft),
                new(0, InputCommand.RotateWorldLeft),
                new(1, InputCommand.Hold)
            ]);

        string json = ReplaySerializer.Serialize(replay);

        const string expected =
            "{\"meta\":{\"replayVersion\":\"0.1.1\",\"rulesVersion\":\"0.2.0\",\"levelId\":\"level-1\",\"levelHash\":\"hash-abc\",\"seed\":12345,\"tickRate\":60,\"platform\":\"Windows\",\"inputEncoding\":\"command-v1\"},\"inputs\":[{\"tick\":0,\"command\":\"RotateWorldLeft\"},{\"tick\":1,\"command\":\"Hold\"},{\"tick\":2,\"command\":\"MoveLeft\"}]}";

        Assert.Equal(expected, json);
    }

    [Fact]
    public void Deserialize_RoundtripsStableJson()
    {
        const string json =
            "{\"meta\":{\"replayVersion\":\"0.1.1\",\"rulesVersion\":\"0.2.0\",\"levelId\":\"level-1\",\"levelHash\":\"hash-abc\",\"seed\":12345,\"tickRate\":60,\"platform\":\"Windows\",\"inputEncoding\":\"command-v1\"},\"inputs\":[{\"tick\":0,\"command\":\"RotateWorldLeft\"},{\"tick\":1,\"command\":\"Hold\"},{\"tick\":2,\"command\":\"MoveLeft\"}]}";

        ReplayFile replay = ReplaySerializer.Deserialize(json);
        string roundtrip = ReplaySerializer.Serialize(replay);

        Assert.Equal(json, roundtrip);
    }
}
