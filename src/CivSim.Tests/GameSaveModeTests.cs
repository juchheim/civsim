using System;
using System.IO;
using CivSim.Game;
using CivSim.Sim.Ai;
using CivSim.Sim.Core;
using CivSim.Sim.Research;

namespace CivSim.Tests;

public sealed class GameSaveModeTests
{
    [Theory]
    [InlineData(ChapterMode.Iron, 1)]
    [InlineData(ChapterMode.Fief, 2)]
    public void Non_Bronze_save_modes_round_trip_without_changing_their_appended_bytes(ChapterMode mode, byte encoded)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            GameSaveMode.WriteTag(writer, mode);
        Assert.Equal(new[] { encoded }, stream.ToArray());
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        ChapterMode restored = GameSaveMode.ReadTag(reader);
        Assert.Equal(mode, restored);
        Assert.Equal(mode, ChapterRules.For(restored).Mode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(255)]
    public void Bronze_and_unknown_mode_bytes_are_rejected_in_a_non_Bronze_record(byte encoded)
    {
        Assert.Throws<InvalidDataException>(() => GameSaveMode.Decode(encoded));
    }

    [Fact]
    public void Bronze_is_not_encoded_as_a_version_6_mode_byte()
    {
        Assert.Throws<InvalidDataException>(() => GameSaveMode.Encode(ChapterMode.Bronze));
    }

    [Fact]
    public void Legacy_replay_footer_without_a_mode_tag_defaults_to_Bronze()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        GameSaveMode.WriteOptionalTag(writer, ChapterMode.Bronze);
        Assert.Empty(stream.ToArray());
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        Assert.Equal(ChapterMode.Bronze, GameSaveMode.ReadOptionalTag(reader));
    }

    [Fact]
    public void Fief_mode_has_a_distinct_player_facing_label()
    {
        Assert.Equal("Faith & Fief", GameSaveMode.ChapterLabel(ChapterMode.Fief));
        Assert.Equal("Iron mode", GameSaveMode.ChapterLabel(ChapterMode.Iron));
        Assert.Equal("Bronze mode", GameSaveMode.ChapterLabel(ChapterMode.Bronze));
        Assert.Equal("FAITH & FIEF · IRON AGE", GameSaveMode.AgeLabel(ChapterMode.Fief, "Iron"));
        Assert.Equal("IRON AGE", GameSaveMode.AgeLabel(ChapterMode.Iron, "Iron"));
        Assert.Equal("BRONZE AGE", GameSaveMode.AgeLabel(ChapterMode.Bronze, "Bronze"));
        Assert.Equal("Faith & Fief · Year 8.", GameSaveMode.LoadDetail(ChapterMode.Fief, 8));
        Assert.Equal("Year 8.", GameSaveMode.LoadDetail(ChapterMode.Iron, 8));
        Assert.Equal("Faith & Fief · 120 simulated minutes.", GameSaveMode.ReplayDetail(ChapterMode.Fief, "120 simulated minutes."));
        Assert.Equal("120 simulated minutes.", GameSaveMode.ReplayDetail(ChapterMode.Iron, "120 simulated minutes."));
    }

    [Fact]
    public void Rival_countdown_toast_visibility_follows_chapter_rules()
    {
        Assert.True(GameSaveMode.ShowsRivalCountdown(ChapterMode.Bronze));
        Assert.True(GameSaveMode.ShowsRivalCountdown(ChapterMode.Iron));
        Assert.False(GameSaveMode.ShowsRivalCountdown(ChapterMode.Fief));
    }

    [Theory]
    [InlineData(ChapterMode.Bronze, 5U)]
    [InlineData(ChapterMode.Iron, 6U)]
    [InlineData(ChapterMode.Fief, 6U)]
    public void Quick_save_file_round_trips_mode_and_restores_the_matching_rules(ChapterMode mode, uint expectedVersion)
    {
        string path = Path.Combine(Path.GetTempPath(), $"civsim-mode-{Guid.NewGuid():N}.civsave");
        byte[] packHash = new byte[Replay.ContentHashLength];
        var data = new GameSaveData
        {
            Seed = 123,
            ViewerCiv = 0,
            Difficulty = Difficulty.Chieftain,
            ChecksumInterval = 1,
            PackHash = packHash,
            Snapshot = new byte[] { 1 },
            ViewState = new byte[] { 2 },
            Mode = mode,
        };

        try
        {
            GameSave.Write(path, data);
            using (var file = File.OpenRead(path))
            using (var reader = new BinaryReader(file))
            {
                Assert.Equal(0x53475343U, reader.ReadUInt32());
                Assert.Equal(expectedVersion, reader.ReadUInt32());
            }

            GameSaveData restored = GameSave.Read(path, packHash);
            Assert.Equal(mode, restored.Mode);
            Assert.Equal(mode, ChapterRules.For(restored.Mode).Mode);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData(5U)]
    [InlineData(4U)]
    public void Legacy_quick_saves_without_a_mode_tag_load_as_Bronze(uint version)
    {
        string path = Path.Combine(Path.GetTempPath(), $"civsim-legacy-mode-{Guid.NewGuid():N}.civsave");
        byte[] packHash = new byte[Replay.ContentHashLength];
        var data = new GameSaveData
        {
            Seed = 123,
            ViewerCiv = 0,
            Difficulty = Difficulty.Chieftain,
            ChecksumInterval = 1,
            PackHash = packHash,
            Snapshot = new byte[] { 1 },
            ViewState = new byte[] { 2 },
            Mode = ChapterMode.Bronze,
        };

        try
        {
            GameSave.Write(path, data); // Version 5 is the old Bronze layout.
            if (version == 4)
            {
                byte[] bytes = File.ReadAllBytes(path);
                Array.Resize(ref bytes, bytes.Length - sizeof(int)); // v4 predates the v5 settlement-name count.
                BitConverter.GetBytes(version).CopyTo(bytes, sizeof(uint));
                File.WriteAllBytes(path, bytes);
            }

            GameSaveData restored = GameSave.Read(path, packHash);
            Assert.Equal(ChapterMode.Bronze, restored.Mode);
            ChapterRules rules = ChapterRules.For(restored.Mode);
            Assert.Equal(3, rules.LastResearchEra);
            Assert.Equal("KEY-4", TestPack.Catalog.Projects[TestPack.Catalog.KeystoneLeaving(rules.LastResearchEra)].Id);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData(ChapterMode.Bronze)]
    [InlineData(ChapterMode.Iron)]
    [InlineData(ChapterMode.Fief)]
    public void Replay_footer_round_trips_mode(ChapterMode mode)
    {
        var replay = new Replay { MatchSeed = 123, ContentHash = new byte[Replay.ContentHashLength], ChecksumInterval = 1 };
        using var stream = new MemoryStream();
        GameSave.WriteReplay(stream, replay, Difficulty.Chieftain, mode);
        stream.Position = 0;

        Replay restored = GameSave.ReadReplay(stream, out Difficulty difficulty, out ChapterMode restoredMode);
        Assert.Equal(123UL, restored.MatchSeed);
        Assert.Equal(Difficulty.Chieftain, difficulty);
        Assert.Equal(mode, restoredMode);
    }

    [Fact]
    public void Legacy_replay_footer_without_mode_still_defaults_to_Bronze()
    {
        var replay = new Replay { MatchSeed = 123, ContentHash = new byte[Replay.ContentHashLength], ChecksumInterval = 1 };
        using var stream = new MemoryStream();
        replay.Write(stream);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x46444943U);
            writer.Write((byte)Difficulty.Chieftain);
        }
        stream.Position = 0;

        GameSave.ReadReplay(stream, out _, out ChapterMode mode);
        Assert.Equal(ChapterMode.Bronze, mode);
    }
}
