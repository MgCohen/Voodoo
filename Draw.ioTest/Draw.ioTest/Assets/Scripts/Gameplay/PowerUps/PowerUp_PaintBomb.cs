using System.Collections;
using UnityEngine;
using Zenject;

public sealed class PowerUp_PaintBomb : PowerUp
{
    public static AnimationCurve s_DefaultFillCurve = null;

    public float 			m_Radius = 6.0f;
	public float			m_FillDuration = 0.3f;
	public AnimationCurve	m_FillCurve;
	private ITerrainService	m_TerrainService;
    private float           m_RadiusMultiplier;

    [Inject]
    public void ChildConstruct(ITerrainService terrainService)
    {
	    m_TerrainService = terrainService;
	    
	    m_RadiusMultiplier = 1f;
	    s_DefaultFillCurve = m_FillCurve;
    }


	public override void OnPlayerTouched (Player _Player)
	{

        m_RadiusMultiplier = Mathf.Clamp(_Player.GetSize() / _Player.GetMinSize(), 1f, 2.5f);
		UnregisterMap();
        m_Model.enabled = false;
        m_ParticleSystem.Play(true);
		m_IdleParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        m_Shadow.SetActive(false);

        m_TerrainService.FillCircle(_Player, m_Transform.position, m_Radius * m_RadiusMultiplier, m_FillDuration, SelfDestroy);
	}

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}
