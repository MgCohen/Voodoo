using UnityEngine;
using Zenject;

[CreateAssetMenu(fileName = "ManagersInstaller", menuName = "Installer/ManagersInstaller")]
public class ManagersInstaller : ScriptableObjectInstaller<ManagersInstaller>
{
    [SerializeField] private BattleRoyaleConfig m_BattleRoyaleConfig;
    [SerializeField] private GameplayConfig m_GameplayConfig;
    [SerializeField] private ClassicMode m_ClassicMode;
    [SerializeField] private BoosterMode m_BoosterMode;
    [SerializeField] private RankingConfig m_RankingConfig;
    [SerializeField] private TerrainConfig m_TerrainConfig;

    
    public override void InstallBindings()
    {
        Container.Bind<ClassicMode>().FromInstance(m_ClassicMode).AsSingle();
        Container.Bind<BoosterMode>().FromInstance(m_BoosterMode).AsSingle();

        Container.BindInterfacesAndSelfTo<BattleRoyaleService>().FromSubContainerResolve().ByMethod(InstallBattleRoyaleManager).AsSingle();
        Container.BindInterfacesAndSelfTo<GameService>().FromSubContainerResolve().ByMethod(InstallGameManager).AsSingle();
        Container.BindInterfacesAndSelfTo<RankingService>().FromSubContainerResolve().ByMethod(InstallRankingManager).AsSingle();
        Container.BindInterfacesAndSelfTo<StatsService>().FromSubContainerResolve().ByMethod(InstallStatsManager).AsSingle();
        Container.BindInterfacesAndSelfTo<TerrainService>().FromSubContainerResolve().ByMethod(InstallTerrainManager).AsSingle();
        Container.Bind<IMapService>().To<MapService>().AsSingle();
        Container.Bind<ISceneEventsService>().To<SceneEventsService>().AsSingle();
    }

    private void InstallBattleRoyaleManager(DiContainer subContainer)
    {
        subContainer.Bind<BattleRoyaleService>().AsSingle();
        subContainer.Bind<BattleRoyaleConfig>().FromInstance(m_BattleRoyaleConfig).AsSingle();
    }

    private void InstallGameManager(DiContainer subContainer)
    {
        subContainer.Bind<GameService>().AsSingle();
        subContainer.Bind<GameplayConfig>().FromInstance(m_GameplayConfig).AsSingle();
    }

    private void InstallRankingManager(DiContainer subContainer)
    {
        subContainer.Bind<RankingService>().AsSingle();
        subContainer.Bind<RankingConfig>().FromInstance(m_RankingConfig).AsSingle();
    }

    private void InstallStatsManager(DiContainer subContainer)
    {
        subContainer.Bind<StatsService>().AsSingle();
    }

    private void InstallTerrainManager(DiContainer subContainer)
    {
        subContainer.Bind<TerrainService>().AsSingle();
        subContainer.Bind<TerrainConfig>().FromInstance(m_TerrainConfig).AsSingle();
    }
}