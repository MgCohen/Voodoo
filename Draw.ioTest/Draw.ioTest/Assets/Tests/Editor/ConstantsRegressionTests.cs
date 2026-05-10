using NUnit.Framework;

[TestFixture]
public class ConstantsRegressionTests
{
    // Phase 1 moves gameplay constants from GameService/Constants into IGameMode.
    // These tests lock down the current values so any mismatch is caught.

    // ---- PlayerPrefs key names ----

    [Test]
    public void BestScoreKey_IsBestScore()
    {
        Assert.AreEqual("BestScore", Constants.c_BestScoreSave);
    }

    [Test]
    public void GameResultKey_IsGameResult()
    {
        Assert.AreEqual("GameResult", Constants.c_GameResultSave);
    }

    [Test]
    public void PlayerXPKey_IsXP()
    {
        Assert.AreEqual("XP", Constants.c_PlayerXPSave);
    }

    [Test]
    public void PlayerLevelKey_IsLvl()
    {
        Assert.AreEqual("Lvl", Constants.c_PlayerLevelSave);
    }

    [Test]
    public void PlayerNameKey_IsNickname()
    {
        Assert.AreEqual("Nickname", Constants.c_PlayerNameSave);
    }

    // ---- Gameplay values ----

    [Test]
    public void SavedGameCount_IsFive()
    {
        Assert.AreEqual(5, Constants.c_SavedGameCount);
    }
}
