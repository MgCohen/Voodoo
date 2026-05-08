using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Zenject;
using Random = UnityEngine.Random;

public enum GamePhase
{
    MAIN_MENU,
    LOADING,
    GAME,
    PRE_END,
    END
}

public class GameService : IGameService
{
    private const float c_PopPointTeamPadding = 7.5f;
    private const float c_PopPointPadding = 15.0f;
    private const float c_PowerUpPadding = 13.0f;
    private const float c_MinPowerUpRate = 1f;
    private const float c_MaxPowerUpRate = 2.5f;
    private const float c_BrushRate = 16f;

    public event Action<GamePhase>  onGamePhaseChanged;
    public event Action onScoresCalculated;

    public event Action onEndGame;

    public delegate void OnSceneReload();
    public event OnSceneReload onSceneReload;

    public delegate void OnPlayerSpawned(Player _Player, int _Index);
    public event OnPlayerSpawned onPlayerSpawned;

#if UNITY_EDITOR
    public int m_DebugLevel = 1;
    private static bool m_SaveCleared = false;
#endif

    public PowerUp m_BrushPowerUpPrefab => m_GameConfig.m_BrushPowerUpPrefab;
    public GameObject m_HumanPlayer => m_GameConfig.m_HumanPlayer;
    public GameObject m_IAPlayer => m_GameConfig.m_IAPlayer;

    public int m_PlayerSkinID { get; set; }

    public GamePhase currentPhase { get; private set; }
    public List<Player> m_Players { get; set; }
    public List<int> m_XPByRank => m_GameConfig.m_XPByRank;

    public bool m_AlreadyRevive = false;

    // Cache
    private IStatsService m_StatsService;
    private ProgressionView m_ProgressionView;
    private MainMenuView m_MainMenuView;
    private IBattleRoyaleService m_BattleRoyaleService;
    private ITerrainService m_TerrainService;

    private Transform m_HumanPlayerTr;

    public bool m_IsPlaying { get; set; }
    private float m_Level;
    public float level { get { return m_Level; } }
    private float m_LastPowerUpTime;
    private float m_LastBrushTime = 0f;
    private float m_PowerUpRate;
    private Vector3 m_SpotlightOffset;

    // Buffers
    private Vector3 m_PosBuffer;

    public List<BrushData> m_Brushs;
    public List<SkinData> m_Skins { get; set; }
    private List<Color> m_Colors;
    private List<Vector3> m_PopPoints;
    private List<PowerUpData> m_PowerUps;
    private PlayerNameData m_PlayerNameData;
    private List<GameObject> m_Objects; // Powerups and other map objects
    private List<Player> m_OrderedPlayers;
    private Transform m_HumanSpotlight;

    private GameConfig m_GameConfig;
    private DiContainer m_Container;
    private ISceneEventsService m_SceneEventsService;
    
    [Inject]
    public void Construct(GameConfig gameConfig, IStatsService statsService, IBattleRoyaleService battleRoyaleService,
        ITerrainService terrainService, DiContainer container, ISceneEventsService sceneEventsService)
    {
        m_GameConfig = gameConfig;
        m_StatsService = statsService;
        m_BattleRoyaleService = battleRoyaleService;
        m_TerrainService = terrainService;
        m_Container = container;
        m_PlayerSkinID = 1;
        m_Players = new List<Player>();
        m_Objects = new List<GameObject>();
        m_SceneEventsService = sceneEventsService;
        m_SceneEventsService.OnAwake += OnAwake;
        m_SceneEventsService.OnStart += OnStart;
        m_SceneEventsService.OnUpdate += OnUpdate;
        Init();
    }
    
    private void Init()
    {
#if UNITY_EDITOR
        if (m_SaveCleared == false)
        {
            //PlayerPrefs.DeleteAll();
            m_SaveCleared = true;
        }
        PlayerPrefs.SetInt(Constants.c_LevelSave, m_DebugLevel);
#endif

        Application.targetFrameRate = 60;

        m_Brushs = new List<BrushData>(Resources.LoadAll<BrushData>("Brushs"));
        m_Skins = new List<SkinData>(Resources.LoadAll<SkinData>("Skins"));

        List<ColorData> colorsData = new List<ColorData>(Resources.LoadAll<ColorData>("Colors"));
        m_Colors = new List<Color>();
        for (int i = 0; i < colorsData.Count; ++i)
        {
            ColorData colorData = colorsData[i];
            m_Colors.Add(colorData.m_Colors[0]);
        }

        m_PlayerNameData = Resources.Load<PlayerNameData>("PlayerNames");

        m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));
    }

    private void OnAwake()
    {
        float halfWidth = m_TerrainService.WorldHalfWidth;
        float halfHeight = m_TerrainService.WorldHalfHeight;

        m_PopPoints = new List<Vector3>() {
            new Vector3 (-halfWidth + c_PopPointPadding + c_PopPointTeamPadding, 0.0f, -halfHeight + c_PopPointPadding - c_PopPointTeamPadding),
            new Vector3 (-halfWidth + c_PopPointPadding - c_PopPointTeamPadding, 0.0f, -halfHeight + c_PopPointPadding + c_PopPointTeamPadding),
            new Vector3 (halfWidth - c_PopPointPadding - c_PopPointTeamPadding, 0.0f, -halfHeight + c_PopPointPadding - c_PopPointTeamPadding),
            new Vector3 (halfWidth - c_PopPointPadding + c_PopPointTeamPadding, 0.0f, -halfHeight + c_PopPointPadding + c_PopPointTeamPadding),
            new Vector3 (-halfWidth + c_PopPointPadding + c_PopPointTeamPadding, 0.0f, halfHeight - c_PopPointPadding + c_PopPointTeamPadding),
            new Vector3 (-halfWidth + c_PopPointPadding - c_PopPointTeamPadding, 0.0f, halfHeight - c_PopPointPadding - c_PopPointTeamPadding),
            new Vector3 (halfWidth - c_PopPointPadding + c_PopPointTeamPadding, 0.0f, halfHeight - c_PopPointPadding - c_PopPointTeamPadding),
            new Vector3 (halfWidth - c_PopPointPadding - c_PopPointTeamPadding, 0.0f, halfHeight - c_PopPointPadding + c_PopPointTeamPadding),
        };

        foreach (Player player in m_Players)
        {
            if (player != null)
                GameObject.Destroy(player.gameObject);
        }
        m_Players.Clear();
        
        foreach (GameObject go in m_Objects)
        {
            GameObject.Destroy(go);
        }
        m_Objects.Clear();
    }

    private void OnStart()
    {
        // Cache
        m_ProgressionView = ProgressionView.Instance;
        m_MainMenuView = MainMenuView.Instance;
        m_HumanSpotlight = GameObject.Instantiate<Transform>(m_GameConfig.m_HumanSpotlight);
        m_SpotlightOffset = m_HumanSpotlight.position;

        // Buffers
        m_Objects = new List<GameObject>();
        m_PosBuffer = Vector3.zero;

#if UNITY_EDITOR
        Debug.Log("Current difficulty is " + m_StatsService.GetLevel());
#endif
        ChangePhase(GamePhase.MAIN_MENU);
    }

    public List<Color> GetColors()
    {
        return (m_Colors);
    }

    public void SetColor(int colorIndex)
    {
        colorIndex = Mathf.Min(m_Colors.Count - 1, colorIndex);
        Color humanColor = m_Colors[colorIndex];
        RVEndView.Instance.SetTitleColor(humanColor);
        m_MainMenuView.SetTitleColor(humanColor);
        LoadingView.Instance.SetTitleColor(humanColor);
    }

    public void ChangePhase(GamePhase _GamePhase)
    {
        switch (_GamePhase)
        {
            case GamePhase.MAIN_MENU:
                Randomize();
                SetColor(ComputeCurrentPlayerColor(true, 0));
                break;

            case GamePhase.LOADING:
                m_LastBrushTime = Time.time;
                m_LastPowerUpTime = Time.time;
                m_PowerUpRate = Random.Range(c_MinPowerUpRate, c_MaxPowerUpRate);
                m_Level = m_StatsService.GetLevel();
                PopPlayers();

                if (m_OrderedPlayers == null)
                    m_OrderedPlayers = new List<Player>();
                else
                    m_OrderedPlayers.Clear();

                m_OrderedPlayers.AddRange(m_Players);
                break;

            case GamePhase.GAME:
                m_IsPlaying = true;
                break;

            case GamePhase.END:
                int playerScore = Mathf.RoundToInt(m_Players[0].percent * 100.0f);
                m_StatsService.TryToSetBestScore(playerScore);

                int rankingScore = -1; // Difficulty down by default
                int playerRank = m_BattleRoyaleService.GetHumanPlayer().m_Rank;

                if (playerRank == 0) // Best, then increase difficulty
                    rankingScore = 1;
                else if (playerRank >= 2) // Second or third, then stay at same difficulty
                    rankingScore = 0;

                m_StatsService.AddGameResult(rankingScore);
                int xp = m_XPByRank[playerRank];
                m_StatsService.SetLastXP(xp);
                PreEndView.Instance.LaunchPreEnd();
                m_StatsService.GainXP();
                break;
        }

        currentPhase = _GamePhase;

        if (onGamePhaseChanged != null)
            onGamePhaseChanged.Invoke(_GamePhase);
    }

    public void AddMapObject(GameObject _Object)
    {
        m_Objects.Add(_Object);
    }

    private void Randomize()
    {
        // Randomize points groups
        m_PopPoints.Shuffle();

        // Reset names
        m_PlayerNameData.Init();

        ClearGame();
    }

    private void PopPlayers()
    {
        bool humanSpawned = false;
        m_Players = new List<Player>(Constants.s_PlayerCount);

        for (int i = 0; i < Constants.s_PlayerCount; ++i)
        {
            Player newPlayer = PopPlayer(humanSpawned == false, m_PopPoints[i], i);

            m_Players.Add(newPlayer);
            humanSpawned = true;
        }

        m_BattleRoyaleService.SetPlayers(m_Players);
    }

    public int ComputeCurrentPlayerColor(bool _Human, int _Index)
    {
        int favoriteSkin = Mathf.Min(m_StatsService.FavoriteSkin, m_Skins.Count - 1);
        Color favoriteColor = m_Skins[favoriteSkin].Color.m_Colors[0];

        int favoriteColorIndex = 0;
        for (int i = 0; i < m_Colors.Count; i++)
        {
            if (favoriteColor == m_Colors[i])
            {
                favoriteColorIndex = i;
                break;
            }
        }

        if (_Human)
            return favoriteColorIndex;

        if (favoriteColorIndex <= _Index)
            return _Index + 1;
        else
            return _Index;
    }

    public int ComputeCurrentPlayeBrushIndex(bool _Human, int _Index)
    {
        if (!_Human)
            return _Index % m_Brushs.Count;

        int favoriteSkin = Mathf.Min(m_StatsService.FavoriteSkin, m_Skins.Count - 1);
        int selectedBrush = SkinToBrush(m_Skins[favoriteSkin]);
        return selectedBrush;
    }

    private Player PopPlayer(bool _Human, Vector3 _Position, int _Index)
    {
        GameObject playerGo = null;
        Player player = null;

        playerGo = m_Container.InstantiatePrefab((_Human == true) ? m_HumanPlayer : m_IAPlayer, _Position, Quaternion.identity, null) as GameObject;
        player = playerGo.GetComponent<Player>();

#if UNITY_EDITOR
        Debug.Log("m_PlayerSkinID : " + m_PlayerSkinID);
        Debug.Log("Human : " + _Human);
#endif

        int brushIndex = ComputeCurrentPlayeBrushIndex(_Human, _Index);
        int colorIndex = ComputeCurrentPlayerColor(_Human, _Index);
        player.Init(m_PlayerNameData.PickName(), m_Brushs[brushIndex], m_Colors[colorIndex]);

        if (_Human)
        {
            m_HumanPlayerTr = player.transform;
            m_BattleRoyaleService.SetHumanPlayer(player);
        }

        if (onPlayerSpawned != null)
            onPlayerSpawned.Invoke(player, _Index);

        return player;
    }

    void OnUpdate()
    {
        if (!m_IsPlaying)
            return;

        for (int i = 0; i < m_Players.Count; ++i)
        {
            if (m_Players[i].isEliminated == false)
                m_Players[i].CalculatePercentage();
        }

        m_OrderedPlayers.RemoveAll((x) => x.isEliminated);
        m_OrderedPlayers.Sort((x, y) => (int)((y.percent * 100000f) - (x.percent * 100000f)));

        for (int i = 0; i < m_OrderedPlayers.Count; ++i)
            m_OrderedPlayers[i].m_Rank = i;

        if (onScoresCalculated != null)
            onScoresCalculated.Invoke();

        m_ProgressionView.UpdateView();

        if (Time.time - m_LastPowerUpTime > m_PowerUpRate)
        {
            m_PowerUpRate = Random.Range(c_MinPowerUpRate, c_MaxPowerUpRate);
            m_LastPowerUpTime = Time.time;

            PowerUpData powerUpData = m_PowerUps[Random.Range(0, m_PowerUps.Count)];
            PopObjectRandomly(powerUpData.m_Prefab);
        }

        if (Time.time - m_LastBrushTime > c_BrushRate)
        {
            m_LastBrushTime = Time.time;
            PopObjectRandomly(m_BrushPowerUpPrefab.gameObject);
        }

        m_HumanSpotlight.position = m_HumanPlayerTr.position + m_SpotlightOffset;
    }

    public void PopObjectRandomly(GameObject _Prefab)
    {
        m_PosBuffer.Set(Random.Range(-m_TerrainService.WorldHalfWidth + c_PowerUpPadding, m_TerrainService.WorldHalfWidth - c_PowerUpPadding),
                             0.0f,
                        Random.Range(-m_TerrainService.WorldHalfHeight + c_PowerUpPadding, m_TerrainService.WorldHalfHeight - c_PowerUpPadding));

        if (Mathf.Abs(m_PosBuffer.x) < c_PowerUpPadding)
            m_PosBuffer.x += c_PowerUpPadding * Mathf.Sign(m_PosBuffer.x);
        if (Mathf.Abs(m_PosBuffer.z) < c_PowerUpPadding)
            m_PosBuffer.z += c_PowerUpPadding * Mathf.Sign(m_PosBuffer.z);

        m_TerrainService.ClampPosition(ref m_PosBuffer, Constants.c_SpawnBorderOffset);
        m_Objects.Add(m_Container.InstantiatePrefab(_Prefab, m_PosBuffer, Quaternion.identity, null));
    }

    public int PickBrushID()
    {
        return (Random.Range(0, m_Brushs.Count));
    }

    public GameObject PickBrush()
    {
        return m_Brushs[PickBrushID()].m_Prefab;
    }

    public GameObject PickPowerUp()
    {
        return m_PowerUps[Random.Range(0, m_PowerUps.Count)].m_Prefab;
    }

    public Player GetBestPlayer()
    {
        float maxPercent = 0f;
        Player result = null;

        for (int i = 0; i < m_Players.Count; ++i)
        {
            if (m_Players[i].percent > maxPercent)
            {
                result = m_Players[i];
                maxPercent = result.percent;
            }
        }

        return (result);
    }

    public void OnGameFinished()
    {
        m_IsPlaying = false;

        // Check if player didn't win
        if (m_AlreadyRevive == false && m_BattleRoyaleService.GetAlivePlayersCount() > 1)
        {
            if (currentPhase != GamePhase.PRE_END)
            {
                m_BattleRoyaleService.m_IsPlaying = false;

                ChangePhase(GamePhase.PRE_END);
            }
            return;
        }

        TriggerEndGame();

    }

    private void TriggerEndGame()
    {
        if (m_BattleRoyaleService.GetAlivePlayersCount() > 1)
            m_BattleRoyaleService.KillHumanPlayer();

        m_BattleRoyaleService.m_IsPlaying = true;

        if (onEndGame != null)
            onEndGame.Invoke();

        if (currentPhase != GamePhase.END)
            ChangePhase(GamePhase.END);
    }

    public void TriggerSceneReload()
    {
        if (onSceneReload != null)
            onSceneReload.Invoke();
    }

    public void TryRevive()
    {
        Revive();
    }

    private void OnWatchRVRevive(bool _Complete)
    {
        if (_Complete)
            Revive();
        else
            SkipRV();
    }

    private void Revive()
    {
        ChangePhase(GamePhase.GAME);
        m_IsPlaying = true;
        m_AlreadyRevive = true;
        m_Players[0].AddBrush();
    }

    public void SkipRV()
    {
        TriggerEndGame();
    }

    public string PickRandomName()
    {
        return (m_PlayerNameData.PickName());
    }

    public void ClearGame()
    {
        int i;

        m_TerrainService.SetTerrain();

        for (i = 0; i < m_Players.Count; ++i)
            m_Players[i].Remove();

        m_Players.Clear();

        for (i = 0; i < m_Objects.Count; ++i)
        {
            if (m_Objects[i] != null && m_Objects[i].gameObject != null)
                GameObject.Destroy(m_Objects[i].gameObject);
        }

        m_Objects.Clear();
    }

    public int SkinToBrush(SkinData skin)
    {
        for (int i = 0; i < m_Brushs.Count; i++)
        {
            if (m_Brushs[i] == skin.Brush)
                return i;
        }

        return 0;
    }

    public int SkinToColor(SkinData skin)
    {
        for (int i = 0; i < m_Colors.Count; i++)
        {
            if (m_Colors[i] == skin.Color.m_Colors[0])
                return i;
        }

        return 0;
    }
}
