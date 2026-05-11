using UnityEngine;

public sealed class PowerUp_SpeedBoost : PowerUp
{
	[Tooltip("Effective multiplier is capped at 2x by Player.c_MaxSpeed (base 50, ceiling 100).")]
	public float m_SpeedMultiplier = 1.5f;

	public override void OnPlayerTouched(Player _Player)
	{
		base.OnPlayerTouched(_Player);

		_Player.AddSpeedBoost(m_SpeedMultiplier, m_Duration);
	}
}
