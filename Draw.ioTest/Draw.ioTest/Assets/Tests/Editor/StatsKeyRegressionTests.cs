using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

[TestFixture]
public class StatsKeyRegressionTests
{
    // Phase 1 prepends m_ActivePrefix to every per-mode PlayerPrefs key.
    // These tests verify that StatsService reads/writes the EXACT keys
    // we expect, by writing raw PlayerPrefs and reading via StatsService
    // (and vice versa). If the prefix logic corrupts a key, these fail.

    private StatsService m_StatsService;
    private StatsConfig m_Config;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();

        m_Config = ScriptableObject.CreateInstance<StatsConfig>();
        m_Config.m_XPForLevel = new List<int> { 100 };

        m_StatsService = new StatsService();
        m_StatsService.Construct(m_Config);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteAll();
        Object.DestroyImmediate(m_Config);
    }

    // ---- Write via StatsService, read via raw PlayerPrefs ----

    [Test]
    public void TryToSetBestScore_WritesExactKey_BestScore()
    {
        m_StatsService.TryToSetBestScore(42);
        Assert.IsTrue(PlayerPrefs.HasKey("BestScore"));
        Assert.AreEqual(42, PlayerPrefs.GetInt("BestScore"));
    }

    [Test]
    public void AddGameResult_WritesExactKeys_GameResult_0_Through_4()
    {
        for (int i = 0; i < Constants.c_SavedGameCount; i++)
            m_StatsService.AddGameResult(i + 10);

        for (int i = 0; i < Constants.c_SavedGameCount; i++)
        {
            int expected = (Constants.c_SavedGameCount - 1 + 10) - i;
            Assert.AreEqual(expected, PlayerPrefs.GetInt("GameResult_" + i));
        }
    }

    [Test]
    public void GainXP_WritesExactKey_XP()
    {
        int subThreshold = m_StatsService.XPToNextLevel() / 2;
        m_StatsService.SetLastXP(subThreshold);
        m_StatsService.GainXP();
        Assert.IsTrue(PlayerPrefs.HasKey("XP"));
        Assert.AreEqual(subThreshold, PlayerPrefs.GetInt("XP"));
    }

    [Test]
    public void GainXP_LevelUp_WritesExactKey_Lvl()
    {
        int threshold = m_StatsService.XPToNextLevel();
        m_StatsService.SetLastXP(threshold + 1);
        m_StatsService.GainXP();
        Assert.IsTrue(PlayerPrefs.HasKey("Lvl"));
        Assert.AreEqual(2, PlayerPrefs.GetInt("Lvl"));
    }

    [Test]
    public void SetNickname_WritesExactKey_Nickname()
    {
        m_StatsService.SetNickname("TestName");
        Assert.IsTrue(PlayerPrefs.HasKey("Nickname"));
        Assert.AreEqual("TestName", PlayerPrefs.GetString("Nickname"));
    }

    [Test]
    public void FavoriteSkin_WritesExactKey_FavoriteSkin()
    {
        m_StatsService.FavoriteSkin = 5;
        Assert.IsTrue(PlayerPrefs.HasKey("FavoriteSkin"));
        Assert.AreEqual(5, PlayerPrefs.GetInt("FavoriteSkin"));
    }

    // ---- Write via raw PlayerPrefs, read via StatsService ----

    [Test]
    public void GetBestScore_ReadsExactKey_BestScore()
    {
        PlayerPrefs.SetInt("BestScore", 99);
        Assert.AreEqual(99, m_StatsService.GetBestScore());
    }

    [Test]
    public void GetXP_ReadsExactKey_XP()
    {
        PlayerPrefs.SetInt("XP", 77);
        Assert.AreEqual(77, m_StatsService.GetXP());
    }

    [Test]
    public void GetPlayerLevel_ReadsExactKey_Lvl()
    {
        PlayerPrefs.SetInt("Lvl", 3);
        Assert.AreEqual(3, m_StatsService.GetPlayerLevel());
    }

    [Test]
    public void GetLevel_ReadsExactKeys_GameResult_0_Through_N()
    {
        for (int i = 0; i < Constants.c_SavedGameCount; i++)
            PlayerPrefs.SetInt("GameResult_" + i, 1);

        Assert.AreEqual(1f, m_StatsService.GetLevel(), 0.001f);
    }

    [Test]
    public void GetNickname_ReadsExactKey_Nickname()
    {
        PlayerPrefs.SetString("Nickname", "DirectWrite");
        Assert.AreEqual("DirectWrite", m_StatsService.GetNickname());
    }

    [Test]
    public void FavoriteSkin_ReadsExactKey_FavoriteSkin()
    {
        PlayerPrefs.SetInt("FavoriteSkin", 7);
        Assert.AreEqual(7, m_StatsService.FavoriteSkin);
    }

    // ---- Key isolation: per-mode keys don't collide ----

    [Test]
    public void AllPerModeKeys_AreDistinct()
    {
        int threshold = m_StatsService.XPToNextLevel();
        int xpToGrant = threshold + 50;
        int expectedRemainder = xpToGrant - threshold;

        m_StatsService.TryToSetBestScore(10);
        m_StatsService.AddGameResult(1);
        m_StatsService.SetLastXP(xpToGrant);
        m_StatsService.GainXP();

        Assert.AreEqual(10, PlayerPrefs.GetInt("BestScore"));
        Assert.AreEqual(1, PlayerPrefs.GetInt("GameResult_0"));
        Assert.AreEqual(expectedRemainder, PlayerPrefs.GetInt("XP"));
        Assert.AreEqual(2, PlayerPrefs.GetInt("Lvl"));

        // Verify no cross-contamination
        Assert.AreNotEqual(PlayerPrefs.GetInt("BestScore"), PlayerPrefs.GetInt("XP"));
        Assert.AreNotEqual(PlayerPrefs.GetInt("GameResult_0"), PlayerPrefs.GetInt("XP"));
    }

    // ---- No unexpected keys written ----

    [Test]
    public void TryToSetBestScore_DoesNotWriteBoosterKeys()
    {
        m_StatsService.TryToSetBestScore(50);
        Assert.IsFalse(PlayerPrefs.HasKey("Booster_BestScore"));
    }

    [Test]
    public void GainXP_DoesNotWriteBoosterKeys()
    {
        int subThreshold = m_StatsService.XPToNextLevel() / 2;
        m_StatsService.SetLastXP(subThreshold);
        m_StatsService.GainXP();
        Assert.IsFalse(PlayerPrefs.HasKey("Booster_XP"));
        Assert.IsFalse(PlayerPrefs.HasKey("Booster_Lvl"));
    }

    [Test]
    public void AddGameResult_DoesNotWriteBoosterKeys()
    {
        m_StatsService.AddGameResult(1);
        Assert.IsFalse(PlayerPrefs.HasKey("Booster_GameResult_0"));
    }
}
