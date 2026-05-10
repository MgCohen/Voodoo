using UnityEngine;
using Zenject;

public sealed class PowerUp_ColorBomb : PowerUp
{
	public float m_Radius = 8f;
	public float m_FillDuration = 0.3f;

	private ITerrainService m_TerrainService;

	[Inject]
	public void ChildConstruct(ITerrainService terrainService)
	{
		m_TerrainService = terrainService;
	}

	public override void OnPlayerTouched(Player _Player)
	{
		UnregisterMap();
		m_Model.enabled = false;
		m_ParticleSystem.Play(true);
		m_IdleParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
		m_Shadow.SetActive(false);

		Vector3 randomPos = GetRandomTerrainPosition();
		m_TerrainService.FillCircle(_Player, randomPos, m_Radius, m_FillDuration, SelfDestroy);
	}

	private Vector3 GetRandomTerrainPosition()
	{
		float halfW = m_TerrainService.WorldHalfWidth;
		float halfH = m_TerrainService.WorldHalfHeight;
		return new Vector3(
			Random.Range(-halfW + Constants.c_PowerUpPadding, halfW - Constants.c_PowerUpPadding),
			0f,
			Random.Range(-halfH + Constants.c_PowerUpPadding, halfH - Constants.c_PowerUpPadding));
	}

	private void SelfDestroy()
	{
		Destroy(gameObject);
	}
}
