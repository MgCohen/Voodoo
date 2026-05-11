using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

[TestFixture]
public class StatsServiceTests
{
    private StatsService m_StatsService;
    private ClassicMode m_Mode;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteAll();

        m_Mode = ScriptableObject.CreateInstance<ClassicMode>();
        m_Mode.m_XPThresholdPerLevel = new List<int> { 100, 200, 300, 400, 500 };

        m_StatsService = new StatsService();
        m_StatsService.SetActiveMode(m_Mode);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteAll();
        Object.DestroyImmediate(m_Mode);
    }

    // ---- Best Score: key = "BestScore" ----

    [Test]
    public void GetBestScore_ReadsFromBestScoreKey()
    {
        PlayerPrefs.SetInt(Constants.c_BestScoreSave, 42);
        Assert.AreEqual(42, m_StatsService.GetBestScore());
    }

    [Test]
    public void GetBestScore_DefaultsToZero()
    {
        Assert.AreEqual(0, m_StatsService.GetBestScore());
    }

    [Test]
    public void TryToSetBestScore_WritesToBestScoreKey()
    {
        m_StatsService.TryToSetBestScore(50);
        Assert.AreEqual(50, PlayerPrefs.GetInt(Constants.c_BestScoreSave));
    }

    [Test]
    public void TryToSetBestScore_OnlyUpdatesIfHigher()
    {
        m_StatsService.TryToSetBestScore(50);
        m_StatsService.TryToSetBestScore(30);
        Assert.AreEqual(50, m_StatsService.GetBestScore());
    }

    [Test]
    public void TryToSetBestScore_OverwritesWhenHigher()
    {
        m_StatsService.TryToSetBestScore(50);
        m_StatsService.TryToSetBestScore(80);
        Assert.AreEqual(80, m_StatsService.GetBestScore());
    }

    // ---- XP: key = "XP" ----

    [Test]
    public void GetXP_ReadsFromXPKey()
    {
        PlayerPrefs.SetInt(Constants.c_PlayerXPSave, 75);
        Assert.AreEqual(75, m_StatsService.GetXP());
    }

    [Test]
    public void GetXP_DefaultsToZero()
    {
        Assert.AreEqual(0, m_StatsService.GetXP());
    }

    // ---- Player Level: key = "Lvl" ----

    [Test]
    public void GetPlayerLevel_ReadsFromLvlKey()
    {
        PlayerPrefs.SetInt(Constants.c_PlayerLevelSave, 5);
        Assert.AreEqual(5, m_StatsService.GetPlayerLevel());
    }

    [Test]
    public void GetPlayerLevel_DefaultsToOne()
    {
        Assert.AreEqual(1, m_StatsService.GetPlayerLevel());
    }

    // ---- Game Results: keys = "GameResult_0" through "GameResult_4" ----

    [Test]
    public void AddGameResult_WritesToGameResult0Key()
    {
        m_StatsService.AddGameResult(1);
        Assert.AreEqual(1, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_0"));
    }

    [Test]
    public void AddGameResult_ShiftsHistoryForward()
    {
        m_StatsService.AddGameResult(1);
        m_StatsService.AddGameResult(0);
        m_StatsService.AddGameResult(-1);

        Assert.AreEqual(-1, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_0"));
        Assert.AreEqual(0, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_1"));
        Assert.AreEqual(1, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_2"));
    }

    [Test]
    public void AddGameResult_CapsAtSavedGameCount()
    {
        int overflow = Constants.c_SavedGameCount + 2;
        for (int i = 0; i < overflow; i++)
            m_StatsService.AddGameResult(i);

        for (int i = 0; i < Constants.c_SavedGameCount; i++)
        {
            int expected = overflow - 1 - i;
            Assert.AreEqual(expected, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_" + i));
        }
    }

    // ---- GetLevel (rubber-band difficulty) ----

    [Test]
    public void GetLevel_DefaultsToZero_NoResults()
    {
        Assert.AreEqual(0f, m_StatsService.GetLevel(), 0.001f);
    }

    [Test]
    public void GetLevel_AllWins_ReturnsOne()
    {
        for (int i = 0; i < Constants.c_SavedGameCount; i++)
            m_StatsService.AddGameResult(1);

        Assert.AreEqual(1f, m_StatsService.GetLevel(), 0.001f);
    }

    [Test]
    public void GetLevel_MixedResults_ReturnsAverage()
    {
        int wins = 3;
        m_StatsService.AddGameResult(1);
        m_StatsService.AddGameResult(1);
        m_StatsService.AddGameResult(1);

        float expected = (float)wins / Constants.c_SavedGameCount;
        Assert.AreEqual(expected, m_StatsService.GetLevel(), 0.001f);
    }

    [Test]
    public void GetLevel_NegativeResults_ClampsToZero()
    {
        for (int i = 0; i < Constants.c_SavedGameCount; i++)
            m_StatsService.AddGameResult(-1);

        Assert.AreEqual(0f, m_StatsService.GetLevel(), 0.001f);
    }

    // ---- SetLastXP / GainXP / LevelUp ----

    [Test]
    public void SetLastXP_StoresInMemory()
    {
        m_StatsService.SetLastXP(50);
        Assert.AreEqual(50, m_StatsService.m_LastGain);
    }

    [Test]
    public void GainXP_AccumulatesXP()
    {
        int subThreshold = m_StatsService.XPToNextLevel() / 2;
        m_StatsService.SetLastXP(subThreshold);
        m_StatsService.GainXP();
        Assert.AreEqual(subThreshold, m_StatsService.GetXP());
    }

    [Test]
    public void GainXP_WritesToXPKey()
    {
        int subThreshold = m_StatsService.XPToNextLevel() / 2;
        m_StatsService.SetLastXP(subThreshold);
        m_StatsService.GainXP();
        Assert.AreEqual(subThreshold, PlayerPrefs.GetInt(Constants.c_PlayerXPSave));
    }

    [Test]
    public void GainXP_TriggersLevelUp_AtThreshold()
    {
        int threshold = m_StatsService.XPToNextLevel();
        int overflow = 50;
        m_StatsService.SetLastXP(threshold + overflow);
        m_StatsService.GainXP();
        Assert.AreEqual(2, m_StatsService.GetPlayerLevel());
        Assert.AreEqual(overflow, m_StatsService.GetXP());
    }

    [Test]
    public void GainXP_MultipleLevelUps()
    {
        int threshold1 = m_Mode.m_XPThresholdPerLevel[0];
        int threshold2 = m_Mode.m_XPThresholdPerLevel[1];
        int overflow = 50;
        m_StatsService.SetLastXP(threshold1 + threshold2 + overflow);
        m_StatsService.GainXP();
        Assert.AreEqual(3, m_StatsService.GetPlayerLevel());
        Assert.AreEqual(overflow, m_StatsService.GetXP());
    }

    [Test]
    public void GainXP_ExactThreshold_LevelsUpWithZeroRemainder()
    {
        int threshold = m_StatsService.XPToNextLevel();
        m_StatsService.SetLastXP(threshold);
        m_StatsService.GainXP();
        Assert.AreEqual(2, m_StatsService.GetPlayerLevel());
        Assert.AreEqual(0, m_StatsService.GetXP());
    }

    [Test]
    public void GainXP_LevelWritesToLvlKey()
    {
        int threshold = m_StatsService.XPToNextLevel();
        m_StatsService.SetLastXP(threshold + 1);
        m_StatsService.GainXP();
        Assert.AreEqual(2, PlayerPrefs.GetInt(Constants.c_PlayerLevelSave));
    }

    [Test]
    public void GainXP_SubThreshold_NoLevelUp()
    {
        int subThreshold = m_StatsService.XPToNextLevel() - 1;
        m_StatsService.SetLastXP(subThreshold);
        m_StatsService.GainXP();
        Assert.AreEqual(1, m_StatsService.GetPlayerLevel());
        Assert.AreEqual(subThreshold, m_StatsService.GetXP());
    }

    [Test]
    public void GainXP_AccumulatesAcrossMultipleCalls()
    {
        int threshold = m_StatsService.XPToNextLevel();
        int firstGain = threshold / 2 + 10;
        int secondGain = threshold / 2 + 10;
        int totalXP = firstGain + secondGain;

        m_StatsService.SetLastXP(firstGain);
        m_StatsService.GainXP();
        Assert.AreEqual(firstGain, m_StatsService.GetXP());
        Assert.AreEqual(1, m_StatsService.GetPlayerLevel());

        m_StatsService.SetLastXP(secondGain);
        m_StatsService.GainXP();
        Assert.AreEqual(2, m_StatsService.GetPlayerLevel());
        Assert.AreEqual(totalXP - threshold, m_StatsService.GetXP());
    }

    // ---- XPToNextLevel ----

    [Test]
    public void XPToNextLevel_ReturnsFirstEntry_AtLevel1()
    {
        Assert.AreEqual(m_Mode.m_XPThresholdPerLevel[0], m_StatsService.XPToNextLevel());
    }

    [Test]
    public void XPToNextLevel_ReturnsCorrectEntry_AtLevel2()
    {
        PlayerPrefs.SetInt(Constants.c_PlayerLevelSave, 2);
        Assert.AreEqual(m_Mode.m_XPThresholdPerLevel[1], m_StatsService.XPToNextLevel());
    }

    [Test]
    public void XPToNextLevel_ClampsToLastEntry_BeyondList()
    {
        PlayerPrefs.SetInt(Constants.c_PlayerLevelSave, 20);
        int lastEntry = m_Mode.m_XPThresholdPerLevel[m_Mode.m_XPThresholdPerLevel.Count - 1];
        Assert.AreEqual(lastEntry, m_StatsService.XPToNextLevel());
    }

    // ---- Mode-agnostic stats (must NOT be affected by any future prefix) ----

    [Test]
    public void Nickname_UsesPlayerNameSaveKey()
    {
        m_StatsService.SetNickname("TestPlayer");
        Assert.AreEqual("TestPlayer", PlayerPrefs.GetString(Constants.c_PlayerNameSave));
        Assert.AreEqual("TestPlayer", m_StatsService.GetNickname());
    }

    [Test]
    public void FavoriteSkin_UsesFavoriteSkinKey()
    {
        m_StatsService.FavoriteSkin = 3;
        Assert.AreEqual(3, PlayerPrefs.GetInt("FavoriteSkin"));
        Assert.AreEqual(3, m_StatsService.FavoriteSkin);
    }

    // ---- Cross-stat isolation ----

    [Test]
    public void BestScore_DoesNotAffectXP()
    {
        m_StatsService.TryToSetBestScore(99);
        Assert.AreEqual(0, m_StatsService.GetXP());
    }

    [Test]
    public void XPGain_DoesNotAffectBestScore()
    {
        int subThreshold = m_StatsService.XPToNextLevel() / 2;
        m_StatsService.SetLastXP(subThreshold);
        m_StatsService.GainXP();
        Assert.AreEqual(0, m_StatsService.GetBestScore());
    }

    [Test]
    public void GameResult_DoesNotAffectPlayerLevel()
    {
        int levelBefore = m_StatsService.GetPlayerLevel();
        m_StatsService.AddGameResult(1);
        Assert.AreEqual(levelBefore, m_StatsService.GetPlayerLevel());
    }

    [Test]
    public void Nickname_DoesNotAffectBestScoreOrXP()
    {
        m_StatsService.SetNickname("TestPlayer");
        Assert.AreEqual(0, m_StatsService.GetBestScore());
        Assert.AreEqual(0, m_StatsService.GetXP());
    }

    // ---- End-of-match sequence (mirrors GameService.ChangePhase(END)) ----

    [Test]
    public void EndOfMatch_FirstPlace_FullSequence()
    {
        int playerScore = 85;
        int playerRank = 0;
        int threshold = m_StatsService.XPToNextLevel();

        m_StatsService.TryToSetBestScore(playerScore);

        int rankingScore = -1;
        if (playerRank == 0) rankingScore = 1;
        else if (playerRank >= 2) rankingScore = 0;

        m_StatsService.AddGameResult(rankingScore);
        m_StatsService.SetLastXP(threshold);

        int xpBeforeGain = m_StatsService.GetXP();
        int levelBeforeGain = m_StatsService.GetPlayerLevel();

        m_StatsService.GainXP();

        Assert.AreEqual(playerScore, m_StatsService.GetBestScore());
        Assert.AreEqual(1, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_0"));
        Assert.AreEqual(0, xpBeforeGain);
        Assert.AreEqual(1, levelBeforeGain);
        Assert.AreEqual(2, m_StatsService.GetPlayerLevel());
        Assert.AreEqual(0, m_StatsService.GetXP());
    }

    [Test]
    public void EndOfMatch_SecondPlace_DifficultyDown()
    {
        int playerRank = 1;

        int rankingScore = -1;
        if (playerRank == 0) rankingScore = 1;
        else if (playerRank >= 2) rankingScore = 0;

        Assert.AreEqual(-1, rankingScore);
        m_StatsService.AddGameResult(rankingScore);
        Assert.AreEqual(-1, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_0"));
    }

    [Test]
    public void EndOfMatch_ThirdOrWorse_DifficultyStays()
    {
        int playerRank = 5;

        int rankingScore = -1;
        if (playerRank == 0) rankingScore = 1;
        else if (playerRank >= 2) rankingScore = 0;

        Assert.AreEqual(0, rankingScore);
        m_StatsService.AddGameResult(rankingScore);
        Assert.AreEqual(0, PlayerPrefs.GetInt(Constants.c_GameResultSave + "_0"));
    }

    [Test]
    public void EndOfMatch_MultipleGames_ProgressionAccumulates()
    {
        int threshold = m_StatsService.XPToNextLevel();
        int firstXP = threshold / 2;
        int secondXP = threshold / 2 + 10;

        // Game 1
        m_StatsService.TryToSetBestScore(70);
        m_StatsService.AddGameResult(1);
        m_StatsService.SetLastXP(firstXP);
        m_StatsService.GainXP();

        Assert.AreEqual(70, m_StatsService.GetBestScore());
        Assert.AreEqual(firstXP, m_StatsService.GetXP());
        Assert.AreEqual(1, m_StatsService.GetPlayerLevel());

        // Game 2: combined XP crosses threshold → level up
        m_StatsService.TryToSetBestScore(90);
        m_StatsService.AddGameResult(1);
        m_StatsService.SetLastXP(secondXP);
        m_StatsService.GainXP();

        Assert.AreEqual(90, m_StatsService.GetBestScore());
        Assert.AreEqual(2, m_StatsService.GetPlayerLevel());
        Assert.AreEqual(firstXP + secondXP - threshold, m_StatsService.GetXP());
    }
}
