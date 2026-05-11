using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

[TestFixture]
public class AIDifficultyTests
{
    // The original Draw.io IAPlayer used rubber-band difficulty:
    //   m_Difficulty = Random.Range(Clamp01(StatsService.GetLevel() / 2f), 1f);
    // Phase 1 moved this onto GameMode so booster can override per-level.
    // These tests lock in the contract so the rubber band can't silently
    // regress to uniform random again.

    private FakeStatsService m_Stats;
    private ClassicMode m_Classic;
    private BoosterMode m_Booster;

    [SetUp]
    public void SetUp()
    {
        m_Stats = new FakeStatsService();

        m_Classic = ScriptableObject.CreateInstance<ClassicMode>();
        m_Classic.m_XPThresholdPerLevel = new List<int> { 100 };
        m_Classic.m_Match = new MatchSettings();

        m_Booster = ScriptableObject.CreateInstance<BoosterMode>();
        m_Booster.m_AuthoredLevels = new List<BoosterLevelData>();
        m_Booster.m_FallbackLevel = MakeBoosterLevel(min: 0.9f, max: 1.0f);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(m_Classic);
        foreach (var lvl in m_Booster.m_AuthoredLevels)
            Object.DestroyImmediate(lvl);
        Object.DestroyImmediate(m_Booster.m_FallbackLevel);
        Object.DestroyImmediate(m_Booster);
    }

    // ---- Classic: rubber band ----

    [Test]
    public void ClassicMode_AIDifficultyMin_AtZeroLevel_IsZero()
    {
        m_Stats.Level = 0f;
        Assert.AreEqual(0f, m_Classic.GetAIDifficultyMin(m_Stats), 0.001f);
    }

    [Test]
    public void ClassicMode_AIDifficultyMin_AtHalfLevel_IsQuarter()
    {
        m_Stats.Level = 0.5f;
        Assert.AreEqual(0.25f, m_Classic.GetAIDifficultyMin(m_Stats), 0.001f);
    }

    [Test]
    public void ClassicMode_AIDifficultyMin_AtFullLevel_IsHalf()
    {
        m_Stats.Level = 1f;
        Assert.AreEqual(0.5f, m_Classic.GetAIDifficultyMin(m_Stats), 0.001f);
    }

    [Test]
    public void ClassicMode_AIDifficultyMax_IsAlwaysOne()
    {
        m_Stats.Level = 0f;
        Assert.AreEqual(1f, m_Classic.GetAIDifficultyMax(m_Stats), 0.001f);
        m_Stats.Level = 1f;
        Assert.AreEqual(1f, m_Classic.GetAIDifficultyMax(m_Stats), 0.001f);
    }

    // ---- Booster: per-level data ----

    [Test]
    public void BoosterMode_AIDifficulty_ReadsCurrentLevelData()
    {
        m_Booster.m_AuthoredLevels.Add(MakeBoosterLevel(min: 0.1f, max: 0.3f));
        m_Booster.m_AuthoredLevels.Add(MakeBoosterLevel(min: 0.5f, max: 0.7f));

        m_Stats.PlayerLevel = 1;
        Assert.AreEqual(0.1f, m_Booster.GetAIDifficultyMin(m_Stats), 0.001f);
        Assert.AreEqual(0.3f, m_Booster.GetAIDifficultyMax(m_Stats), 0.001f);

        m_Stats.PlayerLevel = 2;
        Assert.AreEqual(0.5f, m_Booster.GetAIDifficultyMin(m_Stats), 0.001f);
        Assert.AreEqual(0.7f, m_Booster.GetAIDifficultyMax(m_Stats), 0.001f);
    }

    [Test]
    public void BoosterMode_AIDifficulty_FallsBackBeyondAuthoredLevels()
    {
        m_Booster.m_AuthoredLevels.Add(MakeBoosterLevel(min: 0.1f, max: 0.3f));

        m_Stats.PlayerLevel = 99;
        Assert.AreEqual(0.9f, m_Booster.GetAIDifficultyMin(m_Stats), 0.001f);
        Assert.AreEqual(1.0f, m_Booster.GetAIDifficultyMax(m_Stats), 0.001f);
    }

    // ---- Booster: XP curve formula ----

    [Test]
    public void BoosterMode_XPForLevel_UsesFormula()
    {
        m_Booster.m_BaseXPThreshold = 100;
        m_Booster.m_XPIncrementPerLevel = 50;

        Assert.AreEqual(100, m_Booster.GetXPForLevel(0));
        Assert.AreEqual(150, m_Booster.GetXPForLevel(1));
        Assert.AreEqual(200, m_Booster.GetXPForLevel(2));
        Assert.AreEqual(5100, m_Booster.GetXPForLevel(100));
    }

    [Test]
    public void BoosterMode_XPForLevel_ClampsNegativeIndexToZero()
    {
        m_Booster.m_BaseXPThreshold = 100;
        m_Booster.m_XPIncrementPerLevel = 50;

        Assert.AreEqual(100, m_Booster.GetXPForLevel(-1));
    }

    // ---- Helpers ----

    private static BoosterLevelData MakeBoosterLevel(float min, float max)
    {
        var lvl = ScriptableObject.CreateInstance<BoosterLevelData>();
        lvl.m_Match = new MatchSettings { m_AIDifficultyMin = min, m_AIDifficultyMax = max };
        return lvl;
    }

    private class FakeStatsService : IStatsService
    {
        public float Level = 0f;
        public int PlayerLevel = 1;

        public float GetLevel() => Level;
        public int GetPlayerLevel() => PlayerLevel;
        public int GetPlayerLevel(GameMode _Mode) => PlayerLevel;

        // unused for these tests
        public void SetActiveMode(GameMode _Mode) { }
        public int GetBestScore() => 0;
        public void TryToSetBestScore(int _Score) { }
        public void AddGameResult(int _Rank) { }
        public void SetLastXP(int _XP) { }
        public void GainXP() { }
        public int FavoriteSkin { get; set; }
        public int m_LastGain { get; set; }
        public int GetXP() => 0;
        public int XPToNextLevel(int _Level) => 100;
        public string GetNickname() => "";
        public void SetNickname(string _Name) { }
    }
}
