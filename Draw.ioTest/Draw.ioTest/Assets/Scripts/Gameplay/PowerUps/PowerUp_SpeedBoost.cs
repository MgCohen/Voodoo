public sealed class PowerUp_SpeedBoost : PowerUp
{
	public float m_SpeedMultiplier = 1.5f;

	public override void OnPlayerTouched(Player _Player)
	{
		base.OnPlayerTouched(_Player);

		_Player.AddSpeedBoost(m_SpeedMultiplier, m_Duration);
	}
}
